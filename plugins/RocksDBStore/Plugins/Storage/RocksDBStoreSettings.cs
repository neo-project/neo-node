// Copyright (C) 2015-2026 The Neo Project.
//
// RocksDBStoreSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.Storage;

/// <summary>
/// The RocksDB options used by <see cref="RocksDBStore"/>, read from RocksDBStore.json.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is replaced by <see cref="Load"/> when the plugin is constructed, which
/// happens before any store is opened, so the values in effect when a database is opened stay
/// fixed for the lifetime of that database.
/// </remarks>
public sealed class RocksDBStoreSettings
{
    public static RocksDBStoreSettings Default { get; private set; } = new();

    /// <summary>
    /// Size of a single in-memory write buffer. The RocksDB default (4 MiB) is small enough that
    /// a busy consensus node flushes to L0 constantly and then stalls waiting for compaction.
    /// </summary>
    public ulong WriteBufferSize { get; }

    /// <summary>
    /// Number of write buffers kept in memory before writes are throttled.
    /// </summary>
    public int MaxWriteBufferNumber { get; }

    /// <summary>
    /// Minimum number of write buffers that are merged before being flushed to an L0 file.
    /// </summary>
    public int MinWriteBufferNumberToMerge { get; }

    /// <summary>
    /// Upper bound for the total size of all WAL files, so the WAL cannot grow without limit.
    /// Keep it above <see cref="WriteBufferSize"/> times <see cref="MaxWriteBufferNumber"/>:
    /// when the WAL reaches this bound RocksDB flushes memtables early to discard old files,
    /// which is the stall pattern the larger write buffer is meant to remove.
    /// </summary>
    public ulong MaxTotalWalSize { get; }

    /// <summary>
    /// Uses dynamic level sizes, which lowers compaction write amplification.
    /// </summary>
    public bool EnableDynamicLevelBytes { get; }

    /// <summary>
    /// Number of background threads used for compaction.
    /// </summary>
    public int MaxBackgroundCompactions { get; }

    /// <summary>
    /// Number of background threads used for flushing memtables to disk.
    /// </summary>
    public int MaxBackgroundFlushes { get; }

    /// <summary>
    /// Size of a data block in the table file. 16 KiB instead of the 4 KiB default reduces the
    /// number of index entries and read operations per lookup.
    /// </summary>
    public ulong BlockSize { get; }

    /// <summary>
    /// Keeps index and filter blocks in the block cache instead of the table reader's own memory,
    /// so they can be evicted under memory pressure instead of growing unbounded.
    /// </summary>
    public bool CacheIndexAndFilterBlocks { get; }

    /// <summary>
    /// Capacity of the shared LRU block cache. RocksDB uses an 8 MiB default which is far too
    /// small for a node that reads recent blocks and contract storage repeatedly; a value of 0
    /// leaves RocksDB's default in place. Raise it together with the write buffers only when the
    /// host has memory to spare, because this is held for the lifetime of the database.
    /// </summary>
    public ulong BlockCacheSize { get; }

    public RocksDBStoreSettings()
    {
        WriteBufferSize = 64UL << 20;
        MaxWriteBufferNumber = 4;
        MinWriteBufferNumberToMerge = 2;
        MaxTotalWalSize = 512UL << 20;
        EnableDynamicLevelBytes = true;
        MaxBackgroundCompactions = 3;
        MaxBackgroundFlushes = 1;
        BlockSize = 16UL * 1024;
        CacheIndexAndFilterBlocks = true;
        BlockCacheSize = 128UL << 20;
    }

    public RocksDBStoreSettings(IConfigurationSection section)
    {
        WriteBufferSize = section.GetValue("WriteBufferSize", 64UL << 20);
        MaxWriteBufferNumber = section.GetValue("MaxWriteBufferNumber", 4);
        MinWriteBufferNumberToMerge = section.GetValue("MinWriteBufferNumberToMerge", 2);
        MaxTotalWalSize = section.GetValue("MaxTotalWalSize", 512UL << 20);
        EnableDynamicLevelBytes = section.GetValue("EnableDynamicLevelBytes", true);
        MaxBackgroundCompactions = section.GetValue("MaxBackgroundCompactions", 3);
        MaxBackgroundFlushes = section.GetValue("MaxBackgroundFlushes", 1);
        BlockSize = section.GetValue("BlockSize", 16UL * 1024);
        CacheIndexAndFilterBlocks = section.GetValue("CacheIndexAndFilterBlocks", true);
        BlockCacheSize = section.GetValue("BlockCacheSize", 128UL << 20);
    }

    public static void Load(IConfigurationSection section)
    {
        Default = new RocksDBStoreSettings(section);
    }
}
