// Copyright (C) 2015-2026 The Neo Project.
//
// ConsensusService.OnMessage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Cryptography;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.Plugins.DBFTPlugin.Consensus;

partial class ConsensusService
{
    private void OnConsensusPayload(ExtensiblePayload payload)
    {
        if (context.BlockSent) return;
        ConsensusMessage message;
        try
        {
            message = context.GetMessage(payload);
        }
        catch (Exception ex)
        {
            DBFTPlugin.PluginLogger?.Information("Error getting message {Exception}", ex.ToString());
            return;
        }

        if (!message.Verify(neoSystem.Settings)) return;
        if (message.BlockIndex != context.Block.Index)
        {
            if (context.Block.Index < message.BlockIndex)
            {
                DBFTPlugin.PluginLogger?.Warning("Chain is behind: expected={Expected} current={Current}", message.BlockIndex, context.Block.Index - 1);
            }
            return;
        }
        if (message.ValidatorIndex >= context.Validators.Length) return;
        if (payload.Sender != Contract.CreateSignatureRedeemScript(context.Validators[message.ValidatorIndex]).ToScriptHash()) return;
        context.LastSeenMessage?[context.Validators[message.ValidatorIndex]] = message.BlockIndex;
        switch (message)
        {
            case PrepareRequest request:
                OnPrepareRequestReceived(payload, request);
                break;
            case PrepareResponse response:
                OnPrepareResponseReceived(payload, response);
                break;
            case ChangeView view:
                OnChangeViewReceived(payload, view);
                break;
            case Commit commit:
                OnCommitReceived(payload, commit);
                break;
            case RecoveryRequest request:
                OnRecoveryRequestReceived(payload, request);
                break;
            case RecoveryMessage recovery:
                OnRecoveryMessageReceived(recovery);
                break;
        }
    }

    private void OnPrepareRequestReceived(ExtensiblePayload payload, PrepareRequest message)
    {
        if (context.RequestSentOrReceived || context.NotAcceptingPayloadsDueToViewChanging) return;
        if (message.ValidatorIndex != context.Block.PrimaryIndex || message.ViewNumber != context.ViewNumber) return;
        if (message.Version != context.Block.Version || message.PrevHash != context.Block.PrevHash) return;
        if (message.TransactionHashes.Length > neoSystem.Settings.MaxTransactionsPerBlock) return;

        DBFTPlugin.PluginLogger?.Information("OnPrepareRequestReceived: height={BlockIndex} view={ViewNumber} index={ValidatorIndex} tx={TransactionHashesLength}",
            message.BlockIndex, message.ViewNumber, message.ValidatorIndex, message.TransactionHashes.Length);
        if (message.Timestamp <= context.PrevHeader.Timestamp
            || message.Timestamp > TimeProvider.Current.UtcNow.AddMilliseconds(8 * context.TimePerBlock.TotalMilliseconds).ToTimestampMS())
        {
            DBFTPlugin.PluginLogger?.Warning("Timestamp incorrect: {Timestamp}", message.Timestamp);
            return;
        }

        if (message.TransactionHashes.Any(p => NativeContract.Ledger.ContainsTransaction(context.Snapshot, p)))
        {
            DBFTPlugin.PluginLogger?.Warning("Invalid request: transaction already exists");
            return;
        }

        // Timeout extension: prepare request has been received with success
        // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
        ExtendTimerByFactor(2);

        prepareRequestReceivedTime = TimeProvider.Current.UtcNow;
        prepareRequestReceivedBlockIndex = message.BlockIndex;

        context.Block.Header.Timestamp = message.Timestamp;
        context.Block.Header.Nonce = message.Nonce;
        context.TransactionHashes = message.TransactionHashes;

        context.Transactions = new Dictionary<UInt256, Transaction>();
        context.VerificationContext = new TransactionVerificationContext();
        for (int i = 0; i < context.PreparationPayloads.Length; i++)
        {
            if (context.PreparationPayloads[i] != null)
            {
                if (!context.GetMessage<PrepareResponse>(context.PreparationPayloads[i]!).PreparationHash.Equals(payload.Hash))
                    context.PreparationPayloads[i] = null;
            }
        }
        context.PreparationPayloads[message.ValidatorIndex] = payload;
        byte[] hashData = context.EnsureHeader()!.GetSignData(neoSystem.Settings.Network);
        for (int i = 0; i < context.CommitPayloads.Length; i++)
        {
            if (context.GetMessage(context.CommitPayloads[i])?.ViewNumber == context.ViewNumber)
            {
                if (!Crypto.VerifySignature(hashData, context.GetMessage<Commit>(context.CommitPayloads[i]!).Signature.Span, context.Validators[i]))
                    context.CommitPayloads[i] = null;
            }
        }

        if (context.TransactionHashes.Length == 0)
        {
            // There are no tx so we should act like if all the transactions were filled
            CheckPrepareResponse();
            return;
        }

        Dictionary<UInt256, Transaction> mempoolVerified = neoSystem.MemPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
        var unverified = new List<Transaction>();
        var mtb = neoSystem.Settings.MaxTraceableBlocks;
        foreach (UInt256 hash in context.TransactionHashes)
        {
            if (mempoolVerified.TryGetValue(hash, out Transaction? tx))
            {
                if (NativeContract.Ledger.ContainsConflictHash(context.Snapshot, hash, tx.Signers.Select(s => s.Account), mtb))
                {
                    DBFTPlugin.PluginLogger?.Warning("Invalid request: transaction has on-chain conflict");
                    return;
                }

                if (!AddTransaction(tx, false))
                    return;
            }
            else
            {
                if (neoSystem.MemPool.TryGetValue(hash, out tx))
                {
                    if (NativeContract.Ledger.ContainsConflictHash(context.Snapshot, hash, tx.Signers.Select(s => s.Account), mtb))
                    {
                        DBFTPlugin.PluginLogger?.Warning("Invalid request: transaction has on-chain conflict");
                        return;
                    }
                    unverified.Add(tx);
                }
            }
        }
        foreach (Transaction tx in unverified)
        {
            if (!AddTransaction(tx, true))
                return;
        }
        if (context.Transactions.Count < context.TransactionHashes.Length)
        {
            UInt256[] hashes = context.TransactionHashes.Where(i => !context.Transactions.ContainsKey(i)).ToArray();
            taskManager.Tell(new TaskManager.RestartTasks(InvPayload.Create(InventoryType.TX, hashes)));
        }
    }

