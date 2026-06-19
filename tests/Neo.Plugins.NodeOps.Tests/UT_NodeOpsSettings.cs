// Copyright (C) 2015-2026 The Neo Project.
//
// UT_NodeOpsSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using System.Text;

namespace Neo.Plugins.NodeOps.Tests;

[TestClass]
public class UT_NodeOpsSettings
{
    private static IConfigurationSection BuildSection(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build().GetSection("PluginConfiguration");
    }

    [TestMethod]
    public void Defaults_DisablePluginWithoutConfiguredSinks()
    {
        NodeOpsSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {}
        }
        """));

        Assert.IsFalse(NodeOpsSettings.Default.Enabled);
        Assert.AreEqual("neo-node", NodeOpsSettings.Default.ServiceName);
        Assert.AreEqual("production", NodeOpsSettings.Default.Environment);
        Assert.AreEqual(60, NodeOpsSettings.Default.HeartbeatIntervalSeconds);
    }

    [TestMethod]
    public void LoadsMultipleSinkTypes()
    {
        NodeOpsSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "Environment": "mainnet",
            "ServiceName": "neo-node",
            "NodeName": "seed-1",
            "HeartbeatIntervalSeconds": 120,
            "RequestTimeoutMilliseconds": 2500,
            "Sinks": [
              {
                "Name": "errors",
                "Description": "Primary error sink",
                "Kind": "ErrorCollector",
                "Provider": "Sentry",
                "Endpoint": "https://sentry.example/api/123/store/",
                "Token": "sentry-token",
                "TokenHeader": "X-Sentry-Auth",
                "TokenScheme": "",
                "MinimumSeverity": "Error"
              },
              {
                "Name": "status",
                "Kind": "StatusMonitor",
                "Provider": "BetterStackHeartbeat",
                "Endpoint": "https://uptime.betterstack.com/api/v1/heartbeat/abcd"
              },
              {
                "Name": "notify",
                "Kind": "Notification",
                "Provider": "CustomWebhook",
                "Endpoint": "https://hooks.example/neo",
                "Headers": {
                  "X-Team": "ops"
                },
                "MinimumSeverity": "Warning"
              }
            ],
            "UnhandledExceptionPolicy": "StopPlugin"
          }
        }
        """));

        var settings = NodeOpsSettings.Default;
        Assert.IsTrue(settings.Enabled);
        Assert.IsTrue(settings.HasEventSinks);
        Assert.IsTrue(settings.HasHeartbeatSinks);
        Assert.AreEqual("mainnet", settings.Environment);
        Assert.AreEqual("seed-1", settings.NodeName);
        Assert.AreEqual(120, settings.HeartbeatIntervalSeconds);
        Assert.AreEqual(2500, settings.RequestTimeoutMilliseconds);
        Assert.HasCount(3, settings.Sinks);
        Assert.AreEqual("Primary error sink", settings.Sinks[0].Description);
        Assert.AreEqual(NodeOpsProvider.Sentry, settings.Sinks[0].Provider);
        Assert.AreEqual("X-Sentry-Auth", settings.Sinks[0].TokenHeader);
        Assert.AreEqual("", settings.Sinks[0].TokenScheme);
        Assert.AreEqual("GET", settings.Sinks[1].Method);
        Assert.AreEqual("ops", settings.Sinks[2].Headers["X-Team"]);
        Assert.AreEqual(NodeOpsSeverity.Warning, settings.Sinks[2].MinimumSeverity);
        Assert.AreEqual(UnhandledExceptionPolicy.StopPlugin, settings.ExceptionPolicy);
    }

    [TestMethod]
    public void Load_RejectsInvalidEndpoint()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NodeOpsSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "Sinks": [
              {
                "Name": "bad",
                "Kind": "ErrorCollector",
                "Provider": "CustomWebhook",
                "Endpoint": "file:///tmp/errors.json"
              }
            ]
          }
        }
        """)));
    }

    [TestMethod]
    public void Load_RejectsInvalidHeartbeatInterval()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NodeOpsSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "HeartbeatIntervalSeconds": 10
          }
        }
        """)));
    }

    [TestMethod]
    public void Load_RejectsInvalidQueueLimits()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NodeOpsSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "MaxQueueSize": 2,
            "BatchSize": 3
          }
        }
        """)));
    }

    [TestMethod]
    public void Load_RejectsInvalidRequestTimeout()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NodeOpsSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "RequestTimeoutMilliseconds": 0
          }
        }
        """)));
    }
}
