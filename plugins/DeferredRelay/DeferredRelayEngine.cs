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
        bool ledgerReady = NativeContract.Ledger.ContainsBlock(snapshot, system.GenesisBlock.Hash);
        uint height = ledgerReady ? NativeContract.Ledger.CurrentIndex(snapshot) : 0;
        uint maxInc = ledgerReady ? snapshot.GetMaxValidUntilBlockIncrement(system.Settings) : 0;

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
                if (ledgerReady && height < tx.ValidUntilBlock)
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
    public static bool TryOffer(NeoSystem system, IStore store, DeferredRelaySettings settings, Transaction tx, VerifyResult relayResult)
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

        if (CountEntries(store) >= settings.MaxTransactions)
            return false;

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            ((ISerializable)tx).Serialize(writer);
        }
        store.Put(key, ms.ToArray());
        return true;
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
    /// Scans the local queue, removes expired or corrupt entries, and relays txs that entered the allowed window.
    /// </summary>
    public static async Task ProcessQueuedAsync(NeoSystem system, IStore store, CancellationToken cancellationToken = default)
    {
        var snapshot = system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);
        List<byte[]> toRemove = [];
        List<Transaction> toRelay = [];

        foreach ((byte[] key, byte[] value) in store.Find())
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            if (height < tx.ValidUntilBlock && tx.ValidUntilBlock <= height + maxInc)
                toRelay.Add(tx);
        }

        foreach (byte[] key in toRemove)
            store.Delete(key);

        foreach (Transaction tx in toRelay)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await TryRelayAsync(system, tx).ConfigureAwait(false))
                continue;
            store.Delete(tx.Hash.GetSpan().ToArray());
        }
    }

    private static async Task<bool> TryRelayAsync(NeoSystem system, Transaction tx)
    {
        try
        {
            var relayResult = await system.Blockchain
                .Ask<Blockchain.RelayResult>(tx, TimeSpan.FromSeconds(30))
                .ConfigureAwait(false);
            return relayResult.Result is VerifyResult.Succeed or VerifyResult.AlreadyInPool or VerifyResult.AlreadyExists;
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Debug(ex, "Deferred relay attempt failed for {Hash}", tx.Hash);
            return false;
        }
    }

    private static int CountEntries(IStore store)
    {
        int n = 0;
        foreach ((byte[] key, _) in store.Find())
        {
            if (key.Length == UInt256.Length) n++;
        }
        return n;
    }
}
