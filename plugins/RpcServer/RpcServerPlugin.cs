// Copyright (C) 2015-2026 The Neo Project.
//
// RpcServerPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RpcServer;

public class RpcServerPlugin : Plugin
{
    public override string Name => "RpcServer";
    public override string Description => "Enables RPC for the node";

    private RpcServerSettings? settings;
    private static readonly Dictionary<uint, RpcServer> servers = new();
    private static readonly Dictionary<uint, List<object>> handlers = new();
    private static int serverIndex = 0;

    public override string ConfigFile => System.IO.Path.Combine(RootPath, "RpcServer.json");

    protected override UnhandledExceptionPolicy ExceptionPolicy => settings!.ExceptionPolicy;

    protected override void Configure()
    {
        settings = new RpcServerSettings(GetConfiguration());
        foreach (var (network, server) in servers)
        {
            var idx = 0;
            foreach (var s in settings.Servers)
            {
                if (idx == serverIndex)
                {
                    server.UpdateSettings(s);
                    break;
                }
                idx++;
            }
        }
    }

    public override void Dispose()
    {
        foreach (var (_, server) in servers)
            server.Dispose();
        base.Dispose();
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (settings is null) throw new InvalidOperationException("RpcServer settings are not loaded");

        if (serverIndex >= settings.Servers.Count)
        {
            Logs.RuntimeLogger.Warning("No RpcServer configuration available for this system instance");
            return;
        }

        var s = settings.Servers[serverIndex];

        if (s.EnableCors && string.IsNullOrEmpty(s.RpcUser) == false && s.AllowOrigins.Length == 0)
        {
            Logs.RuntimeLogger.Warning("RcpServer: CORS is misconfigured!");
            Logs.RuntimeLogger.Warning("You have {EnableCors} and Basic Authentication enabled but {AllowOrigins} is empty in config.json for RcpServer. " +
                "You must add url origins to the list to have CORS work from browser with basic authentication enabled. " +
                "Example: \"AllowOrigins\": [\"http://{BindAddress}:{Port}\"]", s.EnableCors, s.AllowOrigins, s.BindAddress, s.Port);
        }
        Logs.RuntimeLogger.Information("RpcServer started for network {Network}", system.Settings.Network);

        var rpcRpcServer = new RpcServer(system, s);
        if (handlers.Remove(system.Settings.Network, out var list))
        {
            foreach (var handler in list)
            {
                rpcRpcServer.RegisterMethods(handler);
            }
        }

        rpcRpcServer.StartRpcServer();
        servers.TryAdd(system.Settings.Network, rpcRpcServer);
        serverIndex++;
    }

    public static void RegisterMethods(object handler, uint network)
    {
        if (servers.TryGetValue(network, out var server))
        {
            server.RegisterMethods(handler);
            return;
        }
        if (!handlers.TryGetValue(network, out var list))
        {
            list = new List<object>();
            handlers.Add(network, list);
        }
        list.Add(handler);
    }
}
