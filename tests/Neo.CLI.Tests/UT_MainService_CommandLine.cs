// Copyright (C) 2015-2026 The Neo Project.
//
// UT_MainService_CommandLine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Reflection;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_MainService_CommandLine
{
    [TestMethod]
    public void TestDefaultPluginDownloadUrlUsesNeoNodeReleases()
    {
        Assert.AreEqual(new Uri("https://api.github.com/repos/neo-project/neo-node/releases"), new PluginsSettings().DownloadUrl);
    }

    [TestMethod]
    public void TestCommandLineSettingsPreservePluginSettings()
    {
        var previousSettings = Settings.Custom;
        var pluginDownloadUrl = new Uri("https://example.com/custom-plugin-releases");

        try
        {
            var configuredSettings = new Settings
            {
                Logger = new LoggerSettings(),
                Storage = new StorageSettings(),
                P2P = new P2PSettings(),
                UnlockWallet = new UnlockWalletSettings(),
                Contracts = new ContractsSettings(),
                Plugins = new PluginsSettings { DownloadUrl = pluginDownloadUrl }
            };
            var method = typeof(MainService).GetMethod("CustomApplicationSettings", BindingFlags.NonPublic | BindingFlags.Static);

            method!.Invoke(null, [new CommandLineOptions { DBPath = "Data_Custom_{0}" }, configuredSettings]);

            Assert.AreEqual(pluginDownloadUrl, Settings.Default.Plugins.DownloadUrl);
        }
        finally
        {
            Settings.Custom = previousSettings;
        }
    }
}
