// Copyright (C) 2015-2026 The Neo Project.
//
// LevelDBStoreSettings.cs file belongs to the neo project and is free
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
/// The LevelDB options used by <see cref="LevelDBStore"/>, read from LevelDBStore.json.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is replaced by <see cref="Load"/> when the plugin is constructed, which
/// happens before any store is opened, so the values in effect when a database is opened stay
/// fixed for the lifetime of that database.
/// </remarks>
public sealed class LevelDBStoreSettings
{
    public static LevelDBStoreSettings Default { get; private set; } = new();

    /// <summary>
    /// Size of a single in-memory write buffer (memtable). The LevelDB default is 4 MiB, which on
    /// a large-state node fills quickly and turns every persist into a flush plus compaction.
    /// </summary>
    public int WriteBufferSize { get; }

    /// <summary>
    /// Number of table files LevelDB may keep open at the same time. The plugin defaults to 2000
    /// to reduce repeated open/close on databases with many SST files.
    /// </summary>
    public int MaxOpenFiles { get; }

    /// <summary>
    /// Size of a data block in the table file. Larger blocks reduce index entries and read
    /// operations per lookup at the cost of more bytes read per miss.
    /// </summary>
    public int BlockSize { get; }

    public LevelDBStoreSettings()
    {
        WriteBufferSize = 64 << 20;
        MaxOpenFiles = 2000;
        BlockSize = 16384;
    }

    public LevelDBStoreSettings(IConfigurationSection section)
    {
        WriteBufferSize = section.GetValue("WriteBufferSize", 64 << 20);
        MaxOpenFiles = section.GetValue("MaxOpenFiles", 2000);
        BlockSize = section.GetValue("BlockSize", 16384);
    }

    public static void Load(IConfigurationSection section)
    {
        Default = new LevelDBStoreSettings(section);
    }
}
