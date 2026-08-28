// Copyright (C) 2015-2026 The Neo Project.
//
// UT_OracleHttpsProtocol.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Plugins.OracleService.Protocols;

namespace Neo.Plugins.OracleService.Tests;

[TestClass]
public class UT_OracleHttpsProtocol
{
    [TestMethod]
    public void TryResolveHttpsRedirect_KeepsAbsoluteHttps()
    {
        var current = new Uri("https://example.com/oracle");
        var location = new Uri("https://cdn.example.com/data.json");
        Assert.IsTrue(OracleHttpsProtocol.TryResolveHttpsRedirect(current, location, out var next));
        Assert.AreEqual(location, next);
    }

    [TestMethod]
    public void TryResolveHttpsRedirect_ResolvesRelativeAgainstCurrent()
    {
        var current = new Uri("https://example.com/v1/oracle");
        var location = new Uri("/v2/oracle", UriKind.Relative);
        Assert.IsTrue(OracleHttpsProtocol.TryResolveHttpsRedirect(current, location, out var next));
        Assert.AreEqual("https://example.com/v2/oracle", next.AbsoluteUri);
    }

    [TestMethod]
    public void TryResolveHttpsRedirect_RejectsHttp()
    {
        var current = new Uri("https://example.com/oracle");
        var location = new Uri("http://example.com/oracle");
        Assert.IsFalse(OracleHttpsProtocol.TryResolveHttpsRedirect(current, location, out _));
    }
}
