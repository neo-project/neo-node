// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ChangeView.cs file belongs to the neo project and is free
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
public class UT_ChangeView
{
    [TestMethod]
    public void DeserializePreservesRejectedHashesLimit()
    {
        foreach (var reason in new[] { ChangeViewReason.TxInvalid, ChangeViewReason.TxRejectedByPolicy })
        {
            var message = new ChangeView { Reason = reason };
            foreach (var count in new[] { 0, 1, 2, 65535, 65536 })
            {
                message.RejectedHashes = Enumerable.Repeat(UInt256.Zero, count).ToArray();
                if (count <= ushort.MaxValue)
                {
                    var decoded = (ChangeView)ConsensusMessage.DeserializeFrom(message.ToArray());
                    CollectionAssert.AreEqual(message.RejectedHashes, decoded.RejectedHashes);
                }
                else
                {
                    Assert.ThrowsExactly<FormatException>(() => ConsensusMessage.DeserializeFrom(message.ToArray()));
                }
            }
        }
    }
}

