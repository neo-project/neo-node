// Copyright (C) 2015-2026 The Neo Project.
//
// Options.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using RocksDbSharp;

namespace Neo.Plugins.Storage;

public static class Options
{
    private static readonly Lock s_blockCacheLock = new();
    private static readonly Dictionary<ulong, Cache> s_blockCaches = [];

    public static readonly ReadOptions ReadDefault = new();
    public static readonly WriteOptions WriteDefault = new();
    public static readonly WriteOptions WriteDefaultSync = new WriteOptions().SetSync(true);

    public static DbOptions CreateDbOptions() => CreateDbOptions(RocksDBStoreSettings.Default);

    public static DbOptions CreateDbOptions(RocksDBStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var options = new DbOptions();
        options.SetCreateMissingColumnFamilies(true);
        options.SetCreateIfMissing(true);
        options.SetErrorIfExists(false);
        options.SetMaxOpenFiles(1000);
        options.SetParanoidChecks(false);
        options.SetWriteBufferSize(settings.WriteBufferSize);
        options.SetMaxWriteBufferNumber(settings.MaxWriteBufferNumber);
        options.SetMinWriteBufferNumberToMerge(settings.MinWriteBufferNumberToMerge);
        options.SetMaxTotalWalSize(settings.MaxTotalWalSize);
        options.SetLevelCompactionDynamicLevelBytes(settings.EnableDynamicLevelBytes);
        options.SetMaxBackgroundCompactions(settings.MaxBackgroundCompactions);
        options.SetMaxBackgroundFlushes(settings.MaxBackgroundFlushes);
        var tableOptions = new BlockBasedTableOptions()
            .SetBlockSize(settings.BlockSize)
            .SetCacheIndexAndFilterBlocks(settings.CacheIndexAndFilterBlocks);
        if (settings.BlockCacheSize > 0)
            tableOptions.SetBlockCache(GetBlockCache(settings.BlockCacheSize));
        options.SetBlockBasedTableFactory(tableOptions);
        return options;
    }

    /// <summary>
    /// Returns the shared LRU block cache for the requested capacity.
    /// </summary>
    /// <remarks>
    /// One cache instance is shared by every database opened with the same capacity and is
    /// deliberately never disposed: RocksDB keeps raw pointers to it for as long as a database
    /// created with it stays open.
    /// </remarks>
    private static Cache GetBlockCache(ulong capacity)
    {
        lock (s_blockCacheLock)
        {
            if (!s_blockCaches.TryGetValue(capacity, out var cache))
            {
                cache = Cache.CreateLru(capacity);
                s_blockCaches.Add(capacity, cache);
            }
            return cache;
        }
    }
}
