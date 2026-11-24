// Copyright (C) 2015-2026 The Neo Project.
//
// LevelDBStoreSettingsTest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.Storage.Tests;

[TestClass]
public class LevelDBStoreSettingsTest
{
    [TestMethod]
    public void DefaultsEnableTunedOptions()
    {
        var settings = new LevelDBStoreSettings(EmptySection());

        Assert.AreEqual(64 << 20, settings.WriteBufferSize);
        Assert.AreEqual(2000, settings.MaxOpenFiles);
        Assert.AreEqual(16384, settings.BlockSize);
    }

    [TestMethod]
    public void ConfigurationOverridesEveryTuningValue()
    {
        var values = new Dictionary<string, string>
        {
            ["PluginConfiguration:WriteBufferSize"] = "4194304",
            ["PluginConfiguration:MaxOpenFiles"] = "1000",
            ["PluginConfiguration:BlockSize"] = "4096"
        };
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection("PluginConfiguration");
        var settings = new LevelDBStoreSettings(section);

        Assert.AreEqual(4 << 20, settings.WriteBufferSize);
        Assert.AreEqual(1000, settings.MaxOpenFiles);
        Assert.AreEqual(4096, settings.BlockSize);
    }

    private static IConfigurationSection EmptySection()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build()
            .GetSection("PluginConfiguration");
    }
}
