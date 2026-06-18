// Copyright (C) 2015-2026 The Neo Project.
//
// DeferredRelayEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;

namespace Neo.Plugins.DeferredRelay;

internal static class DeferredRelayEngine
{
    /// <summary>
    /// Builds a snapshot of locally queued NotYetValid transactions and plugin settings (RPC/CLI listing).
    /// </summary>
    public static JObject GetPendingState(NeoSystem system, IStore? store, DeferredRelaySettings settings)
    {
        var snapshot = system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);

        var root = new JObject
        {
            ["height"] = height,
            ["maxvaliduntilblockincrement"] = maxInc,
            ["pending"] = new JArray(),
        };

        if (store is null || !settings.Enabled)
        {
            root["enabled"] = false;
            root["pendingcheckfrequency"] = 0u;
            root["pendingrelaymaxtransactions"] = 0u;
            root["count"] = 0;
            return root;
        }

        root["enabled"] = true;
        root["pendingcheckfrequency"] = settings.CheckFrequency;
        root["pendingrelaymaxtransactions"] = settings.MaxTransactions;

        var arr = (JArray)root["pending"]!;
        foreach ((byte[] key, byte[] value) in store.Find())
        {
            // Schema guard: only 32-byte (UInt256) keys are transaction entries; see CountEntries.
            if (key.Length != UInt256.Length) continue;
            try
            {
                Transaction tx = value.AsSerializable<Transaction>();
                var o = new JObject
                {
                    ["hash"] = tx.Hash.ToString(),
                    ["validuntilblock"] = tx.ValidUntilBlock,
                    ["size"] = value.Length,
                };
                if (height < tx.ValidUntilBlock)
                    o["blocksuntildeadline"] = tx.ValidUntilBlock - height;
                arr.Add(o);
            }
            catch { /* skip corrupt */ }
        }

