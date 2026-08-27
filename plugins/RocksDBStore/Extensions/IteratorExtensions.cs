// Copyright (C) 2015-2026 The Neo Project.
//
// Iterator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.Storage.Extensions;

/// <summary>
/// Extension methods for <see cref="RocksDbSharp.Iterator"/> (RocksDbSharp does not allow
/// extending its sealed iterator type directly, so these are provided as extension methods).
/// </summary>
internal static class IteratorExtensions
{
    /// <summary>
    /// Advances the iterator <paramref name="count"/> entries by calling the native
    /// rocksdb_iter_next repeatedly (rocksdb has no batch-skip primitive).
    /// Does not marshal keys/values while skipping.
    /// </summary>
    public static void Skip(this RocksDbSharp.Iterator it, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        while (count-- > 0 && it.Valid())
            it.Next();
    }

    /// <summary>
    /// Moves the iterator backwards <paramref name="count"/> entries by calling the native
    /// rocksdb_iter_prev repeatedly (rocksdb has no batch-skip primitive).
    /// Does not marshal keys/values while skipping.
    /// </summary>
    public static void SkipPrev(this RocksDbSharp.Iterator it, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        while (count-- > 0 && it.Valid())
            it.Prev();
    }
}
