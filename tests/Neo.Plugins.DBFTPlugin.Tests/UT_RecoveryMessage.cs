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

using Neo.Extensions;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.Plugins.DBFTPlugin.Types;

namespace Neo.Plugins.DBFTPlugin.Tests;

[TestClass]
public class UT_RecoveryMessage
{
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
