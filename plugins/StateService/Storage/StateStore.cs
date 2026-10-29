// Copyright (C) 2015-2026 The Neo Project.
//
// StateStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Akka.Actor;
using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Network;
using Neo.Plugins.StateService.Verification;
using Neo.SmartContract;
using System.Collections.Concurrent;

namespace Neo.Plugins.StateService.Storage;

class StateStore : UntypedActor
{
    private readonly StatePlugin _system;
    private readonly IStore _store;
    private const int MaxCacheCount = 100;
    private readonly ConcurrentDictionary<uint, StateRoot> _cache = new();
    private readonly object _snapshotLock = new();
    private bool _disposed;
    private StateSnapshot _currentSnapshot;
    private StateSnapshot? _stateSnapshot;
    public UInt256 CurrentLocalRootHash => ReadCurrentSnapshot(snapshot => snapshot.CurrentLocalRootHash());
    public uint? LocalRootIndex => ReadCurrentSnapshot(snapshot => snapshot.CurrentLocalRootIndex());
    public uint? ValidatedRootIndex => ReadCurrentSnapshot(snapshot => snapshot.CurrentValidatedRootIndex());

    private T ReadCurrentSnapshot<T>(Func<StateSnapshot, T> read)
    {
        lock (_snapshotLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return read(_currentSnapshot);
        }
    }

    private static StateStore? _singleton;
    public static StateStore Singleton
    {
        get
        {
            StateStore? store;
            while ((store = Volatile.Read(ref _singleton)) is null) Thread.Sleep(10);
            return store;
        }
    }

    public StateStore(StatePlugin system, string path)
    {
        if (_singleton != null) throw new InvalidOperationException(nameof(StateStore));
        _system = system;
        _store = StatePlugin.NeoSystem.LoadStore(path);
        StatePlugin.NeoSystem.ActorSystem.EventStream.Subscribe(Self, typeof(Blockchain.RelayResult));
        _currentSnapshot = GetSnapshot();
        Volatile.Write(ref _singleton, this);
    }

    public void Dispose()
    {
        lock (_snapshotLock)
        {
            if (_disposed) return;
            _disposed = true;
            _currentSnapshot.Dispose();
            _store.Dispose();
        }
    }

    public StateSnapshot GetSnapshot()
    {
        return new StateSnapshot(_store);
    }

    public IStoreSnapshot GetStoreSnapshot()
    {
        return _store.GetSnapshot();
    }

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case StateRoot state_root:
                OnNewStateRoot(state_root);
                break;
            case Blockchain.RelayResult rr:
                if (rr.Result == VerifyResult.Succeed && rr.Inventory is ExtensiblePayload payload && payload.Category == StatePlugin.StatePayloadCategory)
                    OnStatePayload(payload);
                break;
            default:
                break;
        }
    }

    private void OnStatePayload(ExtensiblePayload payload)
    {
        if (payload.Data.Length == 0) return;
        if ((MessageType)payload.Data.Span[0] != MessageType.StateRoot) return;

        StateRoot message;
        try
        {
            message = payload.Data[1..].AsSerializable<StateRoot>();
        }
        catch (FormatException)
        {
            return;
        }
        OnNewStateRoot(message);
    }

    private bool OnNewStateRoot(StateRoot stateRoot)
    {
        if (stateRoot.Witness is null) return false;
        if (ValidatedRootIndex != null && stateRoot.Index <= ValidatedRootIndex) return false;
        if (LocalRootIndex is null) throw new InvalidOperationException(nameof(StateStore) + " could not get local root index");
        if (LocalRootIndex < stateRoot.Index && stateRoot.Index < LocalRootIndex + MaxCacheCount)
        {
            return _cache.TryAdd(stateRoot.Index, stateRoot);
        }

        using var stateSnapshot = Singleton.GetSnapshot();
        var localRoot = stateSnapshot.GetStateRoot(stateRoot.Index);
        if (localRoot is null || localRoot.Witness != null) return false;
        if (!stateRoot.Verify(StatePlugin.NeoSystem.Settings, StatePlugin.NeoSystem.StoreView)) return false;
        if (localRoot.RootHash != stateRoot.RootHash) return false;

        stateSnapshot.AddValidatedStateRoot(stateRoot);
        stateSnapshot.Commit();
        UpdateCurrentSnapshot();
        _system.Verifier?.Tell(new VerificationService.ValidatedRootPersisted { Index = stateRoot.Index });
        return true;
    }

    public void UpdateLocalStateRootSnapshot(uint height, IEnumerable<KeyValuePair<StorageKey, DataCache.Trackable>> changeSet)
    {
        _stateSnapshot?.Dispose();
        _stateSnapshot = Singleton.GetSnapshot();
        foreach (var item in changeSet)
        {
            switch (item.Value.State)
            {
                case TrackState.Added:
                    _stateSnapshot.Trie.Put(item.Key.ToArray(), item.Value.Item.ToArray());
                    break;
                case TrackState.Changed:
                    _stateSnapshot.Trie.Put(item.Key.ToArray(), item.Value.Item.ToArray());
                    break;
                case TrackState.Deleted:
                    _stateSnapshot.Trie.Delete(item.Key.ToArray());
                    break;
            }
        }

        var rootHash = _stateSnapshot.Trie.Root.Hash;
        var stateRoot = new StateRoot
        {
            Version = StateRoot.CurrentVersion,
            Index = height,
            RootHash = rootHash,
            Witness = null,
        };
        _stateSnapshot.AddLocalStateRoot(stateRoot);
    }

    public void UpdateLocalStateRoot(uint height)
    {
        if (_stateSnapshot != null)
        {
            _stateSnapshot.Commit();
            _stateSnapshot.Dispose();
            _stateSnapshot = null;
        }
        UpdateCurrentSnapshot();
        _system.Verifier?.Tell(new VerificationService.BlockPersisted { Index = height });
        CheckValidatedStateRoot(height);
    }

    private void CheckValidatedStateRoot(uint index)
    {
        if (_cache.TryRemove(index, out var stateRoot))
        {
            Self.Tell(stateRoot);
        }
    }

    private void UpdateCurrentSnapshot()
    {
        lock (_snapshotLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var previous = _currentSnapshot;
            _currentSnapshot = GetSnapshot();
            previous.Dispose();
        }
    }

    protected override void PostStop()
    {
        Dispose();
        Interlocked.CompareExchange(ref _singleton, null, this);
        base.PostStop();
    }

    public static Props Props(StatePlugin system, string path)
    {
        return Akka.Actor.Props.Create(() => new StateStore(system, path));
    }
}

#nullable disable
