// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ErrorReportDispatcher.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;
using System.Text.Json;

namespace Neo.Plugins.ErrorReporting.Tests;

[TestClass]
public class UT_ErrorReportDispatcher
{
    [TestMethod]
    public async Task SendBatchAsync_PostsReportPayload()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = ErrorReportingSettings.Create(
            new Uri("https://collector.example/v1/events"),
            new Dictionary<string, string> { ["X-Api-Key"] = "secret" },
            environment: "unit-test",
            nodeName: "node-1");
        using var dispatcher = new ErrorReportDispatcher(settings, client);
        var report = ErrorReport.FromException("UnhandledException", new InvalidOperationException("boom"), true, settings, null);

        var sent = await dispatcher.SendBatchAsync([report], CancellationToken.None);

        Assert.IsTrue(sent);
        Assert.AreEqual(HttpMethod.Post, handler.Request.Method);
        Assert.AreEqual("https://collector.example/v1/events", handler.Request.RequestUri!.ToString());
        Assert.IsTrue(handler.Request.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.AreEqual("secret", values.Single());

        using var document = JsonDocument.Parse(handler.Body);
        Assert.AreEqual("neo-node", document.RootElement.GetProperty("serviceName").GetString());
        Assert.AreEqual("unit-test", document.RootElement.GetProperty("environment").GetString());
        Assert.AreEqual("node-1", document.RootElement.GetProperty("nodeName").GetString());
        var item = document.RootElement.GetProperty("reports")[0];
        Assert.AreEqual("UnhandledException", item.GetProperty("eventType").GetString());
        Assert.AreEqual("fatal", item.GetProperty("severity").GetString());
        Assert.AreEqual("boom", item.GetProperty("message").GetString());
    }

    [TestMethod]
    public void TryEnqueue_ReturnsFalseWhenQueueIsFull()
    {
        var settings = ErrorReportingSettings.Create(
            new Uri("https://collector.example/v1/events"),
            maxQueueSize: 1,
            batchSize: 1);
        using var dispatcher = new ErrorReportDispatcher(settings, new HttpClient(new TestHttpMessageHandler()));
        var report = ErrorReport.FromException("UnhandledException", new Exception("first"), false, settings, null);

        Assert.IsTrue(dispatcher.TryEnqueue(report));
        Assert.IsFalse(dispatcher.TryEnqueue(report));
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage Request { get; private set; } = default!;
        public string Body { get; private set; } = default!;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
