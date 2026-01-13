// Copyright (C) 2015-2026 The Neo Project.
//
// TestUtils.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using Neo.Json;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets.NEP6;

namespace Neo.Plugins.RpcServer.Tests;

public static partial class TestUtils
{
    public static readonly Random TestRandom = new(1337); // use fixed seed for guaranteed determinism

    public static UInt256 RandomUInt256()
    {
        var data = new byte[32];
        TestRandom.NextBytes(data);
        return new UInt256(data);
    }

    public static UInt160 RandomUInt160()
    {
        var data = new byte[20];
        TestRandom.NextBytes(data);
        return new UInt160(data);
    }

    public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, ISerializableSpan key = null)
    {
        var k = new KeyBuilder(contract.Id, prefix);
        if (key != null) k = k.Add(key);
        return k;
    }

    public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, uint value)
    {
        return new KeyBuilder(contract.Id, prefix).AddBigEndian(value);
    }

    public static NEP6Wallet GenerateTestWallet(string password)
    {
        var wallet = new JObject()
        {
            ["name"] = "noname",
            ["version"] = new Version("1.0").ToString(),
            ["scrypt"] = new ScryptParameters(2, 1, 1).ToJson(),
            ["accounts"] = new JArray(),
            ["extra"] = null
        };
        Assert.AreEqual("{\"name\":\"noname\",\"version\":\"1.0\",\"scrypt\":{\"n\":2,\"r\":1,\"p\":1},\"accounts\":[],\"extra\":null}", wallet.ToString());
        return new NEP6Wallet(null, password, TestProtocolSettings.Default, wallet);
    }

    public static void StorageItemAdd(DataCache snapshot, int id, byte[] keyValue, byte[] value)
    {
        snapshot.Add(new StorageKey
        {
            Id = id,
            Key = keyValue
        }, new StorageItem(value));
    }
}