    private void OnPrepareResponseReceived(ExtensiblePayload payload, PrepareResponse message)
    {
        if (message.ViewNumber != context.ViewNumber) return;
        if (context.PreparationPayloads[message.ValidatorIndex] != null || context.NotAcceptingPayloadsDueToViewChanging) return;
        if (context.PreparationPayloads[context.Block.PrimaryIndex] != null
            && !message.PreparationHash.Equals(context.PreparationPayloads[context.Block.PrimaryIndex]!.Hash))
        {
            return;
        }

        // Timeout extension: prepare response has been received with success
        // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
        ExtendTimerByFactor(2);

        DBFTPlugin.PluginLogger?.Information("OnPrepareResponseReceived: height={BlockIndex} view={ViewNumber} index={ValidatorIndex}",
           message.BlockIndex, message.ViewNumber, message.ValidatorIndex);
        context.PreparationPayloads[message.ValidatorIndex] = payload;
        if (context.WatchOnly || context.CommitSent) return;
        if (context.RequestSentOrReceived)
            CheckPreparations();
    }

    private void OnChangeViewReceived(ExtensiblePayload payload, ChangeView message)
    {
        if (message.NewViewNumber <= context.ViewNumber)
            OnRecoveryRequestReceived(payload, message);

        if (context.CommitSent) return;

        var expectedView = context.GetMessage<ChangeView>(context.ChangeViewPayloads[message.ValidatorIndex])?.NewViewNumber ?? 0;
        if (message.NewViewNumber <= expectedView)
            return;

        DBFTPlugin.PluginLogger?.Information("OnChangeViewReceived: height={BlockIndex} view={ViewNumber} index={ValidatorIndex} newView={NewViewNumber} reason={Reason}",
            message.BlockIndex, message.ViewNumber, message.ValidatorIndex, message.NewViewNumber, message.Reason);
        context.ChangeViewPayloads[message.ValidatorIndex] = payload;
        CheckExpectedView(message.NewViewNumber);
    }

    private void OnCommitReceived(ExtensiblePayload payload, Commit commit)
    {
        ref ExtensiblePayload? existingCommitPayload = ref context.CommitPayloads[commit.ValidatorIndex];
        if (existingCommitPayload != null)
        {
            if (existingCommitPayload.Hash != payload.Hash)
                DBFTPlugin.PluginLogger?.Warning("Rejected Commit: height={BlockIndex} index={ValidatorIndex} view={ViewNumber} existingView={ExistingView}",
                    commit.BlockIndex, commit.ValidatorIndex, commit.ViewNumber, context.GetMessage(existingCommitPayload).ViewNumber);
            return;
        }

        if (commit.ViewNumber == context.ViewNumber)
        {
            // Timeout extension: commit has been received with success
            // around 4*15s/M=60.0s/5=12.0s ~ 80% block time (for M=5)
            ExtendTimerByFactor(4);

            DBFTPlugin.PluginLogger?.Information("OnCommitReceived: height={BlockIndex} view={ViewNumber} index={ValidatorIndex} committed={CountCommitted} failed={CountFailed}",
                commit.BlockIndex, commit.ViewNumber, commit.ValidatorIndex, context.CountCommitted, context.CountFailed);

            byte[]? hashData = context.EnsureHeader()?.GetSignData(neoSystem.Settings.Network);
            if (hashData == null)
            {
                existingCommitPayload = payload;
            }
            else if (Crypto.VerifySignature(hashData, commit.Signature.Span, context.Validators[commit.ValidatorIndex]))
            {
                existingCommitPayload = payload;
                CheckCommits();
            }
            return;
        }
        else
        {
            // Receiving commit from another view
            existingCommitPayload = payload;
        }
    }

