// Copyright (C) 2016-2023 The Neo Project.
//
// The neo-cli is free software distributed under the MIT software
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.ScriptHelper
{
    public class ScriptHelper
    {
        private readonly NeoSystem _neoSystem;

        public ScriptHelper(NeoSystem neoSystem)
        {
            _neoSystem = neoSystem;
            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
        }

        private Dictionary<UInt256, TaskCompletionSource<Blockchain.ApplicationExecuted>> transactionCompletionSources
            = new();

        private TaskCompletionSource<Block> _blockStream = new();

        public async Task<Blockchain.ApplicationExecuted> SendRawTransactionAsync(Transaction tx)
        {
            var tcs = new TaskCompletionSource<Blockchain.ApplicationExecuted>();

            // Store the TaskCompletionSource for this transaction
            transactionCompletionSources[tx.Hash] = tcs;

            // Broadcast the transaction
            _neoSystem.Blockchain.Tell(tx, ActorRefs.NoSender);

            return await tcs.Task;
        }

        private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var tx in applicationExecutedList)
            {
                if (!transactionCompletionSources.TryGetValue(tx.Transaction.Hash, out var tcs)) continue;
                tcs.SetResult(tx);

                transactionCompletionSources.Remove(tx.Transaction.Hash);
            }
        }

        private void OnCommitted(NeoSystem system, Block block)
        {
            _blockStream.SetResult(block);
        }

        public async Task<Block> OnNewBlock()
        {
            var tcs = new TaskCompletionSource<Block>();
            // Store the TaskCompletionSource for this transaction
            _blockStream = tcs;
            return await tcs.Task;
        }
    }
}
