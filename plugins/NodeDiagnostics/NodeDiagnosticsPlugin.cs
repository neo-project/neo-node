// Copyright (C) 2015-2026 The Neo Project.
//
// NodeDiagnosticsPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using static System.IO.Path;

namespace Neo.Plugins.NodeDiagnostics;

public sealed class NodeDiagnosticsPlugin : Plugin
{
    private NodeDiagnosticsSettings _settings = NodeDiagnosticsSettings.Default;
    private NodeDiagnosticsDispatcher? _dispatcher;
    private NeoSystem? _system;
    private bool _subscribedUnhandledExceptions;
    private bool _subscribedUnobservedTaskExceptions;
    private bool _subscribedBlockLiveness;

    public override string Name => "NodeDiagnostics";
    public override string Description => "Collects node diagnostics events, reports failures, and publishes status heartbeats to configured monitoring services.";
    public override string ConfigFile => Combine(RootPath, "NodeDiagnostics.json");
    protected override UnhandledExceptionPolicy ExceptionPolicy => _settings.ExceptionPolicy;

    protected override void Configure()
    {
        NodeDiagnosticsSettings.Load(GetConfiguration());
        _settings = NodeDiagnosticsSettings.Default;
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        _system = system;

        if (!_settings.Enabled)
        {
            Logs.RuntimeLogger.Information("NodeDiagnostics plugin is disabled");
            return;
        }

        _dispatcher = new NodeDiagnosticsDispatcher(_settings, () => _system);
        _dispatcher.Start();
        Subscribe();
        if (_settings.HasEventSinks)
        {
            if (_settings.SendStartupDiagnosticEvent)
                _dispatcher.TryEnqueue(NodeDiagnosticsEvent.StartupDiagnostic(_settings, _system));
        }
        Logs.RuntimeLogger.Information("NodeDiagnostics plugin started");
    }

    public override void Dispose()
    {
        Unsubscribe();
        _dispatcher?.Dispose();
        _dispatcher = null;
        _system = null;
        base.Dispose();
    }

    private void Subscribe()
    {
        if (_settings.HasEventSinks && _settings.CaptureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            _subscribedUnhandledExceptions = true;
        }
        if (_settings.HasEventSinks && _settings.CaptureUnobservedTaskExceptions)
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            _subscribedUnobservedTaskExceptions = true;
        }
        if (_settings.HasHeartbeatSinks)
        {
            Blockchain.Committed += Blockchain_Committed_Handler;
            _subscribedBlockLiveness = true;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribedUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            _subscribedUnhandledExceptions = false;
        }
        if (_subscribedUnobservedTaskExceptions)
        {
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            _subscribedUnobservedTaskExceptions = false;
        }
        if (_subscribedBlockLiveness)
        {
            Blockchain.Committed -= Blockchain_Committed_Handler;
            _subscribedBlockLiveness = false;
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (_dispatcher is null) return;

        var report = e.ExceptionObject is Exception exception
            ? NodeDiagnosticsEvent.FromException("UnhandledException", exception, e.IsTerminating, _settings, _system)
            : NodeDiagnosticsEvent.FromObject("UnhandledException", e.ExceptionObject, e.IsTerminating, _settings, _system);

        if (e.IsTerminating)
            _dispatcher.SendNow(report);
        else
            _dispatcher.TryEnqueue(report);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (_dispatcher is null) return;

        var report = NodeDiagnosticsEvent.FromException("UnobservedTaskException", e.Exception, false, _settings, _system);
        _dispatcher.TryEnqueue(report);
    }

    private void Blockchain_Committed_Handler(NeoSystem system, Block block) =>
        _dispatcher?.RecordBlockPersisted(block.Index);
}
