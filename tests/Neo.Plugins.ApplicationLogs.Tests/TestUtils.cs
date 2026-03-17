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

using Neo.Json;
using Neo.Wallets.NEP6;

namespace Neo.Plugins.ApplicationsLogs.Tests;

public static partial class TestUtils
{
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
        return new NEP6Wallet(null!, password, TestProtocolSettings.Default, wallet);
    }
}
