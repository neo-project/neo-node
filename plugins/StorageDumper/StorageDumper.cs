// Copyright (C) 2015-2026 The Neo Project.
//
// StorageDumper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Extensions;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;

namespace Neo.Plugins.StorageDumper;

public class StorageDumper : Plugin
{
    private NeoSystem? _system;

    private StreamWriter? _writer;
    /// <summary>
    /// _currentBlock stores the last cached item
    /// </summary>
    private JObject? _currentBlock;
    private string? _lastCreateDirectory;
    protected override UnhandledExceptionPolicy ExceptionPolicy => StorageSettings.Default?.ExceptionPolicy ?? UnhandledExceptionPolicy.Ignore;

    public override string Description => "Exports Neo-CLI status data";

    public override string ConfigFile => System.IO.Path.Combine(RootPath, "StorageDumper.json");

    public StorageDumper()
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
        StorageSettings.Load(GetConfiguration());
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        _system = system;
    }

    /// <summary>
    /// Process "dump contract-storage" command
    /// </summary>
    [ConsoleCommand("dump contract-storage", Category = "Storage", Description = "You can specify the contract script hash or use null to get the corresponding information from the storage")]
    internal void OnDumpStorage(UInt160? contractHash = null)
    {
        if (_system == null) throw new InvalidOperationException("system doesn't exists");
        var networkId = _system.Settings.Network.ToString("X8");
        // dump file path: plugin default naming, optionally combined with base path from config.json
        var pluginPath = string.Format("dump_{0}.json", networkId);
        var path = PluginHelper.ApplyUnifiedStoragePath(pluginPath);
        byte[]? prefix = null;
        if (contractHash is not null)
        {
            var contract = NativeContract.ContractManagement.GetContract(_system.StoreView, contractHash);
            if (contract is null) throw new InvalidOperationException("contract not found");
            prefix = BitConverter.GetBytes(contract.Id);
        }
        var states = _system.StoreView.Find(prefix);
        var array = new JArray(states.Where(p => !StorageSettings.Default!.Exclude.Contains(p.Key.Id)).Select(p => new JObject
        {
            ["key"] = Convert.ToBase64String(p.Key.ToArray()),
            ["value"] = Convert.ToBase64String(p.Value.ToArray())
        }));
        File.WriteAllText(path, array.ToString());
        ConsoleHelper.Info("States",
            $"({array.Count})",
            " have been dumped into file ",
            $"{path}");
    }

    void Blockchain_Committing_Handler(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        InitFileWriter(system.Settings.Network, block);

        if (block.Index >= StorageSettings.Default!.HeightToBegin)
        {
            var stateChangeArray = new JArray();

            foreach (var trackable in snapshot.GetChangeSet())
            {
                if (StorageSettings.Default.Exclude.Contains(trackable.Key.Id))
                    continue;
                var state = new JObject();
                switch (trackable.Value.State)
                {
                    case TrackState.Added:
                        state["id"] = trackable.Key.Id;
                        state["state"] = "Added";
                        state["key"] = Convert.ToBase64String(trackable.Key.ToArray());
                        state["value"] = Convert.ToBase64String(trackable.Value.Item.ToArray());
                        break;
                    case TrackState.Changed:
                        state["id"] = trackable.Key.Id;
                        state["state"] = "Changed";
                        state["key"] = Convert.ToBase64String(trackable.Key.ToArray());
                        state["value"] = Convert.ToBase64String(trackable.Value.Item.ToArray());
                        break;
                    case TrackState.Deleted:
                        state["id"] = trackable.Key.Id;
                        state["state"] = "Deleted";
                        state["key"] = Convert.ToBase64String(trackable.Key.ToArray());
                        break;
                }
                stateChangeArray.Add(state);
            }

            _currentBlock = new JObject()
            {
                ["block"] = block.Index,
                ["size"] = stateChangeArray.Count,
                ["storage"] = stateChangeArray
            };
        }
    }


    void Blockchain_Committed_Handler(NeoSystem system, Block block)
    {
        OnCommitStorage(system.Settings.Network);
    }

    void OnCommitStorage(uint network)
    {
        if (_currentBlock != null && _writer != null)
        {
            _writer.WriteLine(_currentBlock.ToString());
            _writer.Flush();
        }
    }

    private void InitFileWriter(uint network, Block block)
    {
        if (_writer == null
            || block.Index % StorageSettings.Default!.BlockCacheSize == 0)
        {
            string path = GetOrCreateDirectory(network, block.Index);
            var filepart = (block.Index / StorageSettings.Default!.BlockCacheSize) * StorageSettings.Default.BlockCacheSize;
            path = $"{path}/dump-block-{filepart}.dump";
            if (_writer != null)
            {
                _writer.Dispose();
            }
            _writer = new StreamWriter(new FileStream(path, FileMode.Append));
        }
    }

    private string GetOrCreateDirectory(uint network, uint blockIndex)
    {
        string dirPathWithBlock = GetDirectoryPath(network, blockIndex);
        if (_lastCreateDirectory != dirPathWithBlock)
        {
            Directory.CreateDirectory(dirPathWithBlock);
            _lastCreateDirectory = dirPathWithBlock;
        }
        return dirPathWithBlock;
    }

    private static string GetDirectoryPath(uint network, uint blockIndex)
    {
        uint folder = (blockIndex / StorageSettings.Default!.StoragePerFolder) * StorageSettings.Default.StoragePerFolder;
        var networkId = network.ToString("X8");
        // Base folder for StorageDumper, optionally combined with base path from config.json
        var pluginFolder = string.Format("StorageDumper_{0}", networkId);
        var baseFolder = PluginHelper.ApplyUnifiedStoragePath(pluginFolder);
        return System.IO.Path.Combine(baseFolder, $"BlockStorage_{folder}");
    }

}
