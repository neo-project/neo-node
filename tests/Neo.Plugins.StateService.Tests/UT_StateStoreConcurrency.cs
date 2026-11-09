// Copyright (C) 2015-2026 The Neo Project.
//
// UT_StateStoreConcurrency.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.StateService.Storage;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Neo.Plugins.StateService.Tests;

[TestClass]
public class UT_StateStoreConcurrency
{
    [TestMethod]
    [DataRow("LocalRootIndex", false)]
    [DataRow("ValidatedRootIndex", false)]
    [DataRow("CurrentLocalRootHash", false)]
    [DataRow("LocalRootIndex", true)]
    [DataRow("ValidatedRootIndex", true)]
    [DataRow("CurrentLocalRootHash", true)]
    public async Task SnapshotLifetimeCoversActiveReaders(string property, bool dispose)
    {
        using var storage = new GatedStore();
        // Exercise snapshot ownership without starting an actor or changing the singleton.
        var store = (StateStore)RuntimeHelpers.GetUninitializedObject(typeof(StateStore));
        void Set(string name, object value) => typeof(StateStore)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(store, value);
        Set("_store", storage);
        Set("_snapshotLock", new object());
        Set("_currentSnapshot", new StateSnapshot(storage));
        var original = storage.LastSnapshot!;
        storage.GateTarget = original;
        Task? reader = null;
        Task? writer = null;
        using var writerStarted = new ManualResetEventSlim();
        try
        {
            reader = Task.Run(() => typeof(StateStore).GetProperty(property)!.GetValue(store));
            Assert.IsTrue(storage.ReaderEntered.Wait(TimeSpan.FromSeconds(5)));
            writer = Task.Run(() =>
            {
                writerStarted.Set();
                if (dispose) store.Dispose();
                else typeof(StateStore).GetMethod("UpdateCurrentSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(store, null);
            });
            Assert.IsTrue(writerStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(original.Disposed.Wait(TimeSpan.FromMilliseconds(200)),
                "The active snapshot must remain alive until its reader finishes.");
            storage.Release.Set();
            await Task.WhenAll(reader, writer).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(original.Disposed.IsSet);
            if (dispose)
                Assert.ThrowsExactly<ObjectDisposedException>(() => { _ = store.LocalRootIndex; });
            else
                Assert.IsNull(store.LocalRootIndex);
        }
        finally
        {
            storage.Release.Set();
            if (reader is not null) await reader.WaitAsync(TimeSpan.FromSeconds(5));
            if (writer is not null) await writer.WaitAsync(TimeSpan.FromSeconds(5));
            store.Dispose();
        }
    }

    private sealed class GatedStore : IStore
    {
        private readonly MemoryStore _inner = new();
        public GatedSnapshot? GateTarget;
        public GatedSnapshot? LastSnapshot;
        public readonly ManualResetEventSlim ReaderEntered = new();
        public readonly ManualResetEventSlim Release = new();
        public event IStore.OnNewSnapshotDelegate? OnNewSnapshot;
        public IStoreSnapshot GetSnapshot()
        {
            var snapshot = new GatedSnapshot(_inner.GetSnapshot(), this);
            LastSnapshot = snapshot;
            OnNewSnapshot?.Invoke(this, snapshot);
            return snapshot;
        }
        public void Delete(byte[] key) => _inner.Delete(key);
        public void Put(byte[] key, byte[] value) => _inner.Put(key, value);
        public bool Contains(byte[] key) => _inner.Contains(key);
        public byte[]? TryGet(byte[] key) => _inner.TryGet(key, out var value) ? value : null;
        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value) => _inner.TryGet(key, out value);
        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyPrefix = null, SeekDirection direction = SeekDirection.Forward, int skip = 0)
            => _inner.Find(keyPrefix, direction, skip);
        public void Dispose() => _inner.Dispose();
    }

    private sealed class GatedSnapshot(IStoreSnapshot inner, GatedStore owner) : IStoreSnapshot
    {
        public readonly ManualResetEventSlim Disposed = new();
        public IStore Store => owner;
        public void Commit() => inner.Commit();
        public void Delete(byte[] key) => inner.Delete(key);
        public void Put(byte[] key, byte[] value) => inner.Put(key, value);
        public bool Contains(byte[] key) => inner.Contains(key);
        public byte[]? TryGet(byte[] key) => TryGet(key, out var value) ? value : null;
        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
        {
            if (ReferenceEquals(owner.GateTarget, this))
            {
                owner.ReaderEntered.Set();
                if (!owner.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            ObjectDisposedException.ThrowIf(Disposed.IsSet, this);
            return inner.TryGet(key, out value);
        }
        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyPrefix = null, SeekDirection direction = SeekDirection.Forward, int skip = 0)
            => inner.Find(keyPrefix, direction, skip);
        public void Dispose()
        {
            Disposed.Set();
            inner.Dispose();
        }
    }
}
