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
using System.IO;

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
            root["pendingrelay"] = false;
            root["pendingcheckfrequency"] = 0u;
            root["pendingrelaymaxtransactions"] = 0u;
            root["count"] = 0;
            return root;
        }

        root["enabled"] = true;
        root["pendingrelay"] = true;
        root["pendingcheckfrequency"] = settings.CheckFrequency;
        root["pendingrelaymaxtransactions"] = settings.MaxTransactions;

        var arr = (JArray)root["pending"]!;
        foreach ((byte[] key, byte[] value) in store.Find())
        {
            if (key.Length != UInt256.Length) continue;
            UInt256 hash;
            try { hash = new UInt256(key); } catch { continue; }
            try
            {
                Transaction tx = value.AsSerializable<Transaction>();
                var o = new JObject
                {
                    ["hash"] = hash.ToString(),
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

    public static void OnPersistCompleted(NeoSystem system, IStore store, DeferredRelaySettings settings, Block block)
    {
        if (!settings.Enabled)
            return;
        if (block.Index == 0)
            return;
        if (block.Index % settings.CheckFrequency != 0)
            return;

        ProcessQueued(system, store);
    }

    private static void ProcessQueued(NeoSystem system, IStore store)
    {
        var snapshot = system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);
        List<byte[]> toRemove = [];

        foreach ((byte[] key, byte[] value) in store.Find())
        {
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
            {
                var relayResult = system.Blockchain.Ask<Blockchain.RelayResult>(tx, TimeSpan.FromSeconds(30))
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (relayResult.Result is VerifyResult.Succeed or VerifyResult.AlreadyInPool or VerifyResult.AlreadyExists)
                    toRemove.Add(key);
            }
        }

        foreach (byte[] key in toRemove)
            store.Delete(key);
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
