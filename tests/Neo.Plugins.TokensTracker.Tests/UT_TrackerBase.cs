// Copyright (C) 2015-2026 The Neo Project.
//
// UT_TrackerBase.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.Trackers;
using Neo.Plugins.Trackers.NEP_17;
using Neo.SmartContract;
using Neo.VM.Types;
using Neo.Wallets;
using System.Numerics;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_TrackerBase
{
    [TestMethod]
    public void TransferRecordsRejectMalformedNotifications()
    {
        Assert.IsNull(Parser.Parse(new Array()));
        Assert.IsNull(Parser.Parse(State(new Integer(1), Bytes(20), new Integer(1))));
        Assert.IsNull(Parser.Parse(State(Bytes(20), new Integer(1), new Integer(1))));
        Assert.IsNull(Parser.Parse(State(Bytes(20), Bytes(20), new Array())));
        Assert.IsNull(Parser.Parse(State(Bytes(32), Bytes(20), new Integer(1))));
        Assert.IsNull(Parser.Parse(State(Bytes(20), Bytes(32), new Integer(1))));
        Assert.IsNull(Parser.Parse(State(StackItem.Null, StackItem.Null, new Integer(1))));
    }

    [TestMethod]
    public void TransferRecordsRejectUnsupportedPayloadShapes()
    {
        var extraItem = State(Bytes(20), Bytes(20), new Integer(1));
        extraItem.Add(new ByteString(new byte[] { 1 }));
        extraItem.Add(new ByteString(new byte[] { 2 }));
        Assert.IsNull(Parser.Parse(extraItem));

        var invalidTokenId = State(Bytes(20), Bytes(20), new Integer(1));
        invalidTokenId.Add(new Integer(1));
        Assert.IsNull(Parser.Parse(invalidTokenId));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(63)]
    [DataRow(64)]
    public void TransferRecordsAcceptTokenIdsWithinTheStorageKeyLimit(int size)
    {
        var token = Enumerable.Repeat((byte)0x5a, size).ToArray();
        var state = State(Bytes(20), Bytes(20), new Integer(7));
        state.Add(new ByteString(token));

        var record = Parser.Parse(state);

        Assert.IsNotNull(record);
        CollectionAssert.AreEqual(token, record.tokenId);
    }

    [TestMethod]
    public void TransferRecordsRejectTokenIdsAboveTheStorageKeyLimit()
    {
        var state = State(Bytes(20), Bytes(20), new Integer(7));
        state.Add(new ByteString(new byte[ApplicationEngine.MaxStorageKeySize + 1]));

        Assert.IsNull(Parser.Parse(state));
    }

    [TestMethod]
    public void StorageHelpersRoundTripAndDeleteTransfers()
    {
        using var store = new MemoryStore();
        using var tracker = new FakeTracker(store, null);
        var user = Address(1);
        var key = new Nep17TransferKey(user, 5, Address(2), 1);
        var value = new TokenTransfer
        {
            UserScriptHash = user,
            BlockIndex = 9,
            TxHash = UInt256.Zero,
            Amount = 42,
        };

        tracker.ResetBatch();
        tracker.PutRecord(0xea, key, value);
        tracker.Commit();

        var stored = tracker.Query<Nep17TransferKey, TokenTransfer>(0xea, user, 0, ulong.MaxValue).ToArray();
        Assert.HasCount(1, stored);
        Assert.IsTrue(key.Equals(stored[0].key));
        Assert.AreEqual(new BigInteger(42), stored[0].val.Amount);
        Assert.AreEqual(9u, stored[0].val.BlockIndex);

        Assert.IsEmpty(tracker.Query<Nep17TransferKey, TokenTransfer>(0xea, Address(3), 0, ulong.MaxValue));

        tracker.ResetBatch();
        tracker.DeleteRecord(0xea, key);
        tracker.Commit();

        Assert.IsEmpty(tracker.Query<Nep17TransferKey, TokenTransfer>(0xea, user, 0, ulong.MaxValue));
    }

    [TestMethod]
    public void TransferJsonReportsZeroAddressAsNull()
    {
        using var store = new MemoryStore();
        using var system = CreateSystem();
        using var tracker = new FakeTracker(store, system);
        var key = new Nep17TransferKey(Address(2), 1234, Address(3), 4);
        var value = new TokenTransfer
        {
            UserScriptHash = UInt160.Zero,
            BlockIndex = 7,
            TxHash = UInt256.Zero,
            Amount = 99,
        };

        var withoutUser = tracker.ToJsonObject(key, value);
        Assert.AreEqual(1234, withoutUser["timestamp"]!.AsNumber());
        Assert.AreEqual(Address(3).ToString(), withoutUser["assethash"]!.AsString());
        Assert.IsNull(withoutUser["transferaddress"]?.AsString());
        Assert.AreEqual("99", withoutUser["amount"]!.AsString());
        Assert.AreEqual(7, withoutUser["blockindex"]!.AsNumber());
        Assert.AreEqual(4, withoutUser["transfernotifyindex"]!.AsNumber());
        Assert.AreEqual(UInt256.Zero.ToString(), withoutUser["txhash"]!.AsString());

        value.UserScriptHash = Address(5);
        var withUser = tracker.ToJsonObject(key, value);
        Assert.AreEqual(Address(5).ToAddress(system.Settings.AddressVersion), withUser["transferaddress"]!.AsString());
    }

    private static NeoSystem CreateSystem()
    {
        var settings = ProtocolSettings.Default with
        {
            StandbyCommittee = System.Collections.Immutable.ImmutableArray.Create(Neo.Cryptography.ECC.ECCurve.Secp256r1.G),
            ValidatorsCount = 1,
        };
        return new NeoSystem(settings, new MemoryProvider());
    }

    private static Array State(StackItem from, StackItem to, StackItem amount)
    {
        return new Array { from, to, amount };
    }

    private static ByteString Bytes(int length)
    {
        return new ByteString(Enumerable.Repeat((byte)0x11, length).ToArray());
    }

    private static UInt160 Address(byte value)
    {
        return new(Enumerable.Repeat(value, UInt160.Length).ToArray());
    }

    private sealed class Parser() : TrackerBase(null!, 0, false, null!)
    {
        public static TransferRecord? Parse(Array state) => GetTransferRecord(UInt160.Zero, state);

        public override string TrackName => "Parser";

        public override void OnPersist(NeoSystem system, Block block, DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> executions) => throw new NotSupportedException();
    }

    private sealed class FakeTracker(IStore store, NeoSystem? system) : TrackerBase(store, 100, true, system!)
    {
        public override string TrackName => nameof(FakeTracker);

        public void PutRecord<TKey, TValue>(byte prefix, TKey key, TValue value)
            where TKey : Neo.IO.ISerializable, new()
            where TValue : class, Neo.IO.ISerializable, new() => Put(prefix, key, value);

        public void DeleteRecord<TKey>(byte prefix, TKey key)
            where TKey : Neo.IO.ISerializable, new() => Delete(prefix, key);

        public IEnumerable<(TKey key, TValue val)> Query<TKey, TValue>(byte prefix, UInt160 user, ulong start, ulong end)
            where TKey : Neo.IO.ISerializable, new()
            where TValue : class, Neo.IO.ISerializable, new() => QueryTransfers<TKey, TValue>(prefix, user, start, end);

        public JObject ToJsonObject(TokenTransferKey key, TokenTransfer value) => ToJson(key, value);

        public override void OnPersist(NeoSystem system, Block block, DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> executions) => throw new NotSupportedException();
    }

    private sealed class MemoryProvider : IStoreProvider
    {
        public string Name => "TokensTrackerBaseTest";

        public IStore GetStore(string? path) => new MemoryStore();
    }
}
