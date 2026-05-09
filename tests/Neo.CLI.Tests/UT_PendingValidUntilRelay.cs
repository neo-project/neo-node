// Copyright (C) 2015-2026 The Neo Project.
//
// UT_PendingValidUntilRelay.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.CLI;
using Neo.ConsoleService;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Linq;
using System.Reflection;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_PendingValidUntilRelay
{
    private NeoSystem _system = null!;
    private NEP6Wallet _wallet = null!;
    private WalletAccount _account = null!;

    [TestInitialize]
    public void Setup()
    {
        _system = TestBlockchain.GetSystem();
        _wallet = TestUtils.GenerateTestWallet("pwd");
        _account = _wallet.CreateAccount();
    }

    private Transaction CreateSignedTx(uint validUntilBlock)
    {
        var snapshot = _system.StoreView;
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 42,
            ValidUntilBlock = validUntilBlock,
            Signers = [new Signer { Account = _account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        var ctx = new ContractParametersContext(snapshot, tx, _system.Settings.Network);
        Assert.IsTrue(_wallet.Sign(ctx));
        tx.Witnesses = ctx.GetWitnesses();
        return tx;
    }

    [TestMethod]
    public void GetPendingState_NullHost_DisabledShape()
    {
        JObject j = PendingValidUntilRelay.GetPendingState(_system, null);
        Assert.IsFalse(j["enabled"]!.AsBoolean());
        Assert.AreEqual(0, j["count"]!.AsNumber());
        Assert.IsInstanceOfType(j["pending"], typeof(JArray));
        Assert.AreEqual(0, ((JArray)j["pending"]!).Count);
    }

    [TestMethod]
    public void GetPendingState_DisabledFeature_EmptyPending()
    {
        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(false, 1u));
        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.IsFalse(j["enabled"]!.AsBoolean());
        Assert.AreEqual(0, ((JArray)j["pending"]!).Count);
        Assert.AreEqual(0, j["count"]!.AsNumber());
    }

    [TestMethod]
    public void GetPendingState_WithQueuedEntry_ListsHashAndVub()
    {
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 10);
        Assert.IsTrue(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.Expired));

        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.IsTrue(j["enabled"]!.AsBoolean());
        Assert.AreEqual(1, j["count"]!.AsNumber());
        var arr = (JArray)j["pending"]!;
        Assert.AreEqual(1, arr.Count);
        Assert.AreEqual(tx.Hash.ToString(), arr[0]!["hash"]!.AsString());
        Assert.AreEqual((double)tx.ValidUntilBlock, arr[0]!["validuntilblock"]!.AsNumber(), 0.001);
        Assert.AreEqual((double)(tx.ValidUntilBlock - height), arr[0]!["blocksuntildeadline"]!.AsNumber(), 0.001);
        Assert.IsNotNull(arr[0]!["size"]);
        Assert.IsGreaterThan(0, arr[0]!["size"]!.AsNumber());
    }

    private static Block CreateBlockWithIndex(uint index)
    {
        return new Block
        {
            Header = new Header
            {
                Index = index,
                PrimaryIndex = 0,
                Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Nonce = 0,
                NextConsensus = UInt160.Zero,
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                Witness = Witness.Empty,
            },
            Transactions = [],
        };
    }

    [TestMethod]
    public void GetPendingState_EnabledWithNoEntries_ExposesP2PFlags()
    {
        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(true, 3u));
        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.IsTrue(j["enabled"]!.AsBoolean());
        Assert.IsTrue(j["pendingrelay"]!.AsBoolean());
        Assert.AreEqual(3u, (uint)j["pendingcheckfrequency"]!.AsNumber());
        Assert.AreEqual(0, j["count"]!.AsNumber());
        Assert.AreEqual(0, ((JArray)j["pending"]!).Count);
    }

    [TestMethod]
    public void GetPendingState_SkipsNonUInt256KeyLength()
    {
        var store = new MemoryStore();
        store.Put([1, 2], [0x00]);
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.AreEqual(0, j["count"]!.AsNumber());
    }

    [TestMethod]
    public void GetPendingState_SkipsCorruptSerializedTx()
    {
        var store = new MemoryStore();
        var key = new byte[UInt256.Length]; // all-zero hash key
        store.Put(key, [0xFF, 0xFE]);
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.AreEqual(0, j["count"]!.AsNumber());
    }

    [TestMethod]
    public void GetPendingState_NullHost_IncludesHeightAndIncrement()
    {
        JObject j = PendingValidUntilRelay.GetPendingState(_system, null);
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        Assert.AreEqual((double)height, j["height"]!.AsNumber(), 0.001);
        Assert.AreEqual((double)maxInc, j["maxvaliduntilblockincrement"]!.AsNumber(), 0.001);
    }

    [TestMethod]
    public void TryOffer_ReturnsFalse_WhenHostIsNull()
    {
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 3);
        Assert.IsFalse(PendingValidUntilRelay.TryOffer(_system, null, tx, VerifyResult.Expired));
    }

    [TestMethod]
    public void PendingValidUntilRelayHost_NullStore_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _ = new PendingValidUntilRelayHost(null!, new PendingValidUntilRelayConfiguration(true, 1u)));
    }

    [TestMethod]
    public void OnPersistCompleted_Disabled_DoesNotThrow()
    {
        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(false, 1u));
        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(10));
    }

    [TestMethod]
    public void OnPersistCompleted_GenesisIndex_DoesNotThrow()
    {
        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(true, 1u));
        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(0));
    }

    [TestMethod]
    public void OnPersistCompleted_BlockIndexNotOnFrequency_DoesNotThrow()
    {
        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(true, 5u));
        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(3));
    }

    [TestMethod]
    public void IsFarFutureValidityWindow_TrueWhenBeyondIncrement()
    {
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 1);
        Assert.IsTrue(PendingValidUntilRelay.IsFarFutureValidityWindow(tx, _system));
    }

    [TestMethod]
    public void IsFarFutureValidityWindow_FalseAtBoundary()
    {
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc);
        Assert.IsFalse(PendingValidUntilRelay.IsFarFutureValidityWindow(tx, _system));
    }

    [TestMethod]
    public void TryOffer_RequiresExpiredAndFarFuture()
    {
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        Assert.IsFalse(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.Succeed));
        Assert.IsFalse(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.InsufficientFunds));

        Assert.IsTrue(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.Expired));
    }

    [TestMethod]
    public void TryOffer_FrequencyZero_NoStore()
    {
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 0u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);
        Assert.IsFalse(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.Expired));
    }

    [TestMethod]
    public void TryOffer_AlreadyInLocalPendingStore_SecondCallStillTrue_NoExtraEntry()
    {
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 8);

        Assert.IsTrue(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.Expired));
        int entries1 = store.Find(null).Count();
        Assert.IsTrue(PendingValidUntilRelay.TryOffer(_system, host, tx, VerifyResult.Expired));
        int entries2 = store.Find(null).Count();
        Assert.AreEqual(1, entries1);
        Assert.AreEqual(1, entries2);
    }

    [TestMethod]
    public void ListPending_Command_Name_IsListPending()
    {
        var m = typeof(MainService).GetMethod("OnListPendingCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "CLI handler OnListPendingCommand should exist");
        var attr = m.GetCustomAttributes(typeof(ConsoleCommandAttribute), inherit: false).Cast<ConsoleCommandAttribute>().Single();
        Assert.AreEqual("list pending", string.Join(' ', attr.Verbs));
    }
}
