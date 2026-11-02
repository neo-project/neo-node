// Copyright (C) 2015-2026 The Neo Project.
//
// UT_Nep11TransferKey.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Plugins.Trackers.NEP_11;
using Neo.VM.Types;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_Nep11TransferKey
{
    [TestMethod]
    [DataRow(33)]
    [DataRow(64)]
    public void LongTokenIdsCanBeCompared(int size)
    {
        var bytes = new byte[size];
        var first = Key(bytes);
        var equal = Key(bytes.ToArray());
        bytes = bytes.ToArray();
        bytes[^1] = 1;
        var second = Key(bytes);
        Assert.AreEqual(0, first.CompareTo(equal));
        Assert.IsTrue(first.Equals(equal));
        Assert.IsLessThan(0, first.CompareTo(second));
        Assert.IsGreaterThan(0, second.CompareTo(first));
    }

    [TestMethod]
    public void ByteDistinctTokenIdsDoNotCollapseInSortedCollections()
    {
        byte[][] tokens = [[], [0], [0, 0], [1], [1, 0], [0x80], [0xff], [0xff, 0xff]];
        var keys = tokens.Select(Key).ToArray();
        Assert.HasCount(keys.Length, new SortedSet<Nep11TransferKey>(keys));
        foreach (var left in keys)
            foreach (var right in keys)
                Assert.AreEqual(left.Equals(right), left.CompareTo(right) == 0);
    }

    private static Nep11TransferKey Key(byte[] token) => new(UInt160.Zero, 0, UInt160.Zero, new ByteString(token), 0);
}
