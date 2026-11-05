// Copyright (C) 2015-2026 The Neo Project.
//
// UT_TokenRecords.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Plugins.Trackers;
using System.Numerics;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_TokenRecords
{
    [TestMethod]
    [DataRow("0")]
    [DataRow("42")]
    [DataRow("-12345678901234567890")]
    public void TransfersRoundTripThroughStorage(string amount)
    {
        var value = new TokenTransfer
        {
            UserScriptHash = Address(3),
            BlockIndex = 17,
            TxHash = UInt256.Zero,
            Amount = BigInteger.Parse(amount),
        };

        var restored = RoundTrip<TokenTransfer>(value);

        Assert.AreEqual(value.UserScriptHash, restored.UserScriptHash);
        Assert.AreEqual(value.BlockIndex, restored.BlockIndex);
        Assert.AreEqual(value.TxHash, restored.TxHash);
        Assert.AreEqual(value.Amount, restored.Amount);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("100000000000")]
    [DataRow("-1")]
    public void BalancesRoundTripThroughStorage(string balance)
    {
        var value = new TokenBalance
        {
            Balance = BigInteger.Parse(balance),
            LastUpdatedBlock = 23,
        };

        var restored = RoundTrip<TokenBalance>(value);

        Assert.AreEqual(value.Balance, restored.Balance);
        Assert.AreEqual(value.LastUpdatedBlock, restored.LastUpdatedBlock);
    }

    private static T RoundTrip<T>(T value) where T : ISerializable, new()
    {
        var bytes = value.ToArray();
        Assert.AreEqual(value.Size, bytes.Length);
        var restored = new T();
        var reader = new MemoryReader(bytes);
        restored.Deserialize(ref reader);
        return restored;
    }

    private static UInt160 Address(byte value)
    {
        return new(Enumerable.Repeat(value, UInt160.Length).ToArray());
    }
}
