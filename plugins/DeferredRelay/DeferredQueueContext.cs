// Copyright (C) 2015-2026 The Neo Project.
//
// DeferredQueueContext.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.Plugins.DeferredRelay;

/// <summary>
/// Tracks per-sender queue depth and builds a <see cref="TransactionVerificationContext"/> that
/// includes already-queued transactions (fee/oracle aggregation for deferred offers).
/// </summary>
internal sealed class DeferredQueueContext
{
    private readonly Lock _lock = new();
    private readonly Dictionary<UInt160, int> _senderCounts = new();
    private readonly List<Transaction> _queued = new();

    public static DeferredQueueContext Bootstrap(IStore store)
    {
        var context = new DeferredQueueContext();
        foreach ((byte[] key, byte[] value) in store.Find())
        {
            if (key.Length != UInt256.Length) continue;
            try
            {
                context.OnQueued(value.AsSerializable<Transaction>());
            }
            catch
            {
                // Corrupt entries are ignored here; ProcessQueuedAsync removes them later.
            }
        }

        return context;
    }

    public bool CanAcceptSender(UInt160 sender, uint maxPerSender)
    {
        if (maxPerSender == 0) return true;
        lock (_lock)
        {
            _senderCounts.TryGetValue(sender, out int count);
            return count < maxPerSender;
        }
    }

    public TransactionVerificationContext CreateVerificationContext()
    {
        var context = new TransactionVerificationContext();
        lock (_lock)
        {
            foreach (Transaction tx in _queued)
                context.AddTransaction(tx);
        }

        return context;
    }

    public void OnQueued(Transaction tx)
    {
        lock (_lock)
        {
            _queued.Add(tx);
            _senderCounts.TryGetValue(tx.Sender, out int count);
            _senderCounts[tx.Sender] = count + 1;
        }
    }

    public void OnRemoved(Transaction tx)
    {
        lock (_lock)
        {
            for (int i = _queued.Count - 1; i >= 0; i--)
            {
                if (_queued[i].Hash == tx.Hash)
                {
                    _queued.RemoveAt(i);
                    break;
                }
            }

            if (_senderCounts.TryGetValue(tx.Sender, out int count))
            {
                if (count <= 1)
                    _senderCounts.Remove(tx.Sender);
                else
                    _senderCounts[tx.Sender] = count - 1;
            }
        }
    }
}
