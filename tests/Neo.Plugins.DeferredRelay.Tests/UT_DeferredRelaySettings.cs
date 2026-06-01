// Copyright (C) 2015-2026 The Neo Project.
//
// UT_DeferredRelaySettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using System.Text;

namespace Neo.Plugins.DeferredRelay.Tests;

[TestClass]
public class UT_DeferredRelaySettings
{
    private static IConfigurationSection BuildSection(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build().GetSection("PluginConfiguration");
    }

    [TestMethod]
    public void Defaults_DisableFeature()
    {
        const string json = """
        {
          "PluginConfiguration": {
            "Path": "DeferredRelay_{0}"
          }
        }
        """;
        DeferredRelaySettings.Load(BuildSection(json));
        Assert.AreEqual(0u, DeferredRelaySettings.Default.MaxTransactions);
        Assert.AreEqual(0u, DeferredRelaySettings.Default.CheckFrequency);
        Assert.IsFalse(DeferredRelaySettings.Default.Enabled);
    }

    [TestMethod]
    public void LoadsMaxAndFrequency()
    {
        const string json = """
        {
          "PluginConfiguration": {
            "Path": "DeferredRelay_{0}",
            "MaxTransactions": 8192,
            "CheckFrequency": 5
          }
        }
        """;
        DeferredRelaySettings.Load(BuildSection(json));
        Assert.AreEqual(8192u, DeferredRelaySettings.Default.MaxTransactions);
        Assert.AreEqual(5u, DeferredRelaySettings.Default.CheckFrequency);
        Assert.IsTrue(DeferredRelaySettings.Default.Enabled);
    }

    [TestMethod]
    public void Enabled_RequiresBothMaxTransactionsAndCheckFrequency()
    {
        DeferredRelaySettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "MaxTransactions": 10,
            "CheckFrequency": 0
          }
        }
        """));
        Assert.IsFalse(DeferredRelaySettings.Default.Enabled);

        DeferredRelaySettings.Load(BuildSection("""
        {
          "PluginConfiguration": {
            "MaxTransactions": 0,
            "CheckFrequency": 5
          }
        }
        """));
        Assert.IsFalse(DeferredRelaySettings.Default.Enabled);
    }

    [TestMethod]
    public void LoadsExceptionPolicy()
    {
        const string json = """
        {
          "PluginConfiguration": {
            "UnhandledExceptionPolicy": "StopPlugin"
          }
        }
        """;
        DeferredRelaySettings.Load(BuildSection(json));
        Assert.AreEqual(UnhandledExceptionPolicy.StopPlugin, DeferredRelaySettings.Default.ExceptionPolicy);
    }
}
