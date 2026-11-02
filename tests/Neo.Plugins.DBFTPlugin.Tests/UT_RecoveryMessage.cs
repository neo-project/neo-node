// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RecoveryMessage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.Plugins.DBFTPlugin.Consensus;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.Plugins.DBFTPlugin.Types;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;

namespace Neo.Plugins.DBFTPlugin.Tests;

[TestClass]
public class UT_RecoveryMessage
{
    [TestMethod]
    [DataRow(ChangeViewReason.Timeout)]
    [DataRow(ChangeViewReason.ChangeAgreement)]
    [DataRow(ChangeViewReason.TxNotFound)]
    [DataRow(ChangeViewReason.TxRejectedByPolicy)]
    [DataRow(ChangeViewReason.TxInvalid)]
    [DataRow(ChangeViewReason.BlockRejectedByPolicy)]
    public void RecoveryPreservesSignedChangeView(ChangeViewReason reason)
    {
        var key = new KeyPair(Enumerable.Repeat((byte)0x11, 32).ToArray());
        var committee = MockProtocolSettings.Default.StandbyCommittee.ToArray();
        committee[0] = key.PublicKey;
        var protocol = MockProtocolSettings.Default with { StandbyCommittee = committee };
        using var store = new MemoryStore();
        using var system = new NeoSystem(protocol, new MockMemoryStoreProvider(store));
        var wallet = new MockWallet(protocol);
        wallet.AddAccount(key.PublicKey);
        using var context = new ConsensusContext(system, MockBlockchain.CreateDefaultSettings(), wallet);
        context.Reset(0);
        Assert.IsGreaterThanOrEqualTo(0, context.MyIndex);
        var changeView = new ChangeView
        {
            BlockIndex = context.Block.Index,
            ValidatorIndex = (byte)context.MyIndex,
            ViewNumber = 0,
            Timestamp = 123,
            Reason = reason,
            RejectedHashes = reason is ChangeViewReason.TxInvalid or ChangeViewReason.TxRejectedByPolicy
                ? [UInt256.Zero, new UInt256(Enumerable.Repeat((byte)1, 32).ToArray())] : null
        };
        var original = context.CreatePayload(changeView);
        var signature = Crypto.Sign(original.GetSignData(protocol.Network), key.PrivateKey);
        using var builder = new ScriptBuilder();
        builder.EmitPush(signature);
        original.Witness = new Witness
        {
            InvocationScript = builder.ToArray(),
            VerificationScript = Contract.CreateSignatureRedeemScript(key.PublicKey)
        };
        context.ChangeViewPayloads[context.MyIndex] = original;
        context.Reset(1);
        var recoveryPayload = context.MakeRecoveryMessage();
        var recovery = (RecoveryMessage)ConsensusMessage.DeserializeFrom(recoveryPayload.Data);
        var compact = recovery.ChangeViewMessages.Values.Single();
        Assert.HasCount(((ISerializable)compact).Size, compact.ToArray());
        var reconstructed = recovery.GetChangeViewPayloads(context).Single();

        Assert.AreEqual(original.Hash, reconstructed.Hash);
        CollectionAssert.AreEqual(original.Data.ToArray(), reconstructed.Data.ToArray());
        CollectionAssert.AreEqual(original.Witness.ToArray(), reconstructed.Witness.ToArray());
        Assert.IsTrue(Crypto.VerifySignature(reconstructed.GetSignData(protocol.Network), signature, key.PublicKey));
        var restored = (ChangeView)ConsensusMessage.DeserializeFrom(reconstructed.Data);
        Assert.AreEqual(reason, restored.Reason);
        if (changeView.RejectedHashes != null)
            CollectionAssert.AreEqual(changeView.RejectedHashes, restored.RejectedHashes);
    }

    [TestMethod]
    public void TestDeserializeWithPrepareRequest()
    {
        var req = new PrepareRequest
        {
            BlockIndex = 5,
            ValidatorIndex = 1,
            ViewNumber = 2,
            Version = 0,
            PrevHash = UInt256.Zero,
            Timestamp = 123,
            Nonce = 456,
            TransactionHashes = [UInt256.Zero]
        };
        var recovery = new RecoveryMessage
        {
            BlockIndex = 5,
            ValidatorIndex = 3,
            ViewNumber = 2,
            ChangeViewMessages = [],
            PrepareRequestMessage = req,
            PreparationMessages = [],
            CommitMessages = []
        };
        var data = recovery.ToArray();

        var res = (RecoveryMessage)ConsensusMessage.DeserializeFrom(data);
        Assert.IsNotNull(res.PrepareRequestMessage);
        Assert.AreEqual(ConsensusMessageType.PrepareRequest, res.PrepareRequestMessage.Type);
        Assert.IsNull(res.PreparationHash);
        CollectionAssert.AreEqual(req.ToArray(), res.PrepareRequestMessage.ToArray());
        CollectionAssert.AreEqual(data, res.ToArray());
    }
}
