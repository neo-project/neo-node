// Copyright (C) 2015-2026 The Neo Project.
//
// UT_DeferredRelayEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.CLI.Tests;
using Neo.ConsoleService;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Reflection;

namespace Neo.Plugins.DeferredRelay.Tests;

[TestClass]
public class UT_DeferredRelayEngine
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

    private static DeferredRelaySettings EnabledSettings(uint max = 10000u, uint freq = 1u) => DeferredRelaySettings.Create(max, freq);
    private static DeferredRelaySettings DisabledSettings() => DeferredRelaySettings.Create(0u, 1u);

    private static void RunPersistProcessing(NeoSystem system, IStore store, DeferredRelaySettings settings, Block block)
    {
        if (!DeferredRelayEngine.ShouldProcessPersist(block, settings))
            return;
        DeferredRelayEngine.ProcessQueuedAsync(system, store, settings).GetAwaiter().GetResult();
    }

    private Transaction CreateSignedTx(uint validUntilBlock, uint nonce = 42)
    {
        var snapshot = _system.StoreView;
        var tx = new Transaction
        {
            Version = 0,
            Nonce = nonce,
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
    public void GetPendingState_NoStore_DisabledShape()
    {
        JObject j = DeferredRelayEngine.GetPendingState(_system, null, DisabledSettings());
        Assert.IsFalse(j["enabled"]!.AsBoolean());
        Assert.AreEqual(0u, (uint)j["pendingrelaymaxtransactions"]!.AsNumber());
        Assert.AreEqual(0, j["count"]!.AsNumber());
        Assert.IsInstanceOfType<JArray>(j["pending"]);
        Assert.AreEqual(0, ((JArray)j["pending"]!).Count);
    }

    [TestMethod]
    public void GetPendingState_DisabledFeature_EmptyPending()
    {
        var store = new MemoryStore();
        JObject j = DeferredRelayEngine.GetPendingState(_system, store, DisabledSettings());
        Assert.IsFalse(j["enabled"]!.AsBoolean());
        Assert.AreEqual(0, ((JArray)j["pending"]!).Count);
    }

    [TestMethod]
    public void GetPendingState_Enabled_IncludesChainAndPluginFields()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings(max: 8192u, freq: 5u);
        JObject j = DeferredRelayEngine.GetPendingState(_system, store, settings);

        Assert.IsTrue(j["enabled"]!.AsBoolean());
        Assert.AreEqual(8192u, (uint)j["pendingrelaymaxtransactions"]!.AsNumber());
        Assert.AreEqual(5u, (uint)j["pendingcheckfrequency"]!.AsNumber());
        Assert.IsNotNull(j["height"]);
        Assert.IsNotNull(j["maxvaliduntilblockincrement"]);
        Assert.IsNull(j["pendingrelay"]);
    }

    [TestMethod]
    public void GetPendingState_WithQueuedEntry_ListsHashAndVub()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 10);
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid));

        JObject j = DeferredRelayEngine.GetPendingState(_system, store, settings);
        Assert.IsTrue(j["enabled"]!.AsBoolean());
        Assert.AreEqual(1, j["count"]!.AsNumber());
        var arr = (JArray)j["pending"]!;
        Assert.AreEqual(1, arr.Count);
        Assert.AreEqual(tx.Hash.ToString(), arr[0]!["hash"]!.AsString());
        Assert.AreEqual((double)tx.ValidUntilBlock, arr[0]!["validuntilblock"]!.AsNumber(), 0.001);
        Assert.AreEqual((double)(tx.ValidUntilBlock - height), arr[0]!["blocksuntildeadline"]!.AsNumber(), 0.001);
    }

    [TestMethod]
    public void TryGetPendingTx_ReturnsBytesForQueuedHash()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var tx = CreateSignedTx(height + maxInc + 10);
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid));

        var raw = DeferredRelayEngine.TryGetPendingTx(store, tx.Hash);
        Assert.IsNotNull(raw);
        // Round-trip the bytes through Transaction deserialization and verify identity by hash.
        Assert.AreEqual(tx.Hash, raw.AsSerializable<Transaction>().Hash);
    }

    [TestMethod]
    public void TryGetPendingTx_ReturnsNullForUnknownHash()
    {
        var store = new MemoryStore();
        var unknown = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20");

        Assert.IsNull(DeferredRelayEngine.TryGetPendingTx(store, unknown));
    }

    private static Block CreateBlockWithIndex(uint index) => new()
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

    [TestMethod]
    public void TryOffer_WhenDisabled_ReturnsFalse()
    {
        var store = new MemoryStore();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        Assert.IsFalse(DeferredRelayEngine.TryOffer(_system, store, DisabledSettings(), tx, VerifyResult.NotYetValid));
    }

    [TestMethod]
    public void TryOffer_WhenAlreadyQueued_ReturnsTrueWithoutDuplicate()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid));
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid));
        Assert.AreEqual(1, store.Find(null).Count());
    }

    [TestMethod]
    public void TryOffer_RequiresNotYetValid()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        Assert.IsFalse(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.Succeed));
        Assert.IsFalse(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.Expired));
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid));
    }

    [TestMethod]
    public void TryOffer_WhenAtMaxTransactions_RejectsNewHash()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings(max: 2u);
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        uint vub = height + maxInc + 10;
        var tx1 = CreateSignedTx(vub, nonce: 1);
        var tx2 = CreateSignedTx(vub, nonce: 2);
        var tx3 = CreateSignedTx(vub, nonce: 3);

        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx1, VerifyResult.NotYetValid));
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx2, VerifyResult.NotYetValid));
        Assert.IsFalse(DeferredRelayEngine.TryOffer(_system, store, settings, tx3, VerifyResult.NotYetValid));
        Assert.AreEqual(2, store.Find(null).Count());
    }

    [TestMethod]
    public void EntryCounter_InitialValue_AndIncrementDecrement()
    {
        var c = new EntryCounter(7);
        Assert.AreEqual(7, c.Value);
        c.Increment();
        c.Increment();
        Assert.AreEqual(9, c.Value);
        c.Decrement();
        Assert.AreEqual(8, c.Value);
    }

    [TestMethod]
    public void TryOffer_WithCounter_IncrementsOnAcceptAndNotOnReject()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var counter = new EntryCounter(0);
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid, counter));
        Assert.AreEqual(1, counter.Value);

        // Same hash again: TryOffer returns true (already queued) but must NOT double-count.
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid, counter));
        Assert.AreEqual(1, counter.Value);

        // Non-NotYetValid relay results never increment.
        Assert.IsFalse(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.Succeed, counter));
        Assert.AreEqual(1, counter.Value);
    }

    [TestMethod]
    public void TryOffer_WithCounter_UsesCounterForCapacityCheckNotStoreScan()
    {
        // Empty store but counter pre-seeded at the cap: capacity check must consult the counter
        // (the whole point of the optimization) and reject without touching the store.
        var store = new MemoryStore();
        var settings = EnabledSettings(max: 1u);
        var counter = new EntryCounter(1);
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 5);

        Assert.IsFalse(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid, counter));
        Assert.AreEqual(0, store.Find(null).Count());
        Assert.AreEqual(1, counter.Value);
    }

    [TestMethod]
    public void ProcessQueuedAsync_WithCounter_DecrementsOnExpiredAndCorruptDeletes()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);

        // 1 expired (32-byte key) + 1 corrupt (32-byte key) entries; counter starts at 2.
        var expired = CreateRawTx(height);
        store.Put(expired.Hash.GetSpan().ToArray(), SerializeTx(expired));
        var corruptKey = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20").GetSpan().ToArray();
        store.Put(corruptKey, [0xFF]);
        var counter = new EntryCounter(2);

        DeferredRelayEngine.ProcessQueuedAsync(_system, store, settings, counter: counter).GetAwaiter().GetResult();

        Assert.AreEqual(0, counter.Value);
        Assert.IsFalse(store.Contains(expired.Hash.GetSpan().ToArray()));
        Assert.IsFalse(store.Contains(corruptKey));
    }

    [TestMethod]
    public void DeferredRelayActor_BootstrapsCounter_FromExistingStoreEntries()
    {
        // Pre-seed store with `max` entries before starting the actor: the actor must bootstrap its
        // counter via CountEntries and reject the next NotYetValid offer instead of letting the
        // store grow past `max`.
        var store = new MemoryStore();
        var settings = EnabledSettings(max: 2u, freq: 1u);
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        uint vub = height + maxInc + 100;

        var seed1 = CreateSignedTx(vub, nonce: 91);
        var seed2 = CreateSignedTx(vub, nonce: 92);
        store.Put(seed1.Hash.GetSpan().ToArray(), SerializeTx(seed1));
        store.Put(seed2.Hash.GetSpan().ToArray(), SerializeTx(seed2));

        var newTx = CreateSignedTx(vub, nonce: 93);
        var actor = _system.ActorSystem.ActorOf(Props.Create(() => new DeferredRelayActor(_system, store, settings)));
        try
        {
            // Publish a NotYetValid RelayResult; if the bootstrap is wrong (counter==0), the actor would accept it.
            // With a correct bootstrap (counter==2), the offer is rejected and the new hash never lands in the store.
            for (int i = 0; i < 10; i++)
            {
                _system.ActorSystem.EventStream.Publish(new Blockchain.RelayResult(newTx, VerifyResult.NotYetValid));
                Thread.Sleep(20);
            }

            Assert.IsFalse(store.Contains(newTx.Hash.GetSpan().ToArray()),
                "Bootstrapped counter should be at the cap, so the new tx must be rejected");
            Assert.IsTrue(store.Contains(seed1.Hash.GetSpan().ToArray()));
            Assert.IsTrue(store.Contains(seed2.Hash.GetSpan().ToArray()));
        }
        finally
        {
            _system.EnsureStopped(actor);
        }
    }

    [TestMethod]
    public void DeferredRelaySettings_MaxRelayPerCycle_FoldsToMin()
    {
        // Below the global default: cap equals MaxTransactions so a single cycle can still drain the whole queue.
        Assert.AreEqual(3u, DeferredRelaySettings.Create(maxTransactions: 3u, checkFrequency: 1u).MaxRelayPerCycle);
        // At the boundary: still equals the default.
        Assert.AreEqual(DeferredRelaySettings.DefaultMaxRelayPerCycle,
            DeferredRelaySettings.Create(maxTransactions: DeferredRelaySettings.DefaultMaxRelayPerCycle, checkFrequency: 1u).MaxRelayPerCycle);
        // Above the default: cap stays at the default so the Blockchain actor mailbox is never flooded.
        Assert.AreEqual(DeferredRelaySettings.DefaultMaxRelayPerCycle,
            DeferredRelaySettings.Create(maxTransactions: 50000u, checkFrequency: 1u).MaxRelayPerCycle);
    }

    [TestMethod]
    public void ProcessQueuedAsync_RespectsMaxRelayPerCycleCap()
    {
        // MaxRelayPerCycle == min(MaxTransactions, 256). Setting MaxTransactions=3 collapses the cap to 3,
        // so even when 5 in-window entries are queued, a single ProcessQueuedAsync cycle must publish at
        // most 3 RelayResult events (one per Blockchain.Ask attempt observed on the EventStream).
        var store = new MemoryStore();
        var settings = EnabledSettings(max: 3u);
        Assert.AreEqual(3u, settings.MaxRelayPerCycle, "Precondition: MaxRelayPerCycle should fold to MaxTransactions when it is below the default");

        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        Assert.AreNotEqual(0u, maxInc);

        var seededHashes = new List<UInt256>();
        for (uint i = 0; i < 5; i++)
        {
            var tx = CreateRawTx(height + 1, nonce: 1000u + i);
            store.Put(tx.Hash.GetSpan().ToArray(), SerializeTx(tx));
            seededHashes.Add(tx.Hash);
        }
        Assert.AreEqual(5, store.Find(null).Count());

        var (probe, observed, locker) = SubscribeRelayResults();
        try
        {
            DeferredRelayEngine.ProcessQueuedAsync(_system, store, settings).GetAwaiter().GetResult();
            // Allow any in-flight RelayResult events the Blockchain actor may still be publishing to surface.
            Thread.Sleep(200);

            int attempts;
            lock (locker)
                attempts = observed.Count(rr => rr.Inventory is Transaction t && seededHashes.Contains(t.Hash));
            Assert.IsLessThanOrEqualTo(3, attempts, $"Cap exceeded: observed {attempts} relay attempts for cap=3");
        }
        finally
        {
            _system.EnsureStopped(probe);
        }
    }

    [TestMethod]
    public void GetPendingState_SkipsInvalidKeysAndCorruptEntries()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 10);
        Assert.IsTrue(DeferredRelayEngine.TryOffer(_system, store, settings, tx, VerifyResult.NotYetValid));

        store.Put([1, 2, 3], [0xFF]);
        store.Put(UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20").GetSpan().ToArray(), [0xFF, 0xFE]);

        JObject j = DeferredRelayEngine.GetPendingState(_system, store, settings);
        Assert.AreEqual(1, j["count"]!.AsNumber());
        Assert.AreEqual(tx.Hash.ToString(), ((JArray)j["pending"]!)[0]!["hash"]!.AsString());
    }

    [TestMethod]
    public void ShouldProcessPersist_RespectsEnabledBlockZeroAndFrequency()
    {
        var settings = EnabledSettings(freq: 5u);
        Assert.IsFalse(DeferredRelayEngine.ShouldProcessPersist(CreateBlockWithIndex(2), DisabledSettings()));
        Assert.IsFalse(DeferredRelayEngine.ShouldProcessPersist(CreateBlockWithIndex(0), settings));
        Assert.IsFalse(DeferredRelayEngine.ShouldProcessPersist(CreateBlockWithIndex(3), settings));
        Assert.IsTrue(DeferredRelayEngine.ShouldProcessPersist(CreateBlockWithIndex(5), settings));
    }

    [TestMethod]
    public void ProcessQueued_WhenDisabled_DoesNotProcess()
    {
        var store = new MemoryStore();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        RunPersistProcessing(_system, store, DisabledSettings(), CreateBlockWithIndex(2));
        Assert.IsTrue(store.Contains(key));
    }

    [TestMethod]
    public void ProcessQueued_BlockZero_DoesNotProcess()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        RunPersistProcessing(_system, store, settings, CreateBlockWithIndex(0));
        Assert.IsTrue(store.Contains(key));
    }

    [TestMethod]
    public void ProcessQueued_CheckFrequency_SkipsNonMatchingBlock()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings(freq: 5u);
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        RunPersistProcessing(_system, store, settings, CreateBlockWithIndex(3));
        Assert.IsTrue(store.Contains(key));

        RunPersistProcessing(_system, store, settings, CreateBlockWithIndex(5));
        Assert.IsFalse(store.Contains(key));
    }

    [TestMethod]
    public void ProcessQueued_RemovesExpiredEntry()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);

        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        RunPersistProcessing(_system, store, settings, CreateBlockWithIndex(2));
        Assert.IsFalse(store.Contains(key));
    }

    [TestMethod]
    public void ProcessQueued_RemovesCorruptEntry()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        byte[] key = UInt256.Zero.GetSpan().ToArray();
        store.Put(key, [0xFF]);

        RunPersistProcessing(_system, store, settings, CreateBlockWithIndex(1));
        Assert.IsFalse(store.Contains(key));
    }

    [TestMethod]
    public void DeferredRelayActor_IgnoresNonNotYetValidRelayResult()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 20);

        var actor = _system.ActorSystem.ActorOf(Props.Create(() => new DeferredRelayActor(_system, store, settings)));
        try
        {
            _system.ActorSystem.EventStream.Publish(new Blockchain.RelayResult(tx, VerifyResult.Expired));
            Thread.Sleep(100);
            Assert.IsFalse(store.Contains(tx.Hash.GetSpan().ToArray()));
        }
        finally
        {
            _system.EnsureStopped(actor);
        }
    }

    [TestMethod]
    public void DeferredRelayActor_QueuesNotYetValidRelayResult()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        var tx = CreateSignedTx(height + maxInc + 20);

        var actor = _system.ActorSystem.ActorOf(Props.Create(() => new DeferredRelayActor(_system, store, settings)));
        try
        {
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                _system.ActorSystem.EventStream.Publish(new Blockchain.RelayResult(tx, VerifyResult.NotYetValid));
                return store.Contains(tx.Hash.GetSpan().ToArray());
            }, TimeSpan.FromSeconds(5)));
        }
        finally
        {
            _system.EnsureStopped(actor);
        }
    }

    [TestMethod]
    public void DeferredRelayActor_HandlesPersistCompleted_ViaEventStream()
    {
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        var actor = _system.ActorSystem.ActorOf(Props.Create(() => new DeferredRelayActor(_system, store, settings)));
        try
        {
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                _system.ActorSystem.EventStream.Publish(new Blockchain.PersistCompleted(CreateBlockWithIndex(2)));
                return !store.Contains(key);
            }, TimeSpan.FromSeconds(10)));
        }
        finally
        {
            _system.EnsureStopped(actor);
        }
    }

    [TestMethod]
    public void ListPending_Command_RegisteredOnPlugin()
    {
        var m = typeof(DeferredRelayPlugin).GetMethod("OnListPendingCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m);
        var attr = m.GetCustomAttributes(typeof(ConsoleCommandAttribute), inherit: false).Cast<ConsoleCommandAttribute>().Single();
        Assert.AreEqual("list pending", string.Join(' ', attr.Verbs));
    }

    // Captures Blockchain.RelayResult events from the actor system EventStream, so that tests can
    // distinguish between "ProcessQueuedAsync removed an entry via toRemove (no relay attempt)" and
    // "ProcessQueuedAsync attempted to relay an entry (Blockchain published a RelayResult)".
    private sealed class RelayResultProbe : UntypedActor
    {
        private readonly Action<Blockchain.RelayResult> _onReceived;
        public RelayResultProbe(Action<Blockchain.RelayResult> onReceived) => _onReceived = onReceived;
        protected override void OnReceive(object message)
        {
            if (message is Blockchain.RelayResult rr) _onReceived(rr);
        }
    }

    private (IActorRef probe, List<Blockchain.RelayResult> observed, object locker) SubscribeRelayResults()
    {
        var observed = new List<Blockchain.RelayResult>();
        var locker = new object();
        Action<Blockchain.RelayResult> onReceived = rr => { lock (locker) observed.Add(rr); };
        var probe = _system.ActorSystem.ActorOf(Props.Create(() => new RelayResultProbe(onReceived)));
        _system.ActorSystem.EventStream.Subscribe(probe, typeof(Blockchain.RelayResult));
        return (probe, observed, locker);
    }

    [TestMethod]
    public void ProcessQueued_TooFarInFuture_KeepsEntryAndDoesNotAttemptRelay()
    {
        // Covers: height < tx.ValidUntilBlock AND tx.ValidUntilBlock > height + maxInc.
        // The entry is still NotYetValid (outside the relay window), so it must stay in the store
        // AND no Blockchain relay attempt should occur for it.
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);

        var tx = CreateRawTx(height + maxInc + 100);
        byte[] key = tx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(tx));

        var (probe, observed, locker) = SubscribeRelayResults();
        try
        {
            DeferredRelayEngine.ProcessQueuedAsync(_system, store, settings).GetAwaiter().GetResult();
            // Give the actor system a brief window to publish any stray RelayResult events.
            Thread.Sleep(100);

            Assert.IsTrue(store.Contains(key), "Tx beyond the relay window must remain in store");
            lock (locker)
            {
                Assert.IsFalse(
                    observed.Any(rr => rr.Inventory is Transaction t && t.Hash == tx.Hash),
                    "Tx beyond the relay window must not trigger a Blockchain relay attempt");
            }
        }
        finally
        {
            _system.EnsureStopped(probe);
        }
    }

    [TestMethod]
    public void ProcessQueued_InRelayWindow_AttemptsRelay()
    {
        // Covers: height < tx.ValidUntilBlock AND tx.ValidUntilBlock <= height + maxInc.
        // The entry just entered the allowed forward window, so a Blockchain.Ask<RelayResult>
        // must be issued (observed as a RelayResult published on the EventStream by the Blockchain actor).
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_system.Settings);
        Assert.AreNotEqual(0u, maxInc, "Test precondition: MaxValidUntilBlockIncrement must be positive");

        uint vub = height + 1; // height < vub <= height + maxInc
        Assert.IsTrue(vub > height && vub <= height + maxInc);
        var tx = CreateRawTx(vub);
        byte[] key = tx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(tx));

        var (probe, observed, locker) = SubscribeRelayResults();
        try
        {
            DeferredRelayEngine.ProcessQueuedAsync(_system, store, settings).GetAwaiter().GetResult();

            bool relayAttempted = SpinWait.SpinUntil(() =>
            {
                lock (locker)
                    return observed.Any(rr => rr.Inventory is Transaction t && t.Hash == tx.Hash);
            }, TimeSpan.FromSeconds(5));
            Assert.IsTrue(relayAttempted, "Tx inside the relay window must trigger a Blockchain relay attempt");
        }
        finally
        {
            _system.EnsureStopped(probe);
        }
    }

    [TestMethod]
    public void ProcessQueued_ExpiredEntry_DoesNotAttemptRelay()
    {
        // Strengthens ProcessQueued_RemovesExpiredEntry: confirms expired txs are removed via the
        // toRemove path (no Blockchain relay attempt), so the `height < tx.ValidUntilBlock` guard
        // in the second `if` is observably exercised.
        var store = new MemoryStore();
        var settings = EnabledSettings();
        var snapshot = _system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);

        var expiredTx = CreateRawTx(height);
        byte[] key = expiredTx.Hash.GetSpan().ToArray();
        store.Put(key, SerializeTx(expiredTx));

        var (probe, observed, locker) = SubscribeRelayResults();
        try
        {
            DeferredRelayEngine.ProcessQueuedAsync(_system, store, settings).GetAwaiter().GetResult();
            Thread.Sleep(100);

            Assert.IsFalse(store.Contains(key), "Expired tx should be removed");
            lock (locker)
            {
                Assert.IsFalse(
                    observed.Any(rr => rr.Inventory is Transaction t && t.Hash == expiredTx.Hash),
                    "Expired tx should be removed via toRemove without a Blockchain relay attempt");
            }
        }
        finally
        {
            _system.EnsureStopped(probe);
        }
    }

    private static byte[] SerializeTx(Transaction tx)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
            ((ISerializable)tx).Serialize(writer);
        return ms.ToArray();
    }

    private Transaction CreateRawTx(uint validUntilBlock, uint nonce = 0) => new()
    {
        Version = 0,
        // Default nonce uses TickCount for one-off tests; callers that seed multiple txs
        // must pass distinct nonce values to avoid hash collisions in the store.
        Nonce = nonce == 0 ? (uint)Environment.TickCount : nonce,
        ValidUntilBlock = validUntilBlock,
        Signers = [new Signer { Account = _account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
        Attributes = [],
        Script = new byte[] { (byte)OpCode.RET },
        Witnesses = [Witness.Empty],
    };
}
