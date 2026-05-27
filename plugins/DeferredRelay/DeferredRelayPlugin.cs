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
using Neo.Json;
using Neo.Persistence;
using Neo.Plugins.RpcServer;
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
        if (system.Settings.Network != DeferredRelaySettings.Default.Network)
            return;

        _neoSystem = system;
        RpcServerPlugin.RegisterMethods(this, DeferredRelaySettings.Default.Network);

        if (!DeferredRelaySettings.Default.Enabled)
            return;

        var networkId = system.Settings.Network.ToString("X8");
        var pluginPath = string.Format(DeferredRelaySettings.Default.Path, networkId);
        var path = PluginHelper.ApplyUnifiedStoragePath(pluginPath);
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        _store = system.LoadStore(fullPath);
        _actor = system.ActorSystem.ActorOf(
            Props.Create(() => new DeferredRelayActor(system, _store, DeferredRelaySettings.Default)));
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
}
