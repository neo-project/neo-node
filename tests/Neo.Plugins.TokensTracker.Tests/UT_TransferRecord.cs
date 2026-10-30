// Copyright (C) 2015-2026 The Neo Project.
//
// UT_TransferRecord.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.Trackers;
using Neo.VM.Types;
using System.Numerics;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_TransferRecord
{
    [TestMethod]
    [DataRow(3, 33)]
    [DataRow(3, 1024)]
    [DataRow(4, 33)]
    [DataRow(4, 1024)]
    public void OversizedAmountIsIgnoredAndNextTransferIsAccepted(int count, int size)
    {
        Assert.IsNull(Parser.Parse(State(count, new ByteString(new byte[size]))));
        var record = Parser.Parse(State(count, new Integer(500)));
        Assert.IsNotNull(record);
        Assert.AreEqual(new BigInteger(500), record.amount);
        Assert.AreEqual(count == 4, record.tokenId is not null);
    }

    [TestMethod]
    [DataRow(3, 0)]
    [DataRow(3, 1)]
    [DataRow(3, 32)]
    [DataRow(4, 0)]
    [DataRow(4, 1)]
    [DataRow(4, 32)]
    public void ValidByteStringAmountsRemainSupported(int count, int size)
    {
        var bytes = Enumerable.Repeat((byte)0x7f, size).ToArray();
        var record = Parser.Parse(State(count, new ByteString(bytes)));
        Assert.IsNotNull(record);
        Assert.AreEqual(new BigInteger(bytes), record.amount);
    }

    [TestMethod]
    [DataRow(33)]
    [DataRow(64)]
    public void LongTokenIdsArePreserved(int size)
    {
        var state = State(4, new Integer(1));
        var token = Enumerable.Repeat((byte)0xab, size).ToArray();
        state[3] = new ByteString(token);
        var record = Parser.Parse(state);
        Assert.IsNotNull(record);
        CollectionAssert.AreEqual(token, record.tokenId);
    }

    private static Array State(int count, StackItem amount)
    {
        var state = new Array { StackItem.Null, new ByteString(new byte[UInt160.Length]), amount };
        if (count == 4) state.Add(new ByteString(new byte[] { 1 }));
        return state;
    }

    private sealed class Parser() : TrackerBase(null!, 0, false, null!)
    {
        public static TransferRecord? Parse(Array state) => GetTransferRecord(UInt160.Zero, state);
        public override string TrackName => "Parser";
        public override void OnPersist(NeoSystem system, Block block, DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> executions) => throw new NotSupportedException();
    }
}
