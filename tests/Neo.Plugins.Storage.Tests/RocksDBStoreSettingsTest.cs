// Copyright (C) 2015-2026 The Neo Project.
//
// RocksDBStoreSettingsTest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using RocksDbSharp;

namespace Neo.Plugins.Storage.Tests;

[TestClass]
public class RocksDBStoreSettingsTest
{
    [TestMethod]
    public void DefaultsEnableTunedOptions()
    {
        var settings = new RocksDBStoreSettings(EmptySection());

        Assert.AreEqual(64UL << 20, settings.WriteBufferSize);
        Assert.AreEqual(4, settings.MaxWriteBufferNumber);
        Assert.AreEqual(2, settings.MinWriteBufferNumberToMerge);
        Assert.AreEqual(512UL << 20, settings.MaxTotalWalSize);
        Assert.IsTrue(settings.EnableDynamicLevelBytes);
        Assert.AreEqual(3, settings.MaxBackgroundCompactions);
        Assert.AreEqual(1, settings.MaxBackgroundFlushes);
        Assert.AreEqual(16UL * 1024, settings.BlockSize);
        Assert.IsTrue(settings.CacheIndexAndFilterBlocks);
        Assert.AreEqual(128UL << 20, settings.BlockCacheSize);

        // A WAL bound below the memtable budget makes RocksDB flush memtables early to discard
        // old WAL files, which reintroduces the stalls the larger write buffer removes.
        Assert.IsTrue(settings.MaxTotalWalSize >= settings.WriteBufferSize * (ulong)settings.MaxWriteBufferNumber);
    }

    [TestMethod]
    public void ConfigurationOverridesEveryTuningValue()
    {
        var values = new Dictionary<string, string>
        {
            ["PluginConfiguration:WriteBufferSize"] = "4194304",
            ["PluginConfiguration:MaxWriteBufferNumber"] = "3",
            ["PluginConfiguration:MinWriteBufferNumberToMerge"] = "1",
            ["PluginConfiguration:MaxTotalWalSize"] = "0",
            ["PluginConfiguration:EnableDynamicLevelBytes"] = "false",
            ["PluginConfiguration:MaxBackgroundCompactions"] = "2",
            ["PluginConfiguration:MaxBackgroundFlushes"] = "2",
            ["PluginConfiguration:BlockSize"] = "4096",
            ["PluginConfiguration:CacheIndexAndFilterBlocks"] = "false",
            ["PluginConfiguration:BlockCacheSize"] = "1073741824"
        };
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection("PluginConfiguration");
        var settings = new RocksDBStoreSettings(section);

        Assert.AreEqual(4UL << 20, settings.WriteBufferSize);
        Assert.AreEqual(3, settings.MaxWriteBufferNumber);
        Assert.AreEqual(1, settings.MinWriteBufferNumberToMerge);
        Assert.AreEqual(0UL, settings.MaxTotalWalSize);
        Assert.IsFalse(settings.EnableDynamicLevelBytes);
        Assert.AreEqual(2, settings.MaxBackgroundCompactions);
        Assert.AreEqual(2, settings.MaxBackgroundFlushes);
        Assert.AreEqual(4UL * 1024, settings.BlockSize);
        Assert.IsFalse(settings.CacheIndexAndFilterBlocks);
        Assert.AreEqual(1UL << 30, settings.BlockCacheSize);

        var options = Options.CreateDbOptions(settings);
        Assert.AreEqual(settings.WriteBufferSize,
            Native.Instance.rocksdb_options_get_write_buffer_size(options.Handle));
        Assert.AreEqual(settings.MaxWriteBufferNumber,
            Native.Instance.rocksdb_options_get_max_write_buffer_number(options.Handle));
        Assert.AreEqual(settings.MinWriteBufferNumberToMerge,
            Native.Instance.rocksdb_options_get_min_write_buffer_number_to_merge(options.Handle));
        Assert.AreEqual(settings.MaxTotalWalSize,
            Native.Instance.rocksdb_options_get_max_total_wal_size(options.Handle));
        Assert.AreEqual((byte)0,
            Native.Instance.rocksdb_options_get_level_compaction_dynamic_level_bytes(options.Handle));
        Assert.AreEqual(settings.MaxBackgroundCompactions,
            Native.Instance.rocksdb_options_get_max_background_compactions(options.Handle));
        Assert.AreEqual(settings.MaxBackgroundFlushes,
            Native.Instance.rocksdb_options_get_max_background_flushes(options.Handle));
        GC.KeepAlive(options);
    }

    [TestMethod]
    public void BlockCacheCanBeDisabled()
    {
        var values = new Dictionary<string, string>
        {
            ["PluginConfiguration:BlockCacheSize"] = "0"
        };
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection("PluginConfiguration");
        var settings = new RocksDBStoreSettings(section);

        Assert.AreEqual(0UL, settings.BlockCacheSize);

        // A zero size means "leave the RocksDB default cache in place", so building the
        // options must not fail and must not attach a shared cache.
        var options = Options.CreateDbOptions(settings);
        GC.KeepAlive(options);
    }

    private static IConfigurationSection EmptySection()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build()
            .GetSection("PluginConfiguration");
    }
}
