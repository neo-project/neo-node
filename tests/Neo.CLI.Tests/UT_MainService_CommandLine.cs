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
    public void TestCommandLineConfigSelectsProtocolSettings()
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        var previousProtocol = ProtocolSettings.Custom;
        var directory = Path.Combine(Path.GetTempPath(), $"neo-cli-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "config.json"), """
                { "ProtocolConfiguration": { "Network": 111 } }
                """);
            var customConfig = Path.Combine(directory, "custom.json");
            File.WriteAllText(customConfig, """
                { "ProtocolConfiguration": { "Network": 222 } }
                """);
            Directory.SetCurrentDirectory(directory);

            var method = typeof(MainService).GetMethod("LoadProtocolSettings", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            var defaultProtocol = (ProtocolSettings)method.Invoke(null, [new CommandLineOptions()])!;
            Assert.AreEqual(111u, defaultProtocol.Network);

            var selectedProtocol = (ProtocolSettings)method.Invoke(null, [new CommandLineOptions { Config = customConfig }])!;
            Assert.AreEqual(222u, selectedProtocol.Network);
            Assert.AreSame(selectedProtocol, ProtocolSettings.Custom);

            var missingConfig = Path.Combine(directory, "missing.json");
            var exception = Assert.ThrowsExactly<TargetInvocationException>(() =>
                method.Invoke(null, [new CommandLineOptions { Config = missingConfig }]));
            Assert.IsInstanceOfType(exception.InnerException, typeof(FileNotFoundException));
            Assert.AreEqual(missingConfig, ((FileNotFoundException)exception.InnerException!).FileName);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            ProtocolSettings.Custom = previousProtocol;
            Directory.Delete(directory, recursive: true);
        }
    }

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
