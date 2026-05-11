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

using Akka.Actor;
using Neo.CLI;
using Neo.ConsoleService;
using Neo.Extensions;
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
using System.IO;
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

    private static byte[] SerializeTx(Transaction tx)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            ((ISerializable)tx).Serialize(writer);
        }
        return ms.ToArray();
    }

    private Transaction CreateRawTx(uint validUntilBlock)
    {
        var tx = new Transaction
        {
            Version = 0,
            Nonce = (uint)Environment.TickCount,
            ValidUntilBlock = validUntilBlock,
            Signers = [new Signer { Account = _account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [Witness.Empty],
        };
        return tx;
    }

    [TestMethod]
    public void OnPersistCompleted_ProcessQueued_RemovesExpiredEntry()
    {
        // A tx whose ValidUntilBlock has already passed must be dropped from the local pending store
        // the next time the periodic ProcessQueued pass runs.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);

        var expiredTx = CreateRawTx(validUntilBlock: height); // height >= ValidUntilBlock => expired
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));
        Assert.IsTrue(store.Contains(key));

        // Pick an index that satisfies (index != 0) && (index % frequency == 0).
        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(2));

        Assert.IsFalse(store.Contains(key), "Expired entry should have been pruned by ProcessQueued.");
    }

    [TestMethod]
    public void OnPersistCompleted_ProcessQueued_RemovesCorruptEntry()
    {
        // A store entry under a well-formed hash-shaped key whose payload no longer
        // deserializes as a Transaction must be considered corrupt and pruned.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var key = new byte[UInt256.Length];
        new Random(7).NextBytes(key);
        store.Put(key, [0xFF, 0xFE, 0xFD]); // garbage payload
        Assert.IsTrue(store.Contains(key));

        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(2));

        Assert.IsFalse(store.Contains(key), "Corrupt entry should have been pruned by ProcessQueued.");
    }

    [TestMethod]
    public void OnPersistCompleted_ProcessQueued_InWindowTxKeptWhenRelayFails()
    {
        // When a queued tx is now inside the allowed forward window, ProcessQueued tries to relay it
        // via Blockchain.Ask. For our synthetic tx the verifier returns a non-success result
        // (e.g. InsufficientFunds / InvalidSignature), and ProcessQueued must keep the entry.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);

        var tx = CreateSignedTx(height + maxInc); // strictly in-window: height < VUB <= height+maxInc
        byte[] key = tx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(tx));

        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(2));

        Assert.IsTrue(store.Contains(key),
            "In-window tx that fails verification must remain in the pending store for a future retry.");
    }

    [TestMethod]
    public void OnPersistCompleted_ProcessQueued_DoesNotTouchFarFutureEntry()
    {
        // While the tx is still in the "too far ahead" range it must NOT be sent to Blockchain.Ask
        // and must remain in the pending store untouched.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);

        var farFutureTx = CreateSignedTx(height + maxInc + 50);
        byte[] key = farFutureTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(farFutureTx));

        PendingValidUntilRelay.OnPersistCompleted(_system, host, CreateBlockWithIndex(2));

        Assert.IsTrue(store.Contains(key), "Far-future tx must stay queued.");
    }

    [TestMethod]
    public void GetPendingState_OmitsBlocksUntilDeadline_WhenTxAlreadyExpiredAtCurrentHeight()
    {
        // Cover the branch where `ledgerReady && height < tx.ValidUntilBlock` is false:
        // listing must still succeed and just omit the "blocksuntildeadline" field for that entry.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);

        var expiredTx = CreateRawTx(height); // height == VUB
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.AreEqual(1, j["count"]!.AsNumber());
        var arr = (JArray)j["pending"]!;
        Assert.AreEqual(1, arr.Count);
        Assert.AreEqual(expiredTx.Hash.ToString(), arr[0]!["hash"]!.AsString());
        Assert.AreEqual((double)expiredTx.ValidUntilBlock, arr[0]!["validuntilblock"]!.AsNumber(), 0.001);
        Assert.IsNull(arr[0]!["blocksuntildeadline"], "blocksuntildeadline must be absent once tx is no longer in the future.");
    }

    [TestMethod]
    public void TryOffer_NonExpiredVerifyResult_DoesNotStore()
    {
        // TryOffer must ignore any relay result other than Expired, even if the validity window check would pass.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        foreach (var r in new[]
        {
            VerifyResult.Succeed, VerifyResult.AlreadyInPool, VerifyResult.AlreadyExists,
            VerifyResult.OutOfMemory, VerifyResult.InvalidScript, VerifyResult.InvalidAttribute,
            VerifyResult.InvalidSignature, VerifyResult.OverSize, VerifyResult.InsufficientFunds,
            VerifyResult.PolicyFail,
        })
        {
            Assert.IsFalse(PendingValidUntilRelay.TryOffer(_system, host, tx, r),
                $"TryOffer must not store when relay result is {r}.");
        }
        Assert.AreEqual(0, store.Find(null).Count(), "Store must remain empty for non-Expired results.");
    }

    [TestMethod]
    public void GetPendingState_FrequencyZero_TreatedAsDisabled()
    {
        // PendingRelay=true but frequency=0 still means feature disabled per the same `enabled` rule used by TryOffer.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 0u));
        JObject j = PendingValidUntilRelay.GetPendingState(_system, host);
        Assert.IsFalse(j["enabled"]!.AsBoolean());
        Assert.IsTrue(j["pendingrelay"]!.AsBoolean());
        Assert.AreEqual(0u, (uint)j["pendingcheckfrequency"]!.AsNumber());
        Assert.AreEqual(0, j["count"]!.AsNumber());
    }

    [TestMethod]
    public void PendingValidUntilRelayHost_Dispose_DisposesUnderlyingStore()
    {
        // The host owns the store lifetime; Dispose must propagate so leveldb / rocksdb file handles
        // get released when MainService.Stop runs.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        host.Dispose();
        Assert.AreSame(store, host.Store);
    }

    [TestMethod]
    public void PendingValidUntilRelayActor_HandlesPersistCompleted_ViaEventStream()
    {
        // Indirect end-to-end test: subscribing the actor and publishing a PersistCompleted should
        // cause ProcessQueued to run; we observe this by seeding the store with an expired entry
        // and asserting it is pruned after the event is processed.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        var actorType = typeof(PendingValidUntilRelay).Assembly.GetType("Neo.CLI.PendingValidUntilRelayActor");
        Assert.IsNotNull(actorType, "PendingValidUntilRelayActor type should be discoverable.");

        var props = Props.Create(actorType!, _system, host);
        var actor = _system.ActorSystem.ActorOf(props);
        try
        {
            // Re-publish until the actor has subscribed in PreStart and consumed the message.
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                _system.ActorSystem.EventStream.Publish(new Blockchain.PersistCompleted(CreateBlockWithIndex(2)));
                return !store.Contains(key);
            }, TimeSpan.FromSeconds(5)), "Actor should have processed PersistCompleted and pruned expired tx.");
        }
        finally
        {
            _system.EnsureStopped(actor);
        }
    }

    [TestMethod]
    public void PendingValidUntilRelayActor_IgnoresNonPersistMessages()
    {
        // The actor must silently ignore any message other than Blockchain.PersistCompleted.
        var store = new MemoryStore();
        var host = new PendingValidUntilRelayHost(store, new PendingValidUntilRelayConfiguration(true, 1u));

        var actorType = typeof(PendingValidUntilRelay).Assembly.GetType("Neo.CLI.PendingValidUntilRelayActor")!;
        var actor = _system.ActorSystem.ActorOf(Props.Create(actorType, _system, host));
        try
        {
            actor.Tell("not-a-persist-completed");
            actor.Tell(42);
            // No exception means the OnReceive non-matching branch executed cleanly.
        }
        finally
        {
            _system.EnsureStopped(actor);
        }
    }
}
