// Copyright (C) 2015-2026 The Neo Project.
//
// UT_Nep17Keys.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Plugins.Trackers.NEP_17;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_Nep17Keys
{
    [TestMethod]
    public void TransferKeyComparisonOrdersByTimestampAndNotificationIndex()
    {
        var baseline = new Nep17TransferKey(Address(1), 10, Address(2), 3);
        var laterTimestamp = new Nep17TransferKey(Address(1), 11, Address(2), 3);
        var laterNotification = new Nep17TransferKey(Address(1), 10, Address(2), 4);

        Assert.IsLessThan(0, baseline.CompareTo(laterTimestamp));
        Assert.IsGreaterThan(0, laterTimestamp.CompareTo(baseline));
        Assert.IsLessThan(0, baseline.CompareTo(laterNotification));
        Assert.AreEqual(1, baseline.CompareTo(null));
        Assert.AreEqual(0, baseline.CompareTo(baseline));
    }

    [TestMethod]
    public void TransferKeyEqualityAgreesWithComparison()
    {
        var keys = new[]
        {
            new Nep17TransferKey(Address(1), 10, Address(2), 3),
            new Nep17TransferKey(Address(1), 10, Address(3), 3),
            new Nep17TransferKey(Address(2), 10, Address(2), 3),
            new Nep17TransferKey(Address(1), 12, Address(2), 3),
            new Nep17TransferKey(Address(1), 10, Address(2), 5),
        };

        for (int left = 0; left < keys.Length; left++)
        {
            for (int right = 0; right < keys.Length; right++)
            {
                bool equal = left == right;
                Assert.AreEqual(equal, keys[left].Equals(keys[right]));
                Assert.AreEqual(equal, keys[left].Equals((object)keys[right]));
                Assert.AreEqual(keys[left].CompareTo(keys[right]) == 0, equal);
                Assert.AreEqual(
                    Math.Sign(keys[left].CompareTo(keys[right])),
                    -Math.Sign(keys[right].CompareTo(keys[left])));
                if (equal)
                    Assert.AreEqual(keys[left].GetHashCode(), keys[right].GetHashCode());
            }
        }

        Assert.IsFalse(keys[0].Equals(null));
        Assert.IsFalse(keys[0].Equals((object?)null));
        Assert.IsTrue(keys[0].Equals(new Nep17TransferKey(Address(1), 10, Address(2), 3)));
        Assert.HasCount(keys.Length, new SortedSet<Nep17TransferKey>(keys));
    }

    [TestMethod]
    public void TransferKeySerializationRoundTrips()
    {
        var key = new Nep17TransferKey(Address(1), 0x0102030405060708, Address(2), 9);
        var restored = RoundTrip<Nep17TransferKey>(key);

        Assert.AreEqual(key.UserScriptHash, restored.UserScriptHash);
        Assert.AreEqual(key.TimestampMS, restored.TimestampMS);
        Assert.AreEqual(key.AssetScriptHash, restored.AssetScriptHash);
        Assert.AreEqual(key.BlockXferNotificationIndex, restored.BlockXferNotificationIndex);
        Assert.IsTrue(key.Equals(restored));
    }

    [TestMethod]
    public void BalanceKeyComparisonOrdersByUserThenAsset()
    {
        var baseline = new Nep17BalanceKey(Address(1), Address(2));
        var same = new Nep17BalanceKey(Address(1), Address(2));
        var otherAsset = new Nep17BalanceKey(Address(1), Address(3));
        var otherUser = new Nep17BalanceKey(Address(2), Address(2));

        Assert.AreEqual(0, baseline.CompareTo(same));
        Assert.AreEqual(1, baseline.CompareTo(null));
        Assert.AreEqual(0, baseline.CompareTo(baseline));
        Assert.AreNotEqual(0, baseline.CompareTo(otherAsset));
        Assert.AreNotEqual(0, baseline.CompareTo(otherUser));
        Assert.AreEqual(
            Math.Sign(baseline.CompareTo(otherAsset)),
            -Math.Sign(otherAsset.CompareTo(baseline)));
        Assert.AreEqual(
            Math.Sign(otherUser.CompareTo(otherAsset)),
            -Math.Sign(otherAsset.CompareTo(otherUser)));
    }

    [TestMethod]
    public void BalanceKeyEqualityAndSerializationRoundTrip()
    {
        var key = new Nep17BalanceKey(Address(1), Address(2));
        var same = new Nep17BalanceKey(Address(1), Address(2));
        var other = new Nep17BalanceKey(Address(1), Address(3));

        Assert.IsTrue(key.Equals(same));
        Assert.IsTrue(key.Equals((object)same));
        Assert.IsFalse(key.Equals(other));
        Assert.IsFalse(key.Equals(null));
        Assert.IsFalse(key.Equals((object?)null));
        Assert.AreEqual(key.GetHashCode(), same.GetHashCode());

        var restored = RoundTrip<Nep17BalanceKey>(key);
        Assert.AreEqual(key.UserScriptHash, restored.UserScriptHash);
        Assert.AreEqual(key.AssetScriptHash, restored.AssetScriptHash);
        Assert.IsTrue(key.Equals(restored));
        Assert.HasCount(1, new SortedSet<Nep17BalanceKey>(new[] { key, same }));
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
