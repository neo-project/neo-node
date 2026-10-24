// Copyright (C) 2015-2026 The Neo Project.
//
// ContractManifestJsonConverterTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Manifest;
using Newtonsoft.Json;
using System.Text.Json;

namespace Neo.Plugins.RestServer.Tests;

[TestClass]
public class ContractManifestJsonConverterTests
{
    [TestMethod]
    public void ManifestExtraPreservesNestedValues()
    {
        const string digest = "4201b02f571b7415121467d67343a8189b8070ad795a82424c0403782d22b1b4";
        using var result = SerializeManifest($$"""
            {
              "smartAccount": {
                "abiVersion": 2,
                "profileParameterDigest": "{{digest}}",
                "compositeVerifier": false
              },
              "MixedValues": [true, null, {"UpperCaseKey": "2026-10-09T00:00:00Z"}, [], {}, -1.5],
              "number": 9007199254740991,
              "text": "line\nbreak",
              "optional": null
            }
            """);

        var extra = result.RootElement.GetProperty("extra");
        var metadata = extra.GetProperty("smartAccount");
        Assert.AreEqual(JsonValueKind.Object, metadata.ValueKind);
        Assert.AreEqual(2d, metadata.GetProperty("abiVersion").GetDouble());
        Assert.AreEqual(digest, metadata.GetProperty("profileParameterDigest").GetString());
        Assert.IsFalse(metadata.GetProperty("compositeVerifier").GetBoolean());

        var values = extra.GetProperty("MixedValues");
        Assert.AreEqual(6, values.GetArrayLength());
        Assert.IsTrue(values[0].GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, values[1].ValueKind);
        Assert.AreEqual("2026-10-09T00:00:00Z", values[2].GetProperty("UpperCaseKey").GetString());
        Assert.AreEqual(0, values[3].GetArrayLength());
        Assert.AreEqual(JsonValueKind.Object, values[4].ValueKind);
        Assert.AreEqual(0, values[4].EnumerateObject().Count());
        Assert.AreEqual(-1.5d, values[5].GetDouble());
        Assert.AreEqual(9007199254740991d, extra.GetProperty("number").GetDouble());
        Assert.AreEqual("line\nbreak", extra.GetProperty("text").GetString());
        Assert.AreEqual(JsonValueKind.Null, extra.GetProperty("optional").ValueKind);
    }

    [TestMethod]
    public void ManifestExtraPreservesEmptyObject()
    {
        using var result = SerializeManifest("{}");
        var extra = result.RootElement.GetProperty("extra");
        Assert.AreEqual(JsonValueKind.Object, extra.ValueKind);
        Assert.AreEqual(0, extra.EnumerateObject().Count());
    }

    [TestMethod]
    public void ManifestExtraPreservesNull()
    {
        using var result = SerializeManifest("null");
        Assert.AreEqual(JsonValueKind.Null, result.RootElement.GetProperty("extra").ValueKind);
    }

    private static JsonDocument SerializeManifest(string extra)
    {
        var manifest = new ContractManifest
        {
            Name = "AccountManagement",
            Groups = [],
            SupportedStandards = [],
            Abi = new ContractAbi { Methods = [], Events = [] },
            Permissions = [],
            Trusts = WildcardContainer<ContractPermissionDescriptor>.Create(),
            Extra = (Neo.Json.JObject?)Neo.Json.JToken.Parse(extra)
        };
        return JsonDocument.Parse(JsonConvert.SerializeObject(manifest, RestServerSettings.Default.JsonSerializerSettings));
    }
}
