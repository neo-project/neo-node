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

using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using LSnapshot = Neo.IO.Data.LevelDB.Snapshot;

namespace Neo.Plugins.Storage;

/// <summary>
/// <code>Iterating over the whole dataset can be time-consuming. Depending upon how large the dataset is.</code>
/// <remarks>On-chain write operations on a snapshot cannot be concurrent.</remarks>
/// </summary>
internal class Snapshot : IStoreSnapshot, IEnumerable<KeyValuePair<byte[], byte[]>>
{
    private readonly DB _db;
    private readonly LSnapshot _snapshot;
    private readonly ReadOptions _readOptions;
    private readonly WriteBatch _batch;
    private readonly Lock _lock = new();

    public IStore Store { get; }

    internal Snapshot(Store store, DB db)
    {
        Store = store;
        _db = db;
        _snapshot = db.CreateSnapshot();
        _readOptions = new ReadOptions { FillCache = false, Snapshot = _snapshot };
        _batch = new WriteBatch();
    }

    /// <inheritdoc/>
    public void Commit()
    {
        lock (_lock)
            _db.Write(WriteOptions.Default, _batch);
    }

    /// <inheritdoc/>
    public void Delete(byte[] key)
    {
        lock (_lock)
            _batch.Delete(key);
    }

    /// <inheritdoc/>
    public void Put(byte[] key, byte[] value)
    {
        lock (_lock)
            _batch.Put(key, value);
    }

    public void Dispose()
    {
        _snapshot.Dispose();
        _readOptions.Dispose();
    }

    /// <inheritdoc/>
    public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
    {
        return _db.Seek(_readOptions, keyOrPrefix, direction);
    }
    public IEnumerable<(byte[] Key, byte[] Value)> FindRange(byte[] start, byte[] end, SeekDirection direction = SeekDirection.Forward)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        if (Helper.CompareLex(start, end) >= 0)
            yield break;

        using var iterator = _db.CreateIterator(_readOptions);

        if (direction == SeekDirection.Forward)
        {
            iterator.Seek(start);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (key is null) break;

                if (Helper.CompareLex(key, end) >= 0)
                    break;

                yield return (key, iterator.Value()!);
                iterator.Next();
            }
        }
        else
        {
            iterator.Seek(end);

            if (!iterator.Valid())
            {
                iterator.SeekToLast();
            }
            else
            {
                iterator.Prev();
            }

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (key is null) break;

                if (Helper.CompareLex(key, start) < 0)
                    break;

                yield return (key, iterator.Value()!);
                iterator.Prev();
            }
        }
    }
    public bool Contains(byte[] key)
    {
        return _db.Contains(_readOptions, key);
    }

    public byte[]? TryGet(byte[] key)
    {
        return _db.Get(_readOptions, key);
    }

    public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
    {
        value = _db.Get(_readOptions, key);
        return value != null;
    }

    public IEnumerator<KeyValuePair<byte[], byte[]>> GetEnumerator()
    {
        using var iterator = _db.CreateIterator(_readOptions);
        for (iterator.SeekToFirst(); iterator.Valid(); iterator.Next())
            yield return new KeyValuePair<byte[], byte[]>(iterator.Key()!, iterator.Value()!);
    }

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
