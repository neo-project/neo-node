// Copyright (C) 2015-2026 The Neo Project.
//
// UT_NodeOpsDispatcher.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;
using System.Text.Json;

namespace Neo.Plugins.NodeOps.Tests;

[TestClass]
public class UT_NodeOpsDispatcher
{
    [TestMethod]
    public async Task SendEventsAsync_RoutesErrorsToCollectorsAndNotifications()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeOpsSettings.Create(
            sinks:
            [
                new NodeOpsSinkSettings(
                    "custom-errors",
                    NodeOpsSinkKind.ErrorCollector,
                    NodeOpsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"),
                    token: "collector-token"),
                new NodeOpsSinkSettings(
                    "notifications",
                    NodeOpsSinkKind.Notification,
                    NodeOpsProvider.CustomWebhook,
                    new Uri("https://notify.example/hooks/node"),
                    token: "notify-token")
            ],
            environment: "unit-test",
            nodeName: "node-1");
        using var dispatcher = new NodeOpsDispatcher(settings, client);
        var nodeEvent = NodeOpsEvent.FromException("UnhandledException", new InvalidOperationException("boom"), true, settings, null);

        var sent = await dispatcher.SendEventsAsync([nodeEvent], CancellationToken.None);

        Assert.IsTrue(sent);
        Assert.HasCount(2, handler.Requests);
        Assert.AreEqual("https://collector.example/v1/events", handler.Requests[0].RequestUri!.ToString());
        Assert.AreEqual("Bearer collector-token", handler.Requests[0].Headers.GetValues("Authorization").Single());
        Assert.AreEqual("https://notify.example/hooks/node", handler.Requests[1].RequestUri!.ToString());
        Assert.AreEqual("Bearer notify-token", handler.Requests[1].Headers.GetValues("Authorization").Single());

        using var document = JsonDocument.Parse(handler.Bodies[0]);
        Assert.AreEqual("neo-node", document.RootElement.GetProperty("serviceName").GetString());
        Assert.AreEqual("unit-test", document.RootElement.GetProperty("environment").GetString());
        Assert.AreEqual("node-1", document.RootElement.GetProperty("nodeName").GetString());
        var item = document.RootElement.GetProperty("events")[0];
        Assert.AreEqual("UnhandledException", item.GetProperty("eventType").GetString());
        Assert.AreEqual("fatal", item.GetProperty("severity").GetString());
        Assert.AreEqual("boom", item.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task SendEventsAsync_UsesProviderPayloadForGoogleCloudErrorReporting()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeOpsSettings.Create(
            sinks:
            [
                new NodeOpsSinkSettings(
                    "google",
                    NodeOpsSinkKind.ErrorCollector,
                    NodeOpsProvider.GoogleCloudErrorReporting,
                    new Uri("https://clouderrorreporting.googleapis.com/v1beta1/projects/test/events:report"),
                    token: "gcp-token")
            ]);
        using var dispatcher = new NodeOpsDispatcher(settings, client);
        var nodeEvent = NodeOpsEvent.FromException("UnhandledException", new InvalidOperationException("boom"), true, settings, null);

        var sent = await dispatcher.SendEventsAsync([nodeEvent], CancellationToken.None);

        Assert.IsTrue(sent);
        Assert.AreEqual("Bearer gcp-token", handler.Requests[0].Headers.GetValues("Authorization").Single());
        using var document = JsonDocument.Parse(handler.Bodies[0]);
        Assert.AreEqual("neo-node", document.RootElement.GetProperty("serviceContext").GetProperty("service").GetString());
        Assert.IsTrue(document.RootElement.TryGetProperty("message", out _));
        Assert.IsTrue(document.RootElement.TryGetProperty("context", out _));
    }

    [TestMethod]
    public async Task SendHeartbeatAsync_UsesConfiguredHeartbeatSink()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeOpsSettings.Create(
            sinks:
            [
                new NodeOpsSinkSettings(
                    "betterstack",
                    NodeOpsSinkKind.StatusMonitor,
                    NodeOpsProvider.BetterStackHeartbeat,
                    new Uri("https://uptime.betterstack.com/api/v1/heartbeat/abcd"))
            ]);
        using var dispatcher = new NodeOpsDispatcher(settings, client);

        var sent = await dispatcher.SendHeartbeatAsync(CancellationToken.None);

        Assert.IsTrue(sent);
        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(HttpMethod.Get, handler.Requests[0].Method);
        Assert.AreEqual("https://uptime.betterstack.com/api/v1/heartbeat/abcd", handler.Requests[0].RequestUri!.ToString());
        Assert.AreEqual("", handler.Bodies[0]);
    }

    [TestMethod]
    public void TryEnqueue_ReturnsFalseWhenQueueIsFull()
    {
        var settings = NodeOpsSettings.Create(
            sinks:
            [
                new NodeOpsSinkSettings(
                    "custom-errors",
                    NodeOpsSinkKind.ErrorCollector,
                    NodeOpsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            maxQueueSize: 1,
            batchSize: 1);
        using var dispatcher = new NodeOpsDispatcher(settings, new HttpClient(new TestHttpMessageHandler()));
        var nodeEvent = NodeOpsEvent.FromException("UnhandledException", new Exception("first"), false, settings, null);

        Assert.IsTrue(dispatcher.TryEnqueue(nodeEvent));
        Assert.IsFalse(dispatcher.TryEnqueue(nodeEvent));
    }

    [TestMethod]
    public async Task SendEventsAsync_ReturnsFalseWhenSinkTimesOut()
    {
        var handler = new TestHttpMessageHandler
        {
            Delay = TimeSpan.FromMilliseconds(200)
        };
        using var client = new HttpClient(handler);
        var settings = NodeOpsSettings.Create(
            sinks:
            [
                new NodeOpsSinkSettings(
                    "custom-errors",
                    NodeOpsSinkKind.ErrorCollector,
                    NodeOpsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            requestTimeoutMilliseconds: 50);
        using var dispatcher = new NodeOpsDispatcher(settings, client);
        var nodeEvent = NodeOpsEvent.FromException("UnhandledException", new Exception("timeout"), false, settings, null);

        var sent = await dispatcher.SendEventsAsync([nodeEvent], CancellationToken.None);

        Assert.IsFalse(sent);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public void SendNow_UsesFlushTimeout()
    {
        var handler = new TestHttpMessageHandler
        {
            Delay = TimeSpan.FromMilliseconds(200)
        };
        using var client = new HttpClient(handler);
        var settings = NodeOpsSettings.Create(
            sinks:
            [
                new NodeOpsSinkSettings(
                    "custom-errors",
                    NodeOpsSinkKind.ErrorCollector,
                    NodeOpsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            requestTimeoutMilliseconds: 1000,
            flushTimeoutMilliseconds: 50);
        using var dispatcher = new NodeOpsDispatcher(settings, client);
        var nodeEvent = NodeOpsEvent.FromException("UnhandledException", new Exception("fatal"), true, settings, null);

        Assert.IsFalse(dispatcher.SendNow(nodeEvent));
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();
        public TimeSpan Delay { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, cancellationToken);
            Bodies.Add(request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
