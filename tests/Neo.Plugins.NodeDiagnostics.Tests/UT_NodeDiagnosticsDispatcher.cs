// Copyright (C) 2015-2026 The Neo Project.
//
// UT_NodeDiagnosticsDispatcher.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;
using System.Text.Json;

namespace Neo.Plugins.NodeDiagnostics.Tests;

[TestClass]
public class UT_NodeDiagnosticsDispatcher
{
    [TestMethod]
    public async Task SendEventsAsync_RoutesErrorsToCollectorsAndNotifications()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "custom-errors",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"),
                    token: "collector-token"),
                new NodeDiagnosticsSinkSettings(
                    "notifications",
                    NodeDiagnosticsSinkKind.Notification,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://notify.example/hooks/node"),
                    token: "notify-token")
            ],
            environment: "unit-test",
            nodeName: "node-1");
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var nodeEvent = NodeDiagnosticsEvent.FromException("UnhandledException", new InvalidOperationException("boom"), true, settings, null);

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
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "google",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.GoogleCloudErrorReporting,
                    new Uri("https://clouderrorreporting.googleapis.com/v1beta1/projects/test/events:report"),
                    token: "gcp-token")
            ]);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var nodeEvent = NodeDiagnosticsEvent.FromException("UnhandledException", new InvalidOperationException("boom"), true, settings, null);

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
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "betterstack",
                    NodeDiagnosticsSinkKind.StatusMonitor,
                    NodeDiagnosticsProvider.BetterStackHeartbeat,
                    new Uri("https://uptime.betterstack.com/api/v1/heartbeat/abcd"))
            ]);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);

        var sent = await dispatcher.SendHeartbeatAsync(CancellationToken.None);

        Assert.IsTrue(sent);
        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(HttpMethod.Get, handler.Requests[0].Method);
        Assert.AreEqual("https://uptime.betterstack.com/api/v1/heartbeat/abcd", handler.Requests[0].RequestUri!.ToString());
        Assert.AreEqual("", handler.Bodies[0]);
    }

    [TestMethod]
    public async Task SendEventsAsync_IncludesOperatorTags()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "custom-errors",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            tags: new Dictionary<string, string>
            {
                ["role"] = "seed",
                ["region"] = "eu"
            });
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var nodeEvent = NodeDiagnosticsEvent.StartupDiagnostic(settings, null);

        var sent = await dispatcher.SendEventsAsync([nodeEvent], CancellationToken.None);

        Assert.IsTrue(sent);
        using var document = JsonDocument.Parse(handler.Bodies[0]);
        var tags = document.RootElement.GetProperty("events")[0].GetProperty("tags");
        Assert.AreEqual("seed", tags.GetProperty("role").GetString());
        Assert.AreEqual("eu", tags.GetProperty("region").GetString());
        Assert.AreEqual("StartupDiagnostic", document.RootElement.GetProperty("events")[0].GetProperty("eventType").GetString());
    }

    [TestMethod]
    public async Task SendEventsAsync_IncludesNodeLivenessContext()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "status",
                    NodeDiagnosticsSinkKind.StatusMonitor,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/status"))
            ]);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var nodeEvent = NodeDiagnosticsEvent.Heartbeat(
            settings,
            null,
            new NodeDiagnosticsNodeState(100, 120, 7));

        var sent = await dispatcher.SendEventsAsync([nodeEvent], CancellationToken.None);

        Assert.IsTrue(sent);
        using var document = JsonDocument.Parse(handler.Bodies[0]);
        var item = document.RootElement.GetProperty("events")[0];
        Assert.AreEqual("Heartbeat", item.GetProperty("eventType").GetString());
        Assert.AreEqual(100u, item.GetProperty("blockHeight").GetUInt32());
        Assert.AreEqual(120u, item.GetProperty("headerHeight").GetUInt32());
        Assert.AreEqual(7, item.GetProperty("secondsSinceLastBlockAdvance").GetInt32());

        var tags = item.GetProperty("tags");
        Assert.AreEqual("100", tags.GetProperty("block_height").GetString());
        Assert.AreEqual("120", tags.GetProperty("header_height").GetString());
        Assert.AreEqual("7", tags.GetProperty("seconds_since_last_block_advance").GetString());
    }

    [TestMethod]
    public async Task SendConsensusStallIfNeededAsync_SendsOncePerStalledHeight()
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "custom-errors",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            consensusStallThresholdSeconds: 30);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var now = DateTimeOffset.UtcNow;
        dispatcher.RecordBlockPersisted(100, now - TimeSpan.FromSeconds(31));

        var sent = await dispatcher.SendConsensusStallIfNeededAsync(now, CancellationToken.None);
        var duplicate = await dispatcher.SendConsensusStallIfNeededAsync(now.AddSeconds(1), CancellationToken.None);

        Assert.IsTrue(sent);
        Assert.IsFalse(duplicate);
        Assert.HasCount(1, handler.Requests);
        using var document = JsonDocument.Parse(handler.Bodies[0]);
        var item = document.RootElement.GetProperty("events")[0];
        Assert.AreEqual("ConsensusStall", item.GetProperty("eventType").GetString());
        Assert.AreEqual("error", item.GetProperty("severity").GetString());
        Assert.AreEqual(100u, item.GetProperty("blockHeight").GetUInt32());
        Assert.AreEqual(31, item.GetProperty("secondsSinceLastBlockAdvance").GetInt32());
        Assert.AreEqual("consensus", item.GetProperty("source").GetString());
    }

    [TestMethod]
    public void TryEnqueue_ReturnsFalseWhenQueueIsFull()
    {
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "custom-errors",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            maxQueueSize: 1,
            batchSize: 1);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, new HttpClient(new TestHttpMessageHandler()));
        var nodeEvent = NodeDiagnosticsEvent.FromException("UnhandledException", new Exception("first"), false, settings, null);

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
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "custom-errors",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            requestTimeoutMilliseconds: 50);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var nodeEvent = NodeDiagnosticsEvent.FromException("UnhandledException", new Exception("timeout"), false, settings, null);

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
        var settings = NodeDiagnosticsSettings.Create(
            sinks:
            [
                new NodeDiagnosticsSinkSettings(
                    "custom-errors",
                    NodeDiagnosticsSinkKind.ErrorCollector,
                    NodeDiagnosticsProvider.CustomWebhook,
                    new Uri("https://collector.example/v1/events"))
            ],
            requestTimeoutMilliseconds: 1000,
            flushTimeoutMilliseconds: 50);
        using var dispatcher = new NodeDiagnosticsDispatcher(settings, client);
        var nodeEvent = NodeDiagnosticsEvent.FromException("UnhandledException", new Exception("fatal"), true, settings, null);

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