    private void OnRecoveryMessageReceived(RecoveryMessage message)
    {
        // isRecovering is always set to false again after OnRecoveryMessageReceived
        isRecovering = true;
        int validChangeViews = 0, totalChangeViews = 0, validPrepReq = 0, totalPrepReq = 0;
        int validPrepResponses = 0, totalPrepResponses = 0, validCommits = 0, totalCommits = 0;

        DBFTPlugin.PluginLogger?.Information("OnRecoveryMessageReceived: height={BlockIndex} view={ViewNumber} index={ValidatorIndex}",
           message.BlockIndex, message.ViewNumber, message.ValidatorIndex);
        try
        {
            if (message.ViewNumber > context.ViewNumber)
            {
                if (context.CommitSent) return;
                ExtensiblePayload[] changeViewPayloads = message.GetChangeViewPayloads(context);
                totalChangeViews = changeViewPayloads.Length;
                foreach (ExtensiblePayload changeViewPayload in changeViewPayloads)
                    if (ReverifyAndProcessPayload(changeViewPayload)) validChangeViews++;
            }
            if (message.ViewNumber == context.ViewNumber && !context.NotAcceptingPayloadsDueToViewChanging && !context.CommitSent)
            {
                if (!context.RequestSentOrReceived)
                {
                    ExtensiblePayload? prepareRequestPayload = message.GetPrepareRequestPayload(context);
                    if (prepareRequestPayload != null)
                    {
                        totalPrepReq = 1;
                        if (ReverifyAndProcessPayload(prepareRequestPayload)) validPrepReq++;
                    }
                }
                ExtensiblePayload[] prepareResponsePayloads = message.GetPrepareResponsePayloads(context);
                totalPrepResponses = prepareResponsePayloads.Length;
                foreach (ExtensiblePayload prepareResponsePayload in prepareResponsePayloads)
                    if (ReverifyAndProcessPayload(prepareResponsePayload)) validPrepResponses++;
            }
            if (message.ViewNumber <= context.ViewNumber)
            {
                // Ensure we know about all commits from lower view numbers.
                ExtensiblePayload[] commitPayloads = message.GetCommitPayloadsFromRecoveryMessage(context);
                totalCommits = commitPayloads.Length;
                foreach (ExtensiblePayload commitPayload in commitPayloads)
                    if (ReverifyAndProcessPayload(commitPayload)) validCommits++;
            }
        }
        finally
        {
            DBFTPlugin.PluginLogger?.Information("Recovery finished: (valid/total) ChangeViews: {ValidChangeViews}/{TotalChangeViews} " +
                "PrepRequests: {ValidPrepReq}/{TotalPrepReq} PrepResponses: {ValidPrepResponses}/{TotalPrepResponses} Commits: {ValidCommits}/{TotalCommits}",
                validChangeViews, totalChangeViews, validPrepReq, totalPrepReq, validPrepResponses, totalPrepResponses, validCommits, totalCommits);
            isRecovering = false;
        }
    }

    private void OnRecoveryRequestReceived(ExtensiblePayload payload, ConsensusMessage message)
    {
        // We keep track of the payload hashes received in this block, and don't respond with recovery
        // in response to the same payload that we already responded to previously.
        // ChangeView messages include a Timestamp when the change view is sent, thus if a node restarts
        // and issues a change view for the same view, it will have a different hash and will correctly respond
        // again; however replay attacks of the ChangeView message from arbitrary nodes will not trigger an
        // additional recovery message response.
        if (!knownHashes.Add(payload.Hash)) return;

        DBFTPlugin.PluginLogger?.Information("OnRecoveryRequestReceived: height={BlockIndex} index={ValidatorIndex} view={ViewNumber}",
             message.BlockIndex, message.ValidatorIndex, message.ViewNumber);
        if (context.WatchOnly) return;
        if (!context.CommitSent)
        {
            bool shouldSendRecovery = false;
            int allowedRecoveryNodeCount = context.F + 1;
            // Limit recoveries to be sent from an upper limit of `f + 1` nodes
            for (int i = 1; i <= allowedRecoveryNodeCount; i++)
            {
                var chosenIndex = (message.ValidatorIndex + i) % context.Validators.Length;
                if (chosenIndex != context.MyIndex) continue;
                shouldSendRecovery = true;
                break;
            }

            if (!shouldSendRecovery) return;
        }
        localNode.Tell(new LocalNode.SendDirectly(context.MakeRecoveryMessage()));
    }
}
