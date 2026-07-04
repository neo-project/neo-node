// Copyright (C) 2015-2026 The Neo Project.
//
// Snapshot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using RocksDbSharp;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Plugins.Storage;

/// <summary>
/// <remarks>On-chain write operations on a snapshot cannot be concurrent.</remarks>
/// </summary>
internal class Snapshot : IStoreSnapshot
{
    private readonly RocksDb _db;
    private readonly RocksDbSharp.Snapshot _snapshot;
    private readonly WriteBatch _batch;
    private readonly ReadOptions _options;
    private readonly Lock _lock = new();

    public IStore Store { get; }

    internal Snapshot(Store store, RocksDb db)
    {
        Store = store;
        _db = db;
        _snapshot = db.CreateSnapshot();
        _batch = new WriteBatch();

        _options = new ReadOptions();
        _options.SetFillCache(false);
        _options.SetSnapshot(_snapshot);
    }

    public void Commit()
    {
        lock (_lock)
            _db.Write(_batch, Options.WriteDefault);
    }

    public void Delete(byte[] key)
    {
        lock (_lock)
            _batch.Delete(key);
    }

    public void Put(byte[] key, byte[] value)
    {
        lock (_lock)
            _batch.Put(key, value);
    }

    /// <inheritdoc/>
    public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction)
    {
        keyOrPrefix ??= [];
        using var it = _db.NewIterator(readOptions: _options);
        if (direction == SeekDirection.Forward)
        {
            for (it.Seek(keyOrPrefix); it.Valid(); it.Next())
                yield return (it.Key(), it.Value());
        }
        else
        {
            for (it.SeekForPrev(keyOrPrefix); it.Valid(); it.Prev())
                yield return (it.Key(), it.Value());
        }
    }

    public IEnumerable<(byte[] Key, byte[] Value)> FindRange(byte[] start, byte[] end, SeekDirection direction = SeekDirection.Forward)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        var order = start.AsSpan().SequenceCompareTo(end);
        if ((direction == SeekDirection.Forward && order >= 0) ||
            (direction == SeekDirection.Backward && order <= 0))
        {
            yield break;
        }

        foreach (var item in Find(start, direction))
        {
            order = item.Key.AsSpan().SequenceCompareTo(end);
            if ((direction == SeekDirection.Forward && order >= 0) ||
                (direction == SeekDirection.Backward && order <= 0))
            {
                yield break; // end is the exclusive max key if forward, or the exclusive min key if backward
            }
            yield return item;
        }
    }

    public bool Contains(byte[] key)
    {
        return _db.Get(key, Array.Empty<byte>(), 0, 0, readOptions: _options) >= 0;
    }

    public byte[]? TryGet(byte[] key)
    {
        return _db.Get(key, readOptions: _options);
    }

    public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
    {
        value = _db.Get(key, readOptions: _options);
        return value != null;
    }

    public void Dispose()
    {
        _snapshot.Dispose();
        _batch.Dispose();
    }
}
