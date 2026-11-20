// Copyright (C) 2015-2026 The Neo Project.
//
// RestServerSettingsTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;

namespace Neo.Plugins.RestServer.Tests;

[TestClass]
public class RestServerSettingsTests
{
    [TestMethod]
    [DataRow("{}", "127.0.0.1")]
    [DataRow("{ \"Port\": 1234 }", "127.0.0.1")]
    [DataRow("{ \"BindAddress\": \"0.0.0.0\" }", "0.0.0.0")]
    public void Load_UsesDefaultAddressUnlessOverridden(string settingsJson, string expectedAddress)
    {
        var configuration = TestUtility.CreateConfigurationFromJson(settingsJson);
        RestServerSettings.Load(configuration.GetSection("PluginConfiguration"));
        Assert.AreEqual(IPAddress.Parse(expectedAddress), RestServerSettings.Current.BindAddress);
    }
}
