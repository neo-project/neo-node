// Copyright (C) 2015-2026 The Neo Project.
//
// PendingValidUntilRelay.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System.IO;

namespace Neo.CLI;

/// <summary>
/// Configuration for locally queueing transactions whose <see cref="Transaction.ValidUntilBlock"/> is beyond
/// the allowed forward window at the current height (verification returns <see cref="VerifyResult.Expired"/>).
/// Values come from the same <c>config.json</c> P2P section as RpcServer (see <see cref="Neo.P2PSettings"/>).
/// </summary>
public sealed record PendingValidUntilRelayConfiguration(bool PendingRelay, uint PendingCheckFrequency);

/// <summary>
/// Holds a dedicated <see cref="IStore"/> (e.g. LevelDB directory) for deferred relay of such transactions.
/// </summary>
public sealed class PendingValidUntilRelayHost : IDisposable
{
    private readonly IStore _store;

    /// <summary>Configuration bound to this host.</summary>
    public PendingValidUntilRelayConfiguration Configuration { get; }

    /// <summary>The underlying key-value store.</summary>
    public IStore Store => _store;

    /// <summary>Creates a host over the given store.</summary>
    public PendingValidUntilRelayHost(IStore store, PendingValidUntilRelayConfiguration configuration)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Configuration = configuration;
    }

    /// <inheritdoc/>
    public void Dispose() => _store.Dispose();
}

/// <summary>
/// CLI-side local persistence and deferred relay for transactions rejected as <see cref="VerifyResult.Expired"/> only because
/// <see cref="Transaction.ValidUntilBlock"/> exceeds <c>height + MaxValidUntilBlockIncrement</c> at submission time.
/// RpcServer duplicates the offer path in <c>PendingValidUntilRelayRpcBridge</c>; keep behavior aligned.
/// </summary>
public static class PendingValidUntilRelay
{
    /// <summary>Short CLI line when a far-ahead ValidUntil transaction is stored for local deferred relay.</summary>
    public const string CliQueuedLocallyHint = "ValidUntil too far ahead: queued locally (not broadcast).";

    /// <summary>
    /// Builds a snapshot of local pending ValidUntil-deferred transactions and related settings (RPC/CLI listing).
    /// </summary>
    public static JObject GetPendingState(NeoSystem system, PendingValidUntilRelayHost? host)
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

        if (host is null)
        {
            root["enabled"] = false;
            root["pendingrelay"] = false;
            root["pendingcheckfrequency"] = 0u;
            root["count"] = 0;
            return root;
        }

        PendingValidUntilRelayConfiguration cfg = host.Configuration;
        bool enabled = cfg.PendingRelay && cfg.PendingCheckFrequency != 0;
        root["enabled"] = enabled;
        root["pendingrelay"] = cfg.PendingRelay;
        root["pendingcheckfrequency"] = cfg.PendingCheckFrequency;

        var arr = (JArray)root["pending"]!;
        if (!enabled)
        {
            root["count"] = 0;
            return root;
        }

        foreach ((byte[] key, byte[] value) in host.Store.Find())
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
    /// Returns <see langword="true"/> when the transaction is not yet expired by height but is beyond the allowed forward window.
    /// </summary>
    public static bool IsFarFutureValidityWindow(Transaction tx, NeoSystem system)
    {
        var snapshot = system.StoreView;
        if (!NativeContract.Ledger.ContainsBlock(snapshot, system.GenesisBlock.Hash))
            return false;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);
        return tx.ValidUntilBlock > height + maxInc;
    }

    /// <summary>
    /// Persists the transaction in the local pending store when configuration and relay result allow it.
    /// CLI and RpcServer share the same <see cref="PendingValidUntilRelayHost.Store"/> via <see cref="NeoSystem"/>; if this hash is already in that store, no second write is performed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the transaction was newly written or was already present in the local pending store;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryOffer(NeoSystem system, PendingValidUntilRelayHost? host, Transaction tx, VerifyResult relayResult)
    {
        if (host is null || !host.Configuration.PendingRelay || host.Configuration.PendingCheckFrequency == 0)
            return false;
        if (relayResult != VerifyResult.Expired)
            return false;
        if (!IsFarFutureValidityWindow(tx, system))
            return false;
        UInt256 hash = tx.Hash;
        if (system.ContainsTransaction(hash) != ContainsTransactionType.NotExist)
            return false;

        byte[] key = hash.GetSpan().ToArray();
        if (host.Store.Contains(key))
            return true;

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            ((ISerializable)tx).Serialize(writer);
        }
        byte[] payload = ms.ToArray();
        host.Store.Put(key, payload);
        return true;
    }

    /// <summary>
    /// Invoked after a block is persisted: periodically scans the pending store, relays eligible transactions, and drops expired ones.
    /// </summary>
    public static void OnPersistCompleted(NeoSystem system, PendingValidUntilRelayHost host, Block block)
    {
        if (!host.Configuration.PendingRelay || host.Configuration.PendingCheckFrequency == 0)
            return;
        if (block.Index == 0)
            return;
        if (block.Index % host.Configuration.PendingCheckFrequency != 0)
            return;

        ProcessQueued(system, host);
    }

    private static void ProcessQueued(NeoSystem system, PendingValidUntilRelayHost host)
    {
        var snapshot = system.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);
        List<byte[]> toRemove = [];

        foreach ((byte[] key, byte[] value) in host.Store.Find())
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
            host.Store.Delete(key);
    }
}
