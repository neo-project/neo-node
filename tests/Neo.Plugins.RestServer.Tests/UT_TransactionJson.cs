// Copyright (C) 2015-2026 The Neo Project.
//
// UT_TransactionJson.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.VM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Tests;

[TestClass]
public class UT_TransactionJson
{
    [TestMethod]
    public void TransactionScript_IsBase64OfScriptBytes()
    {
        byte[] script = [(byte)OpCode.PUSH1, (byte)OpCode.RET];
        var tx = new Transaction
        {
            Signers = [new() { Account = UInt160.Zero }],
            Attributes = [],
            Script = script,
            Witnesses = [],
        };

        var serializer = JsonSerializer.Create(RestServerSettings.Default.JsonSerializerSettings);
        var json = (JObject)RestServerUtility.TransactionToJToken(tx, serializer);

        Assert.AreEqual(JTokenType.String, json["script"]!.Type);
        Assert.AreEqual(Convert.ToBase64String(script), json["script"]!.Value<string>());
    }
}
