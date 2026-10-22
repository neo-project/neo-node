// Copyright (C) 2015-2026 The Neo Project.
//
// UT_PrepareRequest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.VM;

namespace Neo.Plugins.DBFTPlugin.Tests;

[TestClass]
public class UT_PrepareRequest
{
    private const uint ExtensionHeight = 10;

    private static bool IsExtended(uint blockIndex) => blockIndex >= ExtensionHeight;

    private static Transaction CreateTransaction(uint nonce) => new()
    {
        Version = 0,
        Nonce = nonce,
        SystemFee = 1,
        NetworkFee = 2,
        ValidUntilBlock = 100,
        Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.CalledByEntry }],
        Attributes = [],
        Script = new[] { (byte)OpCode.PUSH1 },
        Witnesses = [new Witness { InvocationScript = new byte[] { 1 }, VerificationScript = new byte[] { 2 } }]
    };

    private static PrepareRequest CreateRequest(uint blockIndex, Transaction[] txs)
    {
        var req = new PrepareRequest
        {
            BlockIndex = blockIndex,
            ValidatorIndex = 1,
            ViewNumber = 2,
            Version = 0,
            PrevHash = UInt256.Zero,
            Timestamp = 123,
            Nonce = 456,
        };
        if (IsExtended(blockIndex))
        {
            req.Extended = true;
            req.Transactions = txs;
        }
        else
        {
            req.TransactionHashes = txs.Select(p => p.Hash).ToArray();
        }
        return req;
    }

    [TestMethod]
    public void TestLegacyFormat()
    {
        var txs = new[] { CreateTransaction(1), CreateTransaction(2) };
        var req = CreateRequest(ExtensionHeight - 1, txs);
        var data = req.ToArray();
        Assert.AreEqual(req.Size, data.Length);

        var res = (PrepareRequest)ConsensusMessage.DeserializeFrom(data, IsExtended);
        Assert.IsFalse(res.Extended);
        Assert.IsNull(res.Transactions);
        CollectionAssert.AreEqual(txs.Select(p => p.Hash).ToArray(), res.TransactionHashes);
        CollectionAssert.AreEqual(res.TransactionHashes, res.GetTransactionHashes());
        CollectionAssert.AreEqual(data, res.ToArray());
    }

    [TestMethod]
    public void TestExtendedFormat()
    {
        var txs = new[] { CreateTransaction(1), CreateTransaction(2) };
        var req = CreateRequest(ExtensionHeight, txs);
        var data = req.ToArray();
        Assert.AreEqual(req.Size, data.Length);

        var res = (PrepareRequest)ConsensusMessage.DeserializeFrom(data, IsExtended);
        Assert.IsTrue(res.Extended);
        Assert.IsNull(res.TransactionHashes);
        Assert.HasCount(txs.Length, res.Transactions);
        CollectionAssert.AreEqual(txs.Select(p => p.Hash).ToArray(), res.GetTransactionHashes());
        CollectionAssert.AreEqual(data, res.ToArray());

        var legacy = (PrepareRequest)ConsensusMessage.DeserializeFrom(data);
        Assert.IsFalse(legacy.Extended);
        CollectionAssert.AreNotEqual(txs.Select(p => p.Hash).ToArray(), legacy.TransactionHashes);
    }

    [TestMethod]
    public void TestExtendedDuplicateTransactions()
    {
        var tx = CreateTransaction(1);
        var data = CreateRequest(ExtensionHeight, [tx, tx]).ToArray();
        Assert.ThrowsExactly<FormatException>(() => _ = ConsensusMessage.DeserializeFrom(data, IsExtended));
    }

    [TestMethod]
    public void TestRecoveryMessage()
    {
        foreach (var blockIndex in new[] { ExtensionHeight - 1, ExtensionHeight })
        {
            var req = CreateRequest(blockIndex, [CreateTransaction(1)]);
            var recovery = new RecoveryMessage
            {
                BlockIndex = blockIndex,
                ValidatorIndex = 3,
                ViewNumber = 2,
                ChangeViewMessages = [],
                PrepareRequestMessage = req,
                PreparationMessages = [],
                CommitMessages = []
            };
            var data = recovery.ToArray();

            var res = (RecoveryMessage)ConsensusMessage.DeserializeFrom(data, IsExtended);
            Assert.AreEqual(IsExtended(blockIndex), res.PrepareRequestMessage.Extended);
            CollectionAssert.AreEqual(req.ToArray(), res.PrepareRequestMessage.ToArray());
            CollectionAssert.AreEqual(data, res.ToArray());
        }
    }

    [TestMethod]
    public void TestRecoveryMessageWrongBlockIndex()
    {
        var recovery = new RecoveryMessage
        {
            BlockIndex = ExtensionHeight,
            ChangeViewMessages = [],
            PrepareRequestMessage = CreateRequest(ExtensionHeight - 1, [CreateTransaction(1)]),
            PreparationMessages = [],
            CommitMessages = []
        };
        var data = recovery.ToArray();
        Assert.ThrowsExactly<FormatException>(() => _ = ConsensusMessage.DeserializeFrom(data, IsExtended));
    }
}
