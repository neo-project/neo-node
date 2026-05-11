// Copyright (C) 2015-2026 The Neo Project.
//
// UT_Settings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.Extensions.Configuration;
using System.Text;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_Settings
{
    private static IConfigurationSection BuildSection(string json, string sectionName)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build().GetSection(sectionName);
    }

    [TestMethod]
    public void P2PSettings_Defaults_DisablePendingValidUntilRelay()
    {
        var p2p = new P2PSettings();
        Assert.AreEqual(0u, p2p.PendingRelayMaxTransactions);
        Assert.AreEqual(0u, p2p.PendingCheckFrequency);
    }

    [TestMethod]
    public void P2PSettings_LoadsPendingRelayLimitsFromConfiguration()
    {
        const string json = """
        {
          "ApplicationConfiguration": {
            "P2P": {
              "Port": 10333,
              "PendingRelayMaxTransactions": 8192,
              "PendingCheckFrequency": 5
            }
          }
        }
        """;
        var section = BuildSection(json, "ApplicationConfiguration");
        var settings = new Settings(section);
        Assert.AreEqual(8192u, settings.P2P.PendingRelayMaxTransactions);
        Assert.AreEqual(5u, settings.P2P.PendingCheckFrequency);
        Assert.AreEqual((ushort)10333, settings.P2P.Port);
    }

    [TestMethod]
    public void P2PSettings_OmittedKeys_FallBackToDefaults()
    {
        const string json = """
        {
          "ApplicationConfiguration": {
            "P2P": {
              "Port": 10333
            }
          }
        }
        """;
        var section = BuildSection(json, "ApplicationConfiguration");
        var settings = new Settings(section);
        Assert.AreEqual(0u, settings.P2P.PendingRelayMaxTransactions);
        Assert.AreEqual(0u, settings.P2P.PendingCheckFrequency);
    }

    [TestMethod]
    public void Settings_ParameterlessCtor_PopulatesEverySection()
    {
        // Defensive: the default ctor must initialize every sub-settings to a non-null instance so
        // MainService.Stop, OnListPendingCommand, etc. never throw NullReferenceException when
        // Settings.Custom is used.
        var s = new Settings();
        Assert.IsNotNull(s.Logger);
        Assert.IsNotNull(s.Storage);
        Assert.IsNotNull(s.P2P);
        Assert.IsNotNull(s.UnlockWallet);
        Assert.IsNotNull(s.Contracts);
        Assert.IsNotNull(s.Plugins);
    }
}
