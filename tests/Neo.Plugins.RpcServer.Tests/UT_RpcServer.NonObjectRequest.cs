// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RpcServer.NonObjectRequest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace Neo.Plugins.RpcServer.Tests;

public partial class UT_RpcServer
{
    private async Task<string> ProcessRawAsync(string body)
    {
        using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes(body));
        using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Body = requestBody;
        context.Response.Body = responseBody;

        await _rpcServer.ProcessAsync(context);

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        return await reader.ReadToEndAsync();
    }

    private static void AssertResponse(string expected, JsonNode response)
    {
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expected), response), $"Expected: {expected}; actual: {response?.ToJsonString() ?? "null"}");
    }

    private static void AssertInvalidRequest(JsonNode response) =>
        AssertResponse("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid request"}}""", response);

    private static void AssertBlockCountResponse(int id, JsonNode response) =>
        AssertResponse($$"""{"jsonrpc":"2.0","id":{{id}},"result":1}""", response);

    [TestMethod]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("\"hello\"")]
    [DataRow("null")]
    public async Task TestProcessAsync_NonObjectSingleRequest(string body)
    {
        AssertInvalidRequest(JsonNode.Parse(await ProcessRawAsync(body)));
    }

    [TestMethod]
    [DataRow("[42]", 1)]
    [DataRow("[true, \"hello\", 42]", 3)]
    public async Task TestProcessAsync_BatchWithOnlyInvalidItems(string body, int count)
    {
        var array = Assert.IsInstanceOfType<JsonArray>(JsonNode.Parse(await ProcessRawAsync(body)));
        Assert.AreEqual(count, array.Count);
        foreach (var item in array) AssertInvalidRequest(item);
    }

    [TestMethod]
    [DataRow("42")]
    [DataRow("[42]")]
    public async Task TestProcessAsync_BatchWithInvalidAndValidItems(string invalidItem)
    {
        var response = await ProcessRawAsync($$"""[{{invalidItem}}, {"jsonrpc":"2.0","method":"getblockcount","params":[],"id":7}]""");
        var array = Assert.IsInstanceOfType<JsonArray>(JsonNode.Parse(response));
        Assert.AreEqual(2, array.Count);
        AssertInvalidRequest(array[0]);
        AssertBlockCountResponse(7, array[1]);
    }

    [TestMethod]
    public async Task TestProcessAsync_ValidSingleRequest()
    {
        var response = await ProcessRawAsync("""{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1}""");
        AssertBlockCountResponse(1, JsonNode.Parse(response));
    }

    [TestMethod]
    public async Task TestProcessAsync_ValidBatch()
    {
        var response = await ProcessRawAsync("""
            [{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1},
             {"jsonrpc":"2.0","method":"getblockcount","params":[],"id":2}]
            """);
        var array = Assert.IsInstanceOfType<JsonArray>(JsonNode.Parse(response));
        Assert.AreEqual(2, array.Count);
        AssertBlockCountResponse(1, array[0]);
        AssertBlockCountResponse(2, array[1]);
    }

    [TestMethod]
    public async Task TestProcessAsync_NotificationHasNoResponse()
    {
        Assert.AreEqual(string.Empty, await ProcessRawAsync("""{"jsonrpc":"2.0","method":"getblockcount","params":[]}"""));
    }

    [TestMethod]
    public async Task TestProcessAsync_BatchNotificationsOnlyHaveNoResponse()
    {
        Assert.AreEqual(string.Empty, await ProcessRawAsync("""[{"jsonrpc":"2.0","method":"getblockcount","params":[]}]"""));
    }

    [TestMethod]
    public async Task TestProcessAsync_MalformedJsonIsBadRequest()
    {
        var response = JsonNode.Parse(await ProcessRawAsync("{\"jsonrpc\":"));
        AssertResponse("""{"jsonrpc":"2.0","id":null,"error":{"code":-32700,"message":"Bad request"}}""", response);
    }

    [TestMethod]
    public async Task TestProcessAsync_BatchWithNullItemIsInvalidRequest()
    {
        var array = Assert.IsInstanceOfType<JsonArray>(JsonNode.Parse(await ProcessRawAsync("[null]")));
        Assert.AreEqual(1, array.Count);
        AssertInvalidRequest(array[0]);
    }
}
