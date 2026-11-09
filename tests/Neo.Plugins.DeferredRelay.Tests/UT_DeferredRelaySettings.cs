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
using Neo.SmartContract.Native;
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
            "MaxTransactionsPerSender": 16,
            "CheckFrequency": 5
          }
        }
        """;
        DeferredRelaySettings.Load(BuildSection(json));
        Assert.AreEqual(8192u, DeferredRelaySettings.Default.MaxTransactions);
        Assert.AreEqual(16u, DeferredRelaySettings.Default.MaxTransactionsPerSender);
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
    public void Validate_RejectsPerSenderNotLessThanMax()
    {
        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            DeferredRelaySettings.Create(maxTransactions: 16u, checkFrequency: 1u, maxTransactionsPerSender: 16u));
        Assert.AreEqual("maxTransactionsPerSender", ex.ParamName);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            DeferredRelaySettings.Create(maxTransactions: 16u, checkFrequency: 1u, maxTransactionsPerSender: 32u));
    }

    [TestMethod]
    public void Validate_AllowsPerSenderZeroOrDisabledMax()
    {
        DeferredRelaySettings.Create(maxTransactions: 16u, checkFrequency: 1u, maxTransactionsPerSender: 0u);
        DeferredRelaySettings.Create(maxTransactions: 0u, checkFrequency: 1u, maxTransactionsPerSender: 100u);
        DeferredRelaySettings.Create(maxTransactions: 16u, checkFrequency: 1u, maxTransactionsPerSender: 15u);
    }

    [TestMethod]
    public void Load_RejectsInvalidPerSenderCap()
    {
        const string json = """
        {
          "PluginConfiguration": {
            "MaxTransactions": 8,
            "MaxTransactionsPerSender": 8,
            "CheckFrequency": 1
          }
        }
        """;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DeferredRelaySettings.Load(BuildSection(json)));
    }

    [TestMethod]
    public void LoadsMinNetworkFee()
    {
        const string json = """
        {
          "PluginConfiguration": {
            "MinNetworkFee": 0.0001
          }
        }
        """;
        DeferredRelaySettings.Load(BuildSection(json));
        Assert.AreEqual((long)new BigDecimal(0.0001M, NativeContract.GAS.Decimals).Value, DeferredRelaySettings.Default.MinNetworkFee);
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
