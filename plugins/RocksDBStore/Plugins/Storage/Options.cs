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
    public static readonly DbOptions Default = CreateDbOptions();
    public static readonly ReadOptions ReadDefault = new();
    public static readonly WriteOptions WriteDefault = new();
    public static readonly WriteOptions WriteDefaultSync = new WriteOptions().SetSync(true);

    public static DbOptions CreateDbOptions()
    {
        var options = new DbOptions();
        options.SetCreateMissingColumnFamilies(true);
        options.SetCreateIfMissing(true);
        options.SetErrorIfExists(false);
        options.SetMaxOpenFiles(1000);
        options.SetParanoidChecks(false);
        // --- I/O tuning for consensus nodes (was: 4MB write buffer -> frequent
        //     flush + L0 compaction stalls saturating disk) ---
        options.SetWriteBufferSize(64 << 20);              // 4MB -> 64MB
        options.SetMaxWriteBufferNumber(4);                // default 3
        options.SetMinWriteBufferNumberToMerge(2);
        options.SetMaxTotalWalSize(128 << 20);             // bound WAL growth
        options.SetLevelCompactionDynamicLevelBytes(true); // dynamic level bytes: lower write amp
        options.SetMaxBackgroundCompactions(3);            // parallel compaction
        options.SetMaxBackgroundFlushes(1);                // parallel flush
        options.SetBlockBasedTableFactory(new BlockBasedTableOptions()
            .SetBlockSize(16384)                           // 4KB -> 16KB
            .SetCacheIndexAndFilterBlocks(true));
        return options;
    }
}
