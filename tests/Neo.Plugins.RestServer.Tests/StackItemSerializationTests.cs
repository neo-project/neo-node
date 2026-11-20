// Copyright (C) 2015-2026 The Neo Project.
//
// StackItemSerializationTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.RestServer.Tests;

[TestClass]
public class StackItemSerializationTests
{
    private static JToken Convert(Array item) =>
        RestServerUtility.StackItemToJToken(item, null, new JsonSerializer());

    [TestMethod]
    public void SelfReferencingArrayThrows()
    {
        var array = new Array();
        array.Add(array);

        Assert.ThrowsExactly<InvalidOperationException>(() => Convert(array));
    }

    [TestMethod]
    public void SharedArrayProducesBothValues()
    {
        var shared = new Array();
        shared.Add(new Integer(7));
        var array = new Array();
        array.Add(shared);
        array.Add(shared);

        var result = Convert(array);

        Assert.AreEqual("Array", result["Type"]?.Value<string>());
        Assert.IsTrue(JToken.DeepEquals(result["Value"]?[0], result["Value"]?[1]));
        Assert.AreEqual("7", result["Value"]?[0]?["Value"]?[0]?["Value"]?.ToString());
    }

    [TestMethod]
    public void DeeplySharedArrayIsBounded()
    {
        var array = new Array();
        for (var i = 0; i < 18; i++)
        {
            var parent = new Array();
            parent.Add(array);
            parent.Add(array);
            array = parent;
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => Convert(array));
    }

    [TestMethod]
    public void DeeplyNestedArrayIsBounded()
    {
        var array = new Array();
        for (var i = 0; i < 65; i++)
        {
            var parent = new Array();
            parent.Add(array);
            array = parent;
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => Convert(array));
    }
}
