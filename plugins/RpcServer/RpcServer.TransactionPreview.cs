// Copyright (C) 2015-2026 The Neo Project.
//
// RpcServer.TransactionPreview.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Globalization;

namespace Neo.Plugins.RpcServer;

partial class RpcServer
{
    /// <summary>
    /// Verifies and simulates a complete signed transaction against a disposable
    /// ledger snapshot. This method never relays or reserves mempool resources.
    /// </summary>
    [RpcMethod]
    protected internal virtual JToken InvokeTransaction(string base64Tx)
    {
        if (string.IsNullOrEmpty(base64Tx))
            throw new RpcException(RpcError.InvalidParams.WithData("A signed transaction is required."));
        if (base64Tx.Length > ((Transaction.MaxTransactionSize + 2) / 3) * 4)
            throw new RpcException(RpcError.InvalidSize.WithData("Transaction encoding exceeds the maximum allowed."));
        var bytes = Result.Ok_Or(() => Convert.FromBase64String(base64Tx),
            RpcError.InvalidParams.WithData("Expected a base64-encoded signed transaction."));
        if (bytes.Length > Transaction.MaxTransactionSize)
            throw new RpcException(RpcError.InvalidSize.WithData("Transaction size exceeds the maximum allowed."));
        var tx = Result.Ok_Or(() => bytes.AsSerializable<Transaction>(),
            RpcError.InvalidParams.WithData("Invalid transaction format."));
        if (!bytes.AsSpan().SequenceEqual(tx.ToArray()))
            throw new RpcException(RpcError.InvalidParams.WithData("Transaction encoding must be canonical with no trailing bytes."));
        if (tx.SystemFee > settings.MaxGasInvoke)
            throw new RpcException(RpcError.InvalidParams.WithData("System fee exceeds the RPC invocation gas limit."));

        using var snapshot = system.GetSnapshotCache();
        var result = new JObject
        {
            ["hash"] = tx.Hash.ToString(),
            ["network"] = system.Settings.Network,
            ["snapshot"] = new JObject
            {
                ["height"] = NativeContract.Ledger.CurrentIndex(snapshot),
                ["hash"] = NativeContract.Ledger.CurrentHash(snapshot).ToString()
            },
            ["relayed"] = false,
            ["mempoolChecked"] = false
        };
        var verification = tx.Verify(system.Settings, snapshot, new TransactionVerificationContext(), []);
        result["verification"] = verification.ToString();
        if (verification != VerifyResult.Succeed) return result;

        Block block;
        try
        {
            block = CreateTransactionPreviewBlock(snapshot, tx, system.Settings);
            using var persistScript = new ScriptBuilder();
            persistScript.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
            using var persist = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block,
                system.Settings, 0);
            persist.LoadScript(persistScript.ToArray());
            if (persist.Execute() != VMState.HALT)
                throw new InvalidOperationException("Native OnPersist did not HALT.", persist.FaultException);
        }
        catch (Exception exception)
        {
            throw new RpcException(RpcError.InternalServerError.WithData(
                $"Transaction preview preparation failed: {GetExceptionMessage(exception)}"));
        }
        result["simulation"] = new JObject
        {
            ["mode"] = "single-transaction-next-block",
            ["height"] = block.Index,
            ["timestamp"] = block.Timestamp.ToString(CultureInfo.InvariantCulture),
            ["primaryIndex"] = block.PrimaryIndex,
            ["view"] = 0,
            ["transactionCount"] = 1,
            ["onPersist"] = "HALT",
            ["nextConsensus"] = block.NextConsensus.ToString()
        };

        // Both phases share this hypothetical block. Never commit the disposable root snapshot.
        // Only Application notifications and fee consumption belong in the invocation result.
        using var engine = ApplicationEngine.Run(tx.Script, snapshot, container: tx,
            persistingBlock: block, settings: system.Settings, gas: tx.SystemFee);
        result["state"] = engine.State.ToString();
        result["gasconsumed"] = engine.FeeConsumed.ToString();
        if (MinimumRequiredFee.Read(engine, engine.FeeConsumed) is long minimumFee)
            result["minimumrequiredfee"] = minimumFee.ToString(System.Globalization.CultureInfo.InvariantCulture);
        result["exception"] = GetExceptionMessage(engine.FaultException);
        try { result["stack"] = new JArray(engine.ResultStack.Select(item => item.ToJson(settings.MaxItemResponseSize))); }
        catch (InvalidOperationException) { result["stack"] = "error: result cannot be serialized"; }
        try
        {
            result["notifications"] = new JArray(engine.Notifications.Select(item => new JObject
            {
                ["eventname"] = item.EventName,
                ["contract"] = item.ScriptHash.ToString(),
                ["state"] = item.State.ToJson(settings.MaxItemResponseSize)
            }));
        }
        catch (InvalidOperationException) { result["notifications"] = "error: result cannot be serialized"; }
        return result;
    }

    internal static Block CreateTransactionPreviewBlock(DataCache snapshot, Transaction tx,
        ProtocolSettings protocolSettings)
    {
        var hash = NativeContract.Ledger.CurrentHash(snapshot);
        var previous = NativeContract.Ledger.GetBlock(snapshot, hash)
            ?? throw new InvalidOperationException("The persisted block is unavailable.");
        var height = checked(NativeContract.Ledger.CurrentIndex(snapshot) + 1);
        if (previous.Index != height - 1)
            throw new InvalidOperationException("The persisted block height is inconsistent.");
        var interval = snapshot.GetTimePerBlock(protocolSettings).Ticks / TimeSpan.TicksPerMillisecond;
        if (interval <= 0)
            throw new InvalidOperationException("The block interval must be positive.");
        var timestamp = checked(previous.Timestamp + (ulong)interval);
        var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, protocolSettings.ValidatorsCount);
        if (validators.Length == 0 || validators.Length > byte.MaxValue + 1)
            throw new InvalidOperationException("The next block validator roster is invalid.");
        var nextValidators = NeoToken.ShouldRefreshCommittee(height, protocolSettings.CommitteeMembersCount)
            ? NativeContract.NEO.ComputeNextBlockValidators(snapshot, protocolSettings)
            : validators;
        if (nextValidators.Length == 0)
            throw new InvalidOperationException("The next consensus validator roster is empty.");
        // Use the DBFT view-zero primary calculation, including its signed-height conversion.
        int primary = unchecked((int)height) % validators.Length;
        if (primary < 0) primary += validators.Length;
        return new Block
        {
            Header = new Header
            {
                Version = 0,
                PrevHash = hash,
                MerkleRoot = tx.Hash,
                Timestamp = timestamp,
                Index = height,
                PrimaryIndex = (byte)primary,
                NextConsensus = Contract.GetBFTAddress(nextValidators),
                Nonce = 0,
                Witness = Witness.Empty
            },
            Transactions = [tx]
        };
    }
}
