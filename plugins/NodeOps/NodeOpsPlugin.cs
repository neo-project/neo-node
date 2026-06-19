// Copyright (C) 2015-2026 The Neo Project.
//
// NodeOpsPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using static System.IO.Path;

namespace Neo.Plugins.NodeOps;

public sealed class NodeOpsPlugin : Plugin
{
    private NodeOpsSettings _settings = NodeOpsSettings.Default;
    private NodeOpsDispatcher? _dispatcher;
    private NeoSystem? _system;
    private bool _subscribedUnhandledExceptions;
    private bool _subscribedUnobservedTaskExceptions;

    public override string Name => "NodeOps";
    public override string Description => "Collects node operations events, reports failures, and publishes status heartbeats to configured monitoring services.";
    public override string ConfigFile => Combine(RootPath, "NodeOps.json");
    protected override UnhandledExceptionPolicy ExceptionPolicy => _settings.ExceptionPolicy;

    protected override void Configure()
    {
        NodeOpsSettings.Load(GetConfiguration());
        _settings = NodeOpsSettings.Default;
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        _system = system;

        if (!_settings.Enabled)
        {
            Logs.RuntimeLogger.Information("NodeOps plugin is disabled");
            return;
        }

        _dispatcher = new NodeOpsDispatcher(_settings, () => _system);
        _dispatcher.Start();
        if (_settings.HasEventSinks)
            Subscribe();
        Logs.RuntimeLogger.Information("NodeOps plugin started");
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
        if (_settings.CaptureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            _subscribedUnhandledExceptions = true;
        }
        if (_settings.CaptureUnobservedTaskExceptions)
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            _subscribedUnobservedTaskExceptions = true;
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
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (_dispatcher is null) return;

        var report = e.ExceptionObject is Exception exception
            ? NodeOpsEvent.FromException("UnhandledException", exception, e.IsTerminating, _settings, _system)
            : NodeOpsEvent.FromObject("UnhandledException", e.ExceptionObject, e.IsTerminating, _settings, _system);

        if (e.IsTerminating)
            _dispatcher.SendNow(report);
        else
            _dispatcher.TryEnqueue(report);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (_dispatcher is null) return;

        var report = NodeOpsEvent.FromException("UnobservedTaskException", e.Exception, false, _settings, _system);
        _dispatcher.TryEnqueue(report);
    }
}
