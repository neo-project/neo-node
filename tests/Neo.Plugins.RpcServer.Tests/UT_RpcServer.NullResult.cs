// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RpcServer.NullResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using Neo.Json;

namespace Neo.Plugins.RpcServer.Tests;

public partial class UT_RpcServer
{
    private class NullResultHandler
    {
        [RpcMethod]
        public JToken SyncNull() => null;

        [RpcMethod]
        public Task<JToken> AsyncNull() => Task.FromResult<JToken>(null);

        [RpcMethod]
        public JToken SyncValue() => 42;

        [RpcMethod]
        public async Task<JToken> AsyncYieldNull()
        {
            await Task.Yield();
            return null;
        }

        [RpcMethod]
        public async Task<JToken> AsyncValue()
        {
            await Task.Yield();
            return "ok";
        }

        [RpcMethod]
        public JToken NullWithParam(string value = null) => value is null ? null : value;

        [RpcMethod]
        public int Unsupported() => 1;
    }

    private async Task<JObject> InvokeHandlerAsync(string method)
    {
        _rpcServer.RegisterMethods(new NullResultHandler());
        var request = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = method,
            ["params"] = new JArray()
        };
        var response = await _rpcServer.ProcessRequestAsync(new DefaultHttpContext(), request);
        Assert.IsNotNull(response);
        return response;
    }

    [TestMethod]
    public async Task TestProcessRequest_SyncNullResult()
    {
        var response = await InvokeHandlerAsync("syncnull");
        Assert.IsFalse(response.ContainsProperty("error"));
        Assert.IsTrue(response.ContainsProperty("result"));
        Assert.IsNull(response["result"]);
        StringAssert.Contains(response.ToString(), "\"result\":null");
    }

    [TestMethod]
    public async Task TestProcessRequest_AsyncNullResult()
    {
        var response = await InvokeHandlerAsync("asyncnull");
        Assert.IsFalse(response.ContainsProperty("error"));
        Assert.IsTrue(response.ContainsProperty("result"));
        Assert.IsNull(response["result"]);
        StringAssert.Contains(response.ToString(), "\"result\":null");
    }

    [TestMethod]
    public async Task TestProcessRequest_NonNullResult()
    {
        var response = await InvokeHandlerAsync("syncvalue");
        Assert.IsFalse(response.ContainsProperty("error"));
        Assert.AreEqual(42, response["result"]!.AsNumber());
    }

    [TestMethod]
    public async Task TestProcessRequest_UnsupportedReturnType()
    {
        var response = await InvokeHandlerAsync("unsupported");
        Assert.IsTrue(response.ContainsProperty("error"));
        Assert.IsFalse(response.ContainsProperty("result"));
        Assert.AreEqual(new NotSupportedException().HResult, (int)response["error"]!["code"]!.AsNumber());
    }

    [TestMethod]
    public async Task TestProcessRequest_AsyncNullResult_AfterYield()
    {
        var response = await InvokeHandlerAsync("asyncyieldnull");
        Assert.IsFalse(response.ContainsProperty("error"));
        Assert.IsTrue(response.ContainsProperty("result"));
        Assert.IsNull(response["result"]);
        StringAssert.Contains(response.ToString(), "\"result\":null");
    }

    [TestMethod]
    public async Task TestProcessRequest_AsyncNonNullResult()
    {
        var response = await InvokeHandlerAsync("asyncvalue");
        Assert.IsFalse(response.ContainsProperty("error"));
        Assert.AreEqual("ok", response["result"]!.AsString());
    }

    [TestMethod]
    public async Task TestProcessRequest_NullResult_PreservesEnvelope()
    {
        var response = await InvokeHandlerAsync("syncnull");
        Assert.AreEqual("2.0", response["jsonrpc"]!.AsString());
        Assert.AreEqual(1, response["id"]!.AsNumber());
    }

    [TestMethod]
    public async Task TestProcessRequest_NullResult_WithOptionalParameterOmitted()
    {
        var response = await InvokeHandlerAsync("nullwithparam");
        Assert.IsFalse(response.ContainsProperty("error"), response.ToString());
        Assert.IsNull(response["result"]);
        StringAssert.Contains(response.ToString(), "\"result\":null");
    }

    [TestMethod]
    public async Task TestProcessRequest_NullResult_InBatch()
    {
        _rpcServer.RegisterMethods(new NullResultHandler());
        var context = new DefaultHttpContext();
        var methods = new[] { "syncnull", "asyncnull", "syncvalue" };
        var tasks = methods.Select((m, i) => _rpcServer.ProcessRequestAsync(context, new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = i,
            ["method"] = m,
            ["params"] = new JArray()
        }));
        var results = await Task.WhenAll(tasks);

        Assert.IsTrue(results.All(r => r is not null && !r.ContainsProperty("error")));
        Assert.IsNull(results[0]!["result"]);
        Assert.IsNull(results[1]!["result"]);
        Assert.AreEqual(42, results[2]!["result"]!.AsNumber());
    }
}