        root["count"] = arr.Count;
        return root;
    }

    /// <summary>
    /// Persists the transaction when relay verification returned <see cref="VerifyResult.NotYetValid"/>.
    /// </summary>
    /// <param name="counter">
    /// Optional in-memory entry counter. When supplied, the capacity check uses the counter value
    /// instead of scanning the store, and the counter is incremented on a successful <c>Put</c>.
    /// Callers that do not maintain a counter (e.g. tests) pass <c>null</c>, falling back to <see cref="CountEntries"/>.
    /// </param>
    /// <param name="queueContext">
    /// Optional in-memory deferred-queue state (per-sender limits and fee aggregation).
    /// When <c>null</c>, a fresh context is bootstrapped from <paramref name="store"/> on each call.
    /// </param>
    public static bool TryOffer(NeoSystem system, IStore store, DeferredRelaySettings settings, Transaction tx, VerifyResult relayResult, EntryCounter? counter = null, DeferredQueueContext? queueContext = null)
    {
        if (!settings.Enabled)
            return false;
        if (relayResult != VerifyResult.NotYetValid)
            return false;

        UInt256 hash = tx.Hash;
        if (system.ContainsTransaction(hash) != ContainsTransactionType.NotExist)
            return false;

        byte[] key = hash.GetSpan().ToArray();
        if (store.Contains(key))
            return true;

        // Fallback callers (no in-memory counter): compact before counting store entries.
        if (counter is null)
            queueContext ??= BootstrapQueueContext(system, store, settings);

        int currentCount = counter?.Value ?? CountEntries(store);
        if (currentCount >= settings.MaxTransactions)
            return false;

        if (settings.MinNetworkFee > 0 && tx.NetworkFee < settings.MinNetworkFee)
            return false;

        queueContext ??= BootstrapQueueContext(system, store, settings);
        if (!queueContext.CanAcceptSender(tx.Sender, settings.MaxTransactionsPerSender))
            return false;

        var snapshot = system.StoreView;
        var verifyContext = queueContext.CreateVerificationContext();
        if (DeferredRelayVerification.VerifyForOffer(system.Settings, snapshot, tx, verifyContext) != VerifyResult.Succeed)
            return false;

        store.Put(key, tx.ToArray());
        counter?.Increment();
        queueContext.OnQueued(tx);
        return true;
    }

    /// <summary>
    /// Looks up a queued transaction by its hash and returns the raw serialized blob, or <c>null</c> if not queued.
    /// </summary>
    /// <param name="store">The plugin's local store.</param>
    /// <param name="hash">The transaction hash (UInt256).</param>
    /// <returns>The serialized <see cref="Transaction"/> bytes if queued; otherwise <c>null</c>.</returns>
    public static byte[]? TryGetPendingTx(IStore store, UInt256 hash)
    {
        byte[] key = hash.GetSpan().ToArray();
        return store.TryGet(key, out byte[]? value) ? value : null;
    }

    /// <summary>
    /// Indicates whether the deferred relay queue should be scanned after the given block is persisted.
    /// Returns <c>false</c> when the plugin is disabled, at genesis, or when <see cref="DeferredRelaySettings.CheckFrequency"/>
    /// is zero (which would otherwise throw <see cref="DivideByZeroException"/> on the modulo operation).
    /// </summary>
    public static bool ShouldProcessPersist(Block block, DeferredRelaySettings settings)
    {
        if (!settings.Enabled)
            return false;
        if (settings.CheckFrequency == 0)
            return false;
        if (block.Index == 0)
            return false;
        return block.Index % settings.CheckFrequency == 0;
    }

    /// <summary>
    /// Scans the local queue, removes expired, corrupt, or state-dependent-invalid entries, and relays txs
    /// that entered the allowed window.
    /// The number of relay attempts is capped at <see cref="DeferredRelaySettings.MaxRelayPerCycle"/> so a single
    /// drain pass cannot monopolize the Blockchain actor mailbox when the queue is near capacity; the unrelayed
    /// remainder stays in the store and is picked up by the next <see cref="ShouldProcessPersist"/>-eligible block.
    /// </summary>
    /// <param name="counter">
    /// Optional in-memory entry counter; decremented on every successful <c>Delete</c> so that
    /// <see cref="TryOffer"/> can perform its capacity check without re-scanning the store.
    /// </param>
    public static async Task ProcessQueuedAsync(NeoSystem system, IStore store, DeferredRelaySettings settings, EntryCounter? counter = null, DeferredQueueContext? queueContext = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);
        ClassifyQueueEntries(system, store, settings, out List<byte[]> toRemove, out List<Transaction> survivors, cancellationToken);

        List<Transaction> toRelay = [];
        foreach (Transaction tx in survivors)
        {
            if (tx.ValidUntilBlock <= height + maxInc)
                toRelay.Add(tx);
        }

        queueContext ??= DeferredQueueContext.Bootstrap(store);

        foreach (byte[] key in toRemove)
        {
            if (store.TryGet(key, out byte[]? value))
            {
                try
                {
                    queueContext.OnRemoved(value.AsSerializable<Transaction>());
                }
                catch
                {
                    // Corrupt entries have no queue-context state to reconcile.
                }
            }

            store.Delete(key);
            counter?.Decrement();
        }

        int relayCap = (int)settings.MaxRelayPerCycle;
        int attempts = 0;
        foreach (Transaction tx in toRelay)
        {
            if (attempts >= relayCap) break;
            cancellationToken.ThrowIfCancellationRequested();
            // Count attempts (not just successes): the cap protects the Blockchain actor mailbox,
            // and every Ask -- whether it eventually returns Succeed, AlreadyInPool, or a failure --
            // already occupies one slot in that mailbox.
            attempts++;
            var relayOutcome = await TryRelayAsync(system, tx).ConfigureAwait(false);
            if (relayOutcome is VerifyResult.Succeed or VerifyResult.AlreadyInPool or VerifyResult.AlreadyExists)
            {
                RemoveQueued(store, tx, counter, queueContext);
                continue;
            }

            if (DeferredRelayVerification.IsDefinitiveRelayFailure(relayOutcome))
                RemoveQueued(store, tx, counter, queueContext);
        }
    }

    private static void RemoveQueued(IStore store, Transaction tx, EntryCounter? counter, DeferredQueueContext queueContext)
    {
        store.Delete(tx.Hash.GetSpan().ToArray());
        counter?.Decrement();
        queueContext.OnRemoved(tx);
    }

    private static async Task<VerifyResult> TryRelayAsync(NeoSystem system, Transaction tx)
    {
        try
        {
            var relayResult = await system.Blockchain
                .Ask<Blockchain.RelayResult>(tx, TimeSpan.FromSeconds(30))
                .ConfigureAwait(false);
            return relayResult.Result;
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Debug(ex, "Deferred relay attempt failed for {Hash}", tx.Hash);
            return VerifyResult.Unknown;
        }
    }

    /// <summary>
    /// Drops expired, corrupt, and state-dependent-invalid entries from the local store before
    /// rebuilding in-memory queue state. Called on actor/plugin startup so <see cref="DeferredQueueContext.Bootstrap"/>
    /// does not retain stale txs that would occupy per-sender slots or skew fee aggregation.
    /// The same classification runs on every <see cref="ProcessQueuedAsync"/> cycle while the node is online.
    /// </summary>
    internal static void CompactStore(NeoSystem system, IStore store, DeferredRelaySettings settings)
    {
        if (!settings.Enabled)
            return;

        ClassifyQueueEntries(system, store, settings, out List<byte[]> toRemove, out _);
        foreach (byte[] key in toRemove)
            store.Delete(key);
    }

    internal static bool TryGetCurrentHeight(IStore store, out uint height)
    {
        using var snapshot = new StoreCache(store);
        return TryGetCurrentHeight(snapshot, out height);
    }

    private static bool TryGetCurrentHeight(IReadOnlyStore snapshot, out uint height)
    {
        try
        {
            height = NativeContract.Ledger.CurrentIndex(snapshot);
            return true;
        }
        catch (KeyNotFoundException)
        {
            height = 0;
            return false;
        }
    }

    /// <summary>
    /// Returns store keys to evict and surviving transactions (stable store order, fee-aggregation aware).
    /// </summary>
    private static void ClassifyQueueEntries(
        NeoSystem system,
        IStore store,
        DeferredRelaySettings settings,
        out List<byte[]> toRemove,
        out List<Transaction> survivors,
        CancellationToken cancellationToken = default)
    {
        toRemove = [];
        survivors = [];
        if (!settings.Enabled)
            return;

        var snapshot = system.StoreView;
        if (!TryGetCurrentHeight(snapshot, out uint height))
            return;
        List<(byte[] Key, Transaction Tx)> candidates = [];

        foreach ((byte[] key, byte[] value) in store.Find())
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Schema guard: only 32-byte (UInt256) keys are transaction entries; see CountEntries.
            if (key.Length != UInt256.Length) continue;

            Transaction tx;
            try
            {
                tx = value.AsSerializable<Transaction>();
            }
            catch
            {
                toRemove.Add(key);
                continue;
            }

            if (height >= tx.ValidUntilBlock)
            {
                toRemove.Add(key);
                continue;
            }

            if (settings.MinNetworkFee > 0 && tx.NetworkFee < settings.MinNetworkFee)
            {
                toRemove.Add(key);
                continue;
            }

            candidates.Add((key, tx));
        }

        var verifyContext = new TransactionVerificationContext();
        foreach ((byte[] key, Transaction tx) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DeferredRelayVerification.VerifyForOffer(system.Settings, snapshot, tx, verifyContext) != VerifyResult.Succeed)
                toRemove.Add(key);
            else
            {
                verifyContext.AddTransaction(tx);
                survivors.Add(tx);
            }
        }
    }

    internal static DeferredQueueContext BootstrapQueueContext(NeoSystem system, IStore store, DeferredRelaySettings settings)
    {
        CompactStore(system, store, settings);
        return DeferredQueueContext.Bootstrap(store);
    }

    /// <summary>
    /// Compacts the store and builds the in-memory counter/context pair used by <see cref="DeferredRelayActor"/>.
    /// </summary>
    internal static (EntryCounter Counter, DeferredQueueContext Context) CreateQueueState(
        NeoSystem system, IStore store, DeferredRelaySettings settings)
    {
        var context = BootstrapQueueContext(system, store, settings);
        return (new EntryCounter(CountEntries(store)), context);
    }

    /// <summary>
    /// Performs an O(N) scan of the store to count queued transaction entries.
    /// Used as a fallback when no <see cref="EntryCounter"/> is supplied, and to bootstrap
    /// the actor's counter on startup. Hot-path callers should keep an <see cref="EntryCounter"/>
    /// in memory and maintain it incrementally so each <see cref="TryOffer"/> stays O(1).
    /// </summary>
    internal static int CountEntries(IStore store)
    {
        // Schema: every queued entry is written by TryOffer as
        //   key   = tx.Hash (UInt256, 32 bytes)
        //   value = serialized Transaction (ISerializable blob)
        // The length filter is a schema guard: any key whose length is not UInt256.Length
        // is not a transaction (e.g. future metadata keys with a different prefix/length,
        // or externally-injected entries) and must be ignored, not counted or deserialized.
        int n = 0;
        foreach ((byte[] key, _) in store.Find())
        {
            if (key.Length == UInt256.Length) n++;
        }
        return n;
    }
}

/// <summary>
/// In-memory counter of queued transaction entries maintained by <see cref="DeferredRelayActor"/>.
/// Allows <see cref="DeferredRelayEngine.TryOffer"/> to perform the capacity check in O(1) instead of
/// re-scanning the store on every offer. The counter is mutated via <see cref="Interlocked"/> so it
/// is safe between the (single-threaded) actor mailbox and the background <c>ProcessQueuedAsync</c>
/// continuation that runs on <see cref="TaskScheduler.Default"/>.
/// </summary>
internal sealed class EntryCounter
{
    private int _value;

    public EntryCounter(int initial) => _value = initial;

    public int Value => Volatile.Read(ref _value);

    public void Increment() => Interlocked.Increment(ref _value);

    public void Decrement() => Interlocked.Decrement(ref _value);
}
