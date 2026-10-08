// Copyright (C) 2015-2026 The Neo Project.
// Distributed under the MIT software license.

using Neo.Extensions;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;

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

        using var engine = ApplicationEngine.Run(tx.Script, snapshot, container: tx,
            settings: system.Settings, gas: tx.SystemFee);
        result["state"] = engine.State.ToString();
        result["gasconsumed"] = engine.FeeConsumed.ToString();
        result["minimumrequiredfee"] = engine.MinimumRequiredFee.ToString();
        result["exception"] = GetExceptionMessage(engine.FaultException);
        try { result["stack"] = new JArray(engine.ResultStack.Select(item => item.ToJson(settings.MaxItemResponseSize))); }
        catch (InvalidOperationException) { result["stack"] = "error: result cannot be serialized"; }
        try
        {
            result["notifications"] = new JArray(engine.Notifications.Select(item => new JObject
            {
                ["eventname"] = item.EventName, ["contract"] = item.ScriptHash.ToString(),
                ["state"] = item.State.ToJson(settings.MaxItemResponseSize)
            }));
        }
        catch (InvalidOperationException) { result["notifications"] = "error: result cannot be serialized"; }
        return result;
    }
}
