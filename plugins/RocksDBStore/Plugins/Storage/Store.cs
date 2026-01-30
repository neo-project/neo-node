// Copyright (C) 2015-2026 The Neo Project.
//
// Store.cs file belongs to the neo project and is free
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

internal class Store : IStore
{
    private readonly RocksDb _db;

    /// <inheritdoc/>
    public event IStore.OnNewSnapshotDelegate? OnNewSnapshot;

    public Store(string path)
    {
        _db = RocksDb.Open(Options.Default, Path.GetFullPath(path));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    public IStoreSnapshot GetSnapshot()
    {
        var snapshot = new Snapshot(this, _db);
        OnNewSnapshot?.Invoke(this, snapshot);
        return snapshot;
    }

    /// <inheritdoc/>
    public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
    {
        keyOrPrefix ??= [];

        using var it = _db.NewIterator();
        if (direction == SeekDirection.Forward)
            for (it.Seek(keyOrPrefix); it.Valid(); it.Next())
                yield return (it.Key(), it.Value());
        else
            for (it.SeekForPrev(keyOrPrefix); it.Valid(); it.Prev())
                yield return (it.Key(), it.Value());
    }
    public IEnumerable<(byte[] Key, byte[] Value)> FindRange(byte[] start, byte[] end, SeekDirection direction = SeekDirection.Forward)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        if (CompareLex(start, end) >= 0)
            yield break;

        using var it = _db.NewIterator();

        if (direction == SeekDirection.Forward)
        {
            for (it.Seek(start); it.Valid(); it.Next())
            {
                var key = it.Key();
                if (CompareLex(key, end) >= 0)
                    break;

                yield return (key, it.Value());
            }
        }
        else
        {
            it.Seek(end);

            if (!it.Valid())
            {
                it.SeekToLast();
            }
            else
            {
                it.Prev();
            }

            for (; it.Valid(); it.Prev())
            {
                var key = it.Key();
                if (CompareLex(key, start) < 0)
                    break;

                yield return (key, it.Value());
            }
        }

        static int CompareLex(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int diff = a[i].CompareTo(b[i]);
                if (diff != 0) return diff;
            }
            return a.Length.CompareTo(b.Length);
        }
    }

    public bool Contains(byte[] key)
    {
        return _db.Get(key, Array.Empty<byte>(), 0, 0) >= 0;
    }

    public byte[]? TryGet(byte[] key)
    {
        return _db.Get(key);
    }

    public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
    {
        value = _db.Get(key);
        return value != null;
    }

    public void Delete(byte[] key)
    {
        _db.Remove(key);
    }

    public void Put(byte[] key, byte[] value)
    {
        _db.Put(key, value);
    }

    public void PutSync(byte[] key, byte[] value)
    {
        _db.Put(key, value, writeOptions: Options.WriteDefaultSync);
    }
}
