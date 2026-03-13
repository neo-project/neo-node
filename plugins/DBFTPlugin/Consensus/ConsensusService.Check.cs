// Copyright (C) 2015-2026 The Neo Project.
//
// ConsensusService.Check.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.Plugins.DBFTPlugin.Types;

namespace Neo.Plugins.DBFTPlugin.Consensus;

partial class ConsensusService
{
    private bool CheckPrepareResponse()
    {
        if (context.TransactionHashes.Length == context.Transactions.Count)
        {
            // if we are the primary for this view, but acting as a backup because we recovered our own
            // previously sent prepare request, then we don't want to send a prepare response.
            if (context.IsPrimary || context.WatchOnly) return true;

            // Check maximum block size via Native Contract policy
            if (context.GetExpectedBlockSize() > dbftSettings.MaxBlockSize)
            {
                DBFTPlugin.PluginLogger?.Warning("Rejected block: {Index} The size exceed the policy", context.Block.Index);
                RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                return false;
            }
            // Check maximum block system fee via Native Contract policy
            if (context.GetExpectedBlockSystemFee() > dbftSettings.MaxBlockSystemFee)
            {
                DBFTPlugin.PluginLogger?.Warning("Rejected block: {Index} The system fee exceed the policy", context.Block.Index);
                RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                return false;
            }

            // Timeout extension due to prepare response sent
            // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
            ExtendTimerByFactor(2);

            DBFTPlugin.PluginLogger?.Information("Sending PrepareResponse height={Index} view={ViewNumber}",
                context.Block.Index, context.ViewNumber);
            localNode.Tell(new LocalNode.SendDirectly(context.MakePrepareResponse()));
            CheckPreparations();
        }
        return true;
    }

    private void CheckCommits()
    {
        if (context.CommitPayloads.Count(p => context.GetMessage(p)?.ViewNumber == context.ViewNumber) >= context.M
            && context.TransactionHashes.All(p => context.Transactions.ContainsKey(p)))
        {
            block_received_index = context.Block.Index;
            Block block = context.CreateBlock();
            DBFTPlugin.PluginLogger?.Information("Sending Block: height={Index} hash={Hash} tx={TransactionsLength}",
                block.Index, block.Hash, block.Transactions.Length);
            blockchain.Tell(block);
        }
    }

    private void CheckExpectedView(byte viewNumber)
    {
        if (context.ViewNumber >= viewNumber) return;
        var messages = context.ChangeViewPayloads.Select(p => context.GetMessage<ChangeView>(p)).ToArray();
        // if there are `M` change view payloads with NewViewNumber greater than viewNumber, then, it is safe to move
        if (messages.Count(p => p != null && p.NewViewNumber >= viewNumber) >= context.M)
        {
            if (!context.WatchOnly)
            {
                ChangeView message = messages[context.MyIndex];
                // Communicate the network about my agreement to move to `viewNumber`
                // if my last change view payload, `message`, has NewViewNumber lower than current view to change
                if (message is null || message.NewViewNumber < viewNumber)
                    localNode.Tell(new LocalNode.SendDirectly(context.MakeChangeView(ChangeViewReason.ChangeAgreement)));
            }
            InitializeConsensus(viewNumber);
        }
    }

    private void CheckPreparations()
    {
        if (context.PreparationPayloads.Count(p => p != null) >= context.M
            && context.TransactionHashes.All(p => context.Transactions.ContainsKey(p)))
        {
            ExtensiblePayload payload = context.MakeCommit();
            DBFTPlugin.PluginLogger?.Information("Sending Commit: height={BlockIndex} view={ViewNumber}", context.Block.Index, context.ViewNumber);
            context.Save();
            localNode.Tell(new LocalNode.SendDirectly(payload));
            // Set timer, so we will resend the commit in case of a networking issue
            ChangeTimer(context.TimePerBlock);
            CheckCommits();
        }
    }
}
