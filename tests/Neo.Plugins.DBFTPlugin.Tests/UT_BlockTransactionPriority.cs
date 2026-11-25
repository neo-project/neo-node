// Copyright (C) 2015-2026 The Neo Project.
//
// UT_BlockTransactionPriority.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.

using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Consensus;

namespace Neo.Plugins.DBFTPlugin.Tests;

[TestClass]
public class UT_BlockTransactionPriority
{
    [TestMethod]
    public void CommitteeSignersComeFirstBeforeLimitingTheBlock()
    {
        var committee = UInt160.Zero;
        var other = new UInt160(Enumerable.Repeat((byte)1, UInt160.Length).ToArray());
        var regularHighFee = new Transaction { NetworkFee = 400, Signers = [new Signer { Account = other }], Attributes = [], Witnesses = [] };
        var committeeHighFee = new Transaction { NetworkFee = 300, Signers = [new Signer { Account = committee }], Attributes = [], Witnesses = [] };
        var regularLowFee = new Transaction { NetworkFee = 200, Signers = [new Signer { Account = other }], Attributes = [], Witnesses = [] };
        var committeeLowFee = new Transaction
        {
            NetworkFee = 100,
            Signers = [new Signer { Account = other }, new Signer { Account = committee }],
            Attributes = [],
            Witnesses = []
        };
        Transaction[] sortedByFee = [regularHighFee, committeeHighFee, regularLowFee, committeeLowFee];

        CollectionAssert.AreEqual(
            new[] { committeeHighFee, committeeLowFee, regularHighFee, regularLowFee },
            ConsensusContext.SelectTransactionsForBlock(sortedByFee, committee, 4));
        CollectionAssert.AreEqual(
            new[] { committeeHighFee, committeeLowFee },
            ConsensusContext.SelectTransactionsForBlock(sortedByFee, committee, 2));
    }
}
