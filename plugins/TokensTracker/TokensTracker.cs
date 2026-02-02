// Copyright (C) 2015-2026 The Neo Project.
//
// TokensTracker.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.RpcServer;
using Neo.Plugins.Trackers;
using Neo.Plugins.Trackers.NEP_11;
using Neo.Plugins.Trackers.NEP_17;
using static System.IO.Path;

namespace Neo.Plugins;

public class TokensTracker : Plugin
{
    private string _dbPath = "TokensBalanceData";
    private bool _shouldTrackHistory;
    private uint _maxResults;
    private uint _network;
    private string[] _enabledTrackers = [];
    private IStore? _db;
    private UnhandledExceptionPolicy _exceptionPolicy;
    private NeoSystem? neoSystem;
    private readonly List<TrackerBase> trackers = new();
    protected override UnhandledExceptionPolicy ExceptionPolicy => _exceptionPolicy;

    public override string Description => "Enquiries balances and transaction history of accounts through RPC";

    public override string ConfigFile => Combine(RootPath, "TokensTracker.json");

    public TokensTracker()
    {
        Blockchain.Committing += Blockchain_Committing_Handler;
        Blockchain.Committed += Blockchain_Committed_Handler;
    }

    public override void Dispose()
    {
        Blockchain.Committing -= Blockchain_Committing_Handler;
        Blockchain.Committed -= Blockchain_Committed_Handler;
        base.Dispose();
    }

    protected override void Configure()
    {
        IConfigurationSection config = GetConfiguration();
        _dbPath = config.GetValue("DBPath", "TokensBalanceData");
        _shouldTrackHistory = config.GetValue("TrackHistory", true);
        _maxResults = config.GetValue("MaxResults", 1000u);
        _network = config.GetValue("Network", 860833102u);
        _enabledTrackers = config.GetSection("EnabledTrackers")
            .GetChildren()
            .Select(p => p.Value)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray()!;
        var policyString = config.GetValue(nameof(UnhandledExceptionPolicy), nameof(UnhandledExceptionPolicy.StopNode));
        if (Enum.TryParse(policyString, true, out UnhandledExceptionPolicy policy))
        {
            _exceptionPolicy = policy;
        }
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (system.Settings.Network != _network) return;
        neoSystem = system;
        // Get path from plugin's own configuration, optionally combined with base path from config.json
        var networkId = neoSystem.Settings.Network.ToString("X8");
        // TokensTracker default path format: "TokensBalanceData" or "TokensBalanceData_{0}"
        string defaultPath = _dbPath.Contains("{0}") ? _dbPath : $"{_dbPath}_{{0}}";
        var pluginPath = string.Format(defaultPath, networkId);
        var path = PluginHelper.ApplyUnifiedStoragePath(pluginPath);
        var fullPath = GetFullPath(path);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(fullPath));
        _db = neoSystem.LoadStore(fullPath);
        if (_enabledTrackers.Contains("NEP-11"))
            trackers.Add(new Nep11Tracker(_db, _maxResults, _shouldTrackHistory, neoSystem));
        if (_enabledTrackers.Contains("NEP-17"))
            trackers.Add(new Nep17Tracker(_db, _maxResults, _shouldTrackHistory, neoSystem));
        foreach (TrackerBase tracker in trackers)
            RpcServerPlugin.RegisterMethods(tracker, _network);
    }

    private void ResetBatch()
    {
        foreach (var tracker in trackers)
        {
            tracker.ResetBatch();
        }
    }

    void Blockchain_Committing_Handler(NeoSystem system, Block block, DataCache snapshot,
        IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        if (system.Settings.Network != _network) return;
        // Start freshly with a new DBCache for each block.
        ResetBatch();
        foreach (var tracker in trackers)
        {
            tracker.OnPersist(system, block, snapshot, applicationExecutedList);
        }
    }

    void Blockchain_Committed_Handler(NeoSystem system, Block block)
    {
        if (system.Settings.Network != _network) return;
        foreach (var tracker in trackers)
        {
            tracker.Commit();
        }
    }
}
