#nullable enable

// Copyright (C) 2015-2026 The Neo Project.
//
// UT_NeoStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.ApplicationLogs.Store;
using Neo.SmartContract;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Plugins.ApplicationsLogs.Tests;

[TestClass]
public class UT_NeoStore
{
    [TestMethod]
    public void TestLogStorageStoreDoesNotDisposeBorrowedSnapshot()
    {
        using var store = new TrackingStore();
        var snapshot = store.CreateTrackingSnapshot();

        using (var lss = new LogStorageStore(snapshot))
        {
        }

        Assert.IsFalse(snapshot.IsDisposed);

        snapshot.Dispose();
        Assert.IsTrue(snapshot.IsDisposed);
    }

    [TestMethod]
    public void TestReadOperationsDisposeOwnedSnapshots()
    {
        using var store = new TrackingStore();
        using var neoStore = new NeoStore(store);

        UInt160 scriptHash = UInt160.Zero;
        UInt256 hash = UInt256.Zero;

        Assert.IsEmpty(store.Snapshots);

        Assert.IsEmpty(neoStore.GetContractLog(scriptHash));
        Assert.IsEmpty(neoStore.GetContractLog(scriptHash, TriggerType.Application));
        Assert.IsEmpty(neoStore.GetContractLog(scriptHash, TriggerType.Application, "Transfer"));
        Assert.IsNull(neoStore.GetBlockLog(hash, TriggerType.Application));
        Assert.IsNull(neoStore.GetBlockLog(hash, TriggerType.Application, "Transfer"));
        Assert.IsNull(neoStore.GetTransactionLog(hash));
        Assert.IsNull(neoStore.GetTransactionLog(hash, "Transfer"));

        Assert.HasCount(7, store.Snapshots);
        Assert.IsTrue(store.Snapshots.All(u => u.IsDisposed));
    }

    private sealed class TrackingStore : IStore
    {
        private readonly MemoryStore _inner = new();

        public List<TrackingSnapshot> Snapshots { get; } = [];

        public event IStore.OnNewSnapshotDelegate? OnNewSnapshot;

        public TrackingSnapshot CreateTrackingSnapshot()
        {
            var snapshot = new TrackingSnapshot(this, _inner.GetSnapshot());
            Snapshots.Add(snapshot);
            return snapshot;
        }

        public void Delete(byte[] key) => _inner.Delete(key);

        public void Dispose() => _inner.Dispose();

        public IStoreSnapshot GetSnapshot()
        {
            var snapshot = CreateTrackingSnapshot();
            OnNewSnapshot?.Invoke(this, snapshot);
            return snapshot;
        }

        public void Put(byte[] key, byte[] value) => _inner.Put(key, value);

        public void PutSync(byte[] key, byte[] value) => _inner.Put(key, value);

        public bool Contains(byte[] key) => _inner.Contains(key);

        public byte[]? TryGet(byte[] key)
        {
            _inner.TryGet(key, out var value);
            return value;
        }

        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value) => _inner.TryGet(key, out value);

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyPrefix = null, SeekDirection direction = SeekDirection.Forward) =>
            _inner.Find(keyPrefix, direction);
    }

    private sealed class TrackingSnapshot(TrackingStore store, IStoreSnapshot inner) : IStoreSnapshot
    {
        public bool IsDisposed { get; private set; }

        public IStore Store => store;

        public void Commit() => inner.Commit();

        public void Delete(byte[] key) => inner.Delete(key);

        public void Dispose()
        {
            IsDisposed = true;
            inner.Dispose();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyPrefix = null, SeekDirection direction = SeekDirection.Forward) =>
            inner.Find(keyPrefix, direction);

        public bool Contains(byte[] key) => inner.Contains(key);

        public byte[]? TryGet(byte[] key)
        {
            inner.TryGet(key, out var value);
            return value;
        }

        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value) => inner.TryGet(key, out value);

        public void Put(byte[] key, byte[] value) => inner.Put(key, value);
    }
}
