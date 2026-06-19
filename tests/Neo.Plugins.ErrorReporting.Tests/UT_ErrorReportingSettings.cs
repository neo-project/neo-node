// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ErrorReportingSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using System.Text;

namespace Neo.Plugins.ErrorReporting.Tests;

[TestClass]
public class UT_ErrorReportingSettings
{
    private static IConfigurationSection BuildSection(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build().GetSection("PluginConfiguration");
    }

    [TestMethod]
    public void Defaults_DisablePluginWithoutEndpoint()
    {
        ErrorReportingSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {}
        }
        """));

        Assert.IsFalse(ErrorReportingSettings.Default.Enabled);
        Assert.IsNull(ErrorReportingSettings.Default.Endpoint);
        Assert.AreEqual("neo-node", ErrorReportingSettings.Default.ServiceName);
        Assert.AreEqual("production", ErrorReportingSettings.Default.Environment);
    }

    [TestMethod]
    public void LoadsEndpointHeadersAndLimits()
    {
        ErrorReportingSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "Endpoint": "https://collector.example/v1/events",
            "Headers": {
              "X-Api-Key": "secret"
            },
            "Environment": "mainnet",
            "ServiceName": "neo-node",
            "NodeName": "seed-1",
            "MaxQueueSize": 128,
            "BatchSize": 8,
            "MaxRetries": 2,
            "RetryDelayMilliseconds": 250,
            "FlushTimeoutMilliseconds": 3000,
            "MaxMessageLength": 512,
            "MaxStackTraceLength": 4096,
            "UnhandledExceptionPolicy": "StopPlugin"
          }
        }
        """));

        var settings = ErrorReportingSettings.Default;
        Assert.IsTrue(settings.Enabled);
        Assert.AreEqual("https://collector.example/v1/events", settings.Endpoint!.ToString());
        Assert.AreEqual("secret", settings.Headers["X-Api-Key"]);
        Assert.AreEqual("mainnet", settings.Environment);
        Assert.AreEqual("seed-1", settings.NodeName);
        Assert.AreEqual(128, settings.MaxQueueSize);
        Assert.AreEqual(8, settings.BatchSize);
        Assert.AreEqual(2, settings.MaxRetries);
        Assert.AreEqual(250, settings.RetryDelayMilliseconds);
        Assert.AreEqual(3000, settings.FlushTimeoutMilliseconds);
        Assert.AreEqual(512, settings.MaxMessageLength);
        Assert.AreEqual(4096, settings.MaxStackTraceLength);
        Assert.AreEqual(UnhandledExceptionPolicy.StopPlugin, settings.ExceptionPolicy);
    }

    [TestMethod]
    public void Load_RejectsInvalidEndpoint()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ErrorReportingSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "Endpoint": "file:///tmp/errors.json"
          }
        }
        """)));
    }

    [TestMethod]
    public void Load_RejectsInvalidQueueLimits()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ErrorReportingSettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "MaxQueueSize": 2,
            "BatchSize": 3
          }
        }
        """)));
    }
}
