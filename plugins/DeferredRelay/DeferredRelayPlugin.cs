// Copyright (C) 2015-2026 The Neo Project.
//
// DeferredRelayPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.RpcServer;
using Neo.SmartContract.Native;
using static System.IO.Path;

namespace Neo.Plugins.DeferredRelay;

/// <summary>
/// Locally queues and later relays transactions rejected with <see cref="Neo.Ledger.VerifyResult.NotYetValid"/>
/// (ValidUntilBlock beyond the allowed forward window at submission time).
/// </summary>
public class DeferredRelayPlugin : Plugin
{
    private IStore? _store;
    private NeoSystem? _neoSystem;
    private IActorRef? _actor;

    public override string Name => "DeferredRelay";
    public override string Description => "Queues NotYetValid transactions locally and relays them when they enter the allowed ValidUntil window.";
    public override string ConfigFile => Combine(RootPath, "DeferredRelay.json");
    protected override UnhandledExceptionPolicy ExceptionPolicy => DeferredRelaySettings.Default.ExceptionPolicy;

    protected override void Configure() =>
        DeferredRelaySettings.Load(GetConfiguration());

    protected override void OnSystemLoaded(NeoSystem system)
    {
        _neoSystem = system;
        RpcServerPlugin.RegisterMethods(this, system.Settings.Network);

        if (!DeferredRelaySettings.Default.Enabled)
            return;

        var networkId = system.Settings.Network.ToString("X8");
        var pluginPath = string.Format(DeferredRelaySettings.Default.Path, networkId);
        var path = PluginHelper.ApplyUnifiedStoragePath(pluginPath);
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        _store = system.LoadStore(fullPath);
        var queueState = DeferredRelayEngine.CreateQueueState(system, _store, DeferredRelaySettings.Default);
        _actor = system.ActorSystem.ActorOf(Props.Create(() =>
            new DeferredRelayActor(system, _store, DeferredRelaySettings.Default, queueState.Counter, queueState.Context)));
    }

    public override void Dispose()
    {
        if (_neoSystem is not null && _actor is not null)
        {
            _neoSystem.EnsureStopped(_actor);
            _actor = null;
        }
        _store?.Dispose();
        _store = null;
        _neoSystem = null;
        base.Dispose();
    }

    /// <summary>
    /// Returns locally queued NotYetValid transactions and plugin settings.
    /// </summary>
    [RpcMethod]
    public JToken GetPendingValidUntilRelay()
    {
        if (_neoSystem is null)
            throw new InvalidOperationException("NeoSystem is not loaded.");
        return DeferredRelayEngine.GetPendingState(_neoSystem, _store, DeferredRelaySettings.Default);
    }

    [ConsoleCommand("list pending", Category = "DeferredRelay Commands")]
    internal void OnListPendingCommand()
    {
        if (_neoSystem is null)
        {
            ConsoleHelper.Error("NeoSystem is not loaded.");
            return;
        }
        JObject json = DeferredRelayEngine.GetPendingState(_neoSystem, _store, DeferredRelaySettings.Default);
        Console.WriteLine(json.ToString(true));
    }

    /// <summary>
    /// Gets a locally queued NotYetValid transaction by its hash.
    /// <para>Request format:</para>
    /// <code>
    /// {"jsonrpc": "2.0", "id": 1, "method": "getrawpendingtx", "params": ["The tx hash", true/*verbose, optional*/]}
    /// </code>
    /// <para>Response format:</para>
    /// If verbose is false, returns the Base64-encoded serialized transaction.
    /// If verbose is true, returns a JSON object with the same fields as <c>getrawtransaction</c> (verbose),
    /// minus on-chain-only fields (<c>blockhash</c>/<c>confirmations</c>/<c>blocktime</c>), plus
    /// <c>blocksuntildeadline</c> when the current height is less than the transaction's <c>ValidUntilBlock</c>.
    /// </summary>
    /// <param name="hash">The transaction hash.</param>
    /// <param name="verbose">Optional, defaults to false.</param>
    /// <returns>The transaction as Base64 (verbose=false) or as a JSON object (verbose=true).</returns>
    /// <exception cref="RpcException">
    /// <see cref="RpcError.UnknownTransaction"/> when the plugin is disabled, the store is unavailable,
    /// the hash is not queued, or the stored entry is corrupt.
    /// </exception>
    [RpcMethod]
    public JToken GetRawPendingTx(UInt256 hash, bool verbose = false)
    {
        if (_neoSystem is null)
            throw new InvalidOperationException("NeoSystem is not loaded.");
        hash.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'hash'"));

        if (_store is null || !DeferredRelaySettings.Default.Enabled)
            throw new RpcException(RpcError.UnknownTransaction.WithData(hash.ToString()));

        byte[]? raw = DeferredRelayEngine.TryGetPendingTx(_store, hash);
        raw.NotNull_Or(RpcError.UnknownTransaction.WithData(hash.ToString()));

        if (!verbose) return Convert.ToBase64String(raw);

        Transaction tx;
        try
        {
            tx = raw.AsSerializable<Transaction>();
        }
        catch
        {
            throw new RpcException(RpcError.UnknownTransaction.WithData($"Corrupt entry for {hash}."));
        }

        var json = tx.ToJson(_neoSystem.Settings);
        uint height = NativeContract.Ledger.CurrentIndex(_neoSystem.StoreView);
        if (height < tx.ValidUntilBlock)
            json["blocksuntildeadline"] = tx.ValidUntilBlock - height;
        return json;
    }
}
