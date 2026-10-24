// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RpcServer.TransactionPreview.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Globalization;
using System.Reflection;

namespace Neo.Plugins.RpcServer.Tests;

public partial class UT_RpcServer
{
    private JObject Preview(Transaction tx)
        => PreviewRaw(Convert.ToBase64String(tx.ToArray()));

    private JObject PreviewRaw(string encoded)
    {
        var method = typeof(RpcServer).GetMethod("InvokeTransaction", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "The signed-transaction preflight endpoint must exist.");
        try { return (JObject)method.Invoke(_rpcServer, [encoded]); }
        catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
    }

    private Transaction SignedPreviewTransaction(byte[] script = null, long systemFee = 1_000_000,
        WitnessScope scope = WitnessScope.None)
    {
        using var snapshot = _neoSystem.GetSnapshotCache();
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 0x8127,
            ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + 100,
            SystemFee = systemFee,
            NetworkFee = 20_000_000,
            Attributes = [],
            Signers = [new Signer { Account = _walletAccount.ScriptHash, Scopes = scope }],
            Script = script ?? [(byte)OpCode.PUSH1],
            Witnesses = []
        };
        var context = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(_wallet.Sign(context));
        tx.Witnesses = context.GetWitnesses();
        return tx;
    }

    [TestMethod]
    public void TransactionPreviewVerifiesExactSignatureWithoutRelaying()
    {
        var tx = SignedPreviewTransaction();
        int poolCount = _neoSystem.MemPool.Count;
        using var snapshot = _neoSystem.GetSnapshotCache();
        var before = NativeContract.GAS.BalanceOf(snapshot, _walletAccount.ScriptHash);
        var result = Preview(tx);
        Assert.AreEqual(tx.Hash.ToString(), result["hash"].GetString());
        Assert.AreEqual("Succeed", result["verification"].GetString());
        Assert.AreEqual("HALT", result["state"].GetString());
        AssertMinimumFeeCapability(result);
        Assert.AreEqual("1", result["stack"][0]["value"].GetString());
        Assert.IsFalse(result["relayed"].GetBoolean());
        Assert.IsFalse(result["mempoolChecked"].GetBoolean());
        Assert.AreEqual(poolCount, _neoSystem.MemPool.Count);
        using var after = _neoSystem.GetSnapshotCache();
        Assert.AreEqual(before, NativeContract.GAS.BalanceOf(after, _walletAccount.ScriptHash));
        tx.SystemFee += 1;
        result = Preview(tx);
        Assert.AreEqual("InvalidSignature", result["verification"].GetString());
        Assert.IsFalse(result.ContainsProperty("stack"));
    }

    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    public void TransactionPreviewAppliesNativeFeesAndPrimaryReward(bool payerIsPrimary,
        bool exceedsAvailableBalance, bool assertTransfer)
    {
        using var initial = _neoSystem.GetSnapshotCache();
        var validators = NativeContract.NEO.GetNextBlockValidators(initial, _neoSystem.Settings.ValidatorsCount);
        var primary = Contract.CreateSignatureRedeemScript(validators[0]).ToScriptHash();
        Assert.AreEqual(primary, _walletAccount.ScriptHash);
        if (!payerIsPrimary)
        {
            _walletAccount = _wallet.CreateAccount();
            var key = new KeyBuilder(NativeContract.GAS.Id, 20).Add(_walletAccount.ScriptHash);
            initial.Add(key, new StorageItem(new AccountState { Balance = 100_000_000 }));
            initial.Commit();
        }
        const long systemFee = 10_000_000;
        const long networkFee = 20_000_000;
        var available = NativeContract.GAS.BalanceOf(initial, _walletAccount.ScriptHash)
            - systemFee - (payerIsPrimary ? 0 : networkFee);
        var amount = available + (exceedsAvailableBalance ? 1 : 0);
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(NativeContract.GAS.Hash, "transfer", _walletAccount.ScriptHash,
            UInt160.Parse("0x0102030405060708091011121314151617181920"), amount, null);
        if (assertTransfer) builder.Emit(OpCode.ASSERT);
        var tx = SignedPreviewTransaction(builder.ToArray(), systemFee, WitnessScope.CalledByEntry);
        var rawBefore = tx.ToArray();
        var storageBefore = PreviewStorage();
        var poolCount = _neoSystem.MemPool.Count;

        var result = Preview(tx);

        Assert.AreEqual("Succeed", result["verification"].GetString());
        Assert.AreEqual(assertTransfer ? "FAULT" : "HALT", result["state"].GetString());
        if (!assertTransfer)
            Assert.AreEqual(!exceedsAvailableBalance, result["stack"][0]["value"].GetBoolean());
        // Compare the entire persistent store, including GAS balances/supply and ledger records.
        CollectionAssert.AreEqual(storageBefore, PreviewStorage());
        CollectionAssert.AreEqual(rawBefore, tx.ToArray());
        Assert.AreEqual(poolCount, _neoSystem.MemPool.Count);
    }

    private string[] PreviewStorage()
    {
        using var snapshot = _neoSystem.GetSnapshotCache();
        return snapshot.Find(null).Select(p => Convert.ToHexString(p.Key.ToArray()) + ":"
            + Convert.ToHexString(p.Value.Value.Span)).ToArray();
    }

    [TestMethod]
    public void TransactionPreviewDeclaresItsSingleTransactionBlockAssumptions()
    {
        var tx = SignedPreviewTransaction();
        var storageBefore = PreviewStorage();
        using var snapshot = _neoSystem.GetSnapshotCache();
        var height = NativeContract.Ledger.CurrentIndex(snapshot);
        var hash = NativeContract.Ledger.CurrentHash(snapshot);
        var previous = NativeContract.Ledger.GetBlock(snapshot, hash);
        var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, _neoSystem.Settings.ValidatorsCount);
        var result = Preview(tx);
        var simulation = result["simulation"];
        Assert.IsNotNull(simulation);
        Assert.AreEqual(_neoSystem.Settings.Network, result["network"].AsNumber());
        Assert.AreEqual(tx.Hash.ToString(), result["hash"].GetString());
        Assert.AreEqual(height, result["snapshot"]["height"].AsNumber());
        Assert.AreEqual(hash.ToString(), result["snapshot"]["hash"].GetString());
        Assert.AreEqual("single-transaction-next-block", simulation["mode"].GetString());
        Assert.AreEqual(height + 1, simulation["height"].AsNumber());
        Assert.AreEqual((previous.Timestamp + (ulong)snapshot.GetTimePerBlock(_neoSystem.Settings).TotalMilliseconds)
            .ToString(CultureInfo.InvariantCulture), simulation["timestamp"].GetString());
        Assert.AreEqual((height + 1) % validators.Length, simulation["primaryIndex"].AsNumber());
        Assert.AreEqual(0, simulation["view"].AsNumber());
        Assert.AreEqual(1, simulation["transactionCount"].AsNumber());
        Assert.AreEqual("HALT", simulation["onPersist"].GetString());
        Assert.AreEqual(Contract.GetBFTAddress(NativeContract.NEO.ComputeNextBlockValidators(snapshot, _neoSystem.Settings))
            .ToString(), simulation["nextConsensus"].GetString());
        Assert.AreEqual(0, ((JArray)result["notifications"]).Count, "Fee burn/reward notifications must not leak into Application.");
        using var applicationOnly = ApplicationEngine.Run(tx.Script, snapshot, tx,
            settings: _neoSystem.Settings, gas: tx.SystemFee);
        Assert.AreEqual(applicationOnly.FeeConsumed.ToString(), result["gasconsumed"].GetString(),
            "OnPersist gas must not enter the Application fee quote.");
        CollectionAssert.AreEqual(storageBefore, PreviewStorage());
    }

    [TestMethod]
    public void TransactionPreviewRefusesApplicationWhenNativeOnPersistFails()
    {
        var tx = SignedPreviewTransaction();
        using (var snapshot = _neoSystem.GetSnapshotCache())
        {
            // Corrupt supply only: signature/fee verification still succeeds, but native Burn
            // faults after changing its disposable payer balance and cannot reach Application.
            snapshot.Delete(NativeContract.GAS.CreateStorageKey(11));
            snapshot.Commit();
        }
        var storageBefore = PreviewStorage();
        var poolCount = _neoSystem.MemPool.Count;
        var exception = Assert.ThrowsExactly<RpcException>(() => Preview(tx));
        Assert.AreEqual(RpcError.InternalServerError.Code, exception.HResult);
        StringAssert.Contains(exception.GetError().Data, "Transaction preview preparation failed:");
        CollectionAssert.AreEqual(storageBefore, PreviewStorage());
        Assert.AreEqual(poolCount, _neoSystem.MemPool.Count);
    }

    [TestMethod]
    public void TransactionPreviewRejectsTimestampOverflowBeforeApplication()
    {
        var tx = SignedPreviewTransaction();
        using (var snapshot = _neoSystem.GetSnapshotCache())
        {
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var previous = NativeContract.Ledger.GetBlock(snapshot, hash);
            previous.Header.Timestamp = ulong.MaxValue;
            snapshot.GetAndChange(NativeContract.Ledger.CreateStorageKey(5, hash)).Value
                = previous.ToTrimmedBlock().ToArray();
            snapshot.Commit();
        }
        var storageBefore = PreviewStorage();
        var exception = Assert.ThrowsExactly<RpcException>(() => Preview(tx));
        Assert.AreEqual(RpcError.InternalServerError.Code, exception.HResult);
        StringAssert.Contains(exception.GetError().Data, "Transaction preview preparation failed:");
        CollectionAssert.AreEqual(storageBefore, PreviewStorage());
    }

    [TestMethod]
    [DataRow(1u)]
    [DataRow(21u)]
    [DataRow(0x80000000u)]
    public void TransactionPreviewBlockFollowsDbftPrimaryAndCommitteeRefresh(uint nextHeight)
    {
        using var multiSystem = new NeoSystem(TestProtocolSettings.Default,
            new TestMemoryStoreProvider(new Neo.Persistence.Providers.MemoryStore()));
        using var snapshot = multiSystem.GetSnapshotCache();
        var hash = NativeContract.Ledger.CurrentHash(snapshot);
        var previous = NativeContract.Ledger.GetBlock(snapshot, hash);
        previous.Header.Index = nextHeight - 1;
        snapshot.GetAndChange(NativeContract.Ledger.CreateStorageKey(5, hash)).Value = previous.ToTrimmedBlock().ToArray();
        snapshot.GetAndChange(NativeContract.Ledger.CreateStorageKey(12))
            .GetInteroperable<HashIndexState>().Index = nextHeight - 1;
        // Distinguish the cached current validators from a newly computed refresh roster.
        var protocol = multiSystem.Settings with { StandbyCommittee = multiSystem.Settings.StandbyCommittee.Reverse().ToArray() };
        var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, protocol.ValidatorsCount);
        var computed = NativeContract.NEO.ComputeNextBlockValidators(snapshot, protocol);
        Assert.AreNotEqual(Contract.GetBFTAddress(validators), Contract.GetBFTAddress(computed));
        var tx = SignedPreviewTransaction();
        var block = RpcServer.CreateTransactionPreviewBlock(snapshot, tx, protocol);
        int primary = unchecked((int)nextHeight) % validators.Length;
        if (primary < 0) primary += validators.Length;
        Assert.AreEqual((byte)primary, block.PrimaryIndex);
        Assert.AreEqual(Contract.GetBFTAddress(NeoToken.ShouldRefreshCommittee(nextHeight, protocol.CommitteeMembersCount)
            ? computed : validators), block.NextConsensus);
        Assert.AreEqual(nextHeight, block.Index);
        Assert.AreEqual(hash, block.PrevHash);
        Assert.AreEqual(tx.Hash, block.MerkleRoot);
        Assert.HasCount(1, block.Transactions);
        Assert.AreSame(tx, block.Transactions[0]);
    }

    [TestMethod]
    public void TransactionPreviewBlockRejectsHeightOverflowAndEmptyValidators()
    {
        var tx = SignedPreviewTransaction();
        var storageBefore = PreviewStorage();
        using var snapshot = _neoSystem.GetSnapshotCache();
        var noValidators = _neoSystem.Settings with { ValidatorsCount = 0 };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            RpcServer.CreateTransactionPreviewBlock(snapshot, tx, noValidators));
        snapshot.GetAndChange(NativeContract.Ledger.CreateStorageKey(12))
            .GetInteroperable<HashIndexState>().Index = uint.MaxValue;
        Assert.ThrowsExactly<OverflowException>(() =>
            RpcServer.CreateTransactionPreviewBlock(snapshot, tx, _neoSystem.Settings));
        CollectionAssert.AreEqual(storageBefore, PreviewStorage());
    }

    [TestMethod]
    public void TransactionPreviewUsesSubmittedGasAndReportsApplicationFault()
    {
        var fault = Preview(SignedPreviewTransaction([(byte)OpCode.ABORT]));
        Assert.AreEqual("Succeed", fault["verification"].GetString());
        Assert.AreEqual("FAULT", fault["state"].GetString());
        AssertMinimumFeeCapability(fault);
        var underfunded = Preview(SignedPreviewTransaction(systemFee: 0));
        Assert.AreEqual("Succeed", underfunded["verification"].GetString());
        Assert.AreEqual("FAULT", underfunded["state"].GetString());
        AssertMinimumFeeCapability(underfunded);
    }

    [TestMethod]
    public void TransactionPreviewRejectsMalformedOversizedAndOverBudgetRequests()
    {
        foreach (var encoded in new[] { null, "", "not-base64", "AA==", new string('A', Transaction.MaxTransactionSize * 2) })
            Assert.ThrowsExactly<RpcException>(() => PreviewRaw(encoded));
        var tx = SignedPreviewTransaction();
        Assert.ThrowsExactly<RpcException>(() => PreviewRaw(Convert.ToBase64String([.. tx.ToArray(), 0])));
        Assert.ThrowsExactly<RpcException>(() => Preview(SignedPreviewTransaction(systemFee: _rpcServerSettings.MaxGasInvoke + 1)));
    }

    [TestMethod]
    public void TransactionPreviewBoundsStackSerializationByConfiguredItemSize()
    {
        using var builder = new ScriptBuilder();
        builder.EmitPush(new byte[256]);
        var tx = SignedPreviewTransaction(builder.ToArray());
        var unrestricted = Preview(tx);
        Assert.AreEqual("HALT", unrestricted["state"].GetString());
        Assert.AreEqual(256, Convert.FromBase64String(unrestricted["stack"][0]["value"].GetString()).Length);

        _rpcServerSettings = _rpcServerSettings with { MaxItemResponseSize = 64 };
        _rpcServer = new RpcServer(_neoSystem, _rpcServerSettings);
        var bounded = Preview(tx);
        Assert.AreEqual("Succeed", bounded["verification"].GetString());
        Assert.AreEqual("HALT", bounded["state"].GetString());
        Assert.AreEqual("error: result cannot be serialized", bounded["stack"].GetString());
        Assert.IsFalse(bounded["relayed"].GetBoolean());
    }

    [TestMethod]
    public void TransactionPreviewBoundsNotificationSerializationByConfiguredItemSize()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(NativeContract.GAS.Hash, "transfer", _walletAccount.ScriptHash,
            UInt160.Parse("0x0102030405060708091011121314151617181920"), 1, null);
        var tx = SignedPreviewTransaction(builder.ToArray(), 10_000_000, WitnessScope.CalledByEntry);
        var unrestricted = Preview(tx);
        Assert.AreEqual("HALT", unrestricted["state"].GetString());
        Assert.AreEqual("Transfer", unrestricted["notifications"][0]["eventname"].GetString());
        Assert.AreEqual(3, ((JArray)unrestricted["notifications"][0]["state"]["value"]).Count);

        _rpcServerSettings = _rpcServerSettings with { MaxItemResponseSize = 64 };
        _rpcServer = new RpcServer(_neoSystem, _rpcServerSettings);
        var bounded = Preview(tx);
        Assert.AreEqual("Succeed", bounded["verification"].GetString());
        Assert.AreEqual("HALT", bounded["state"].GetString());
        Assert.AreEqual("error: result cannot be serialized", bounded["notifications"].GetString());
        Assert.IsFalse(bounded["relayed"].GetBoolean());
    }
}
