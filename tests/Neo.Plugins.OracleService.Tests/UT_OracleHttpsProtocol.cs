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

using Microsoft.Extensions.Configuration;
using Neo.Plugins.OracleService.Protocols;
using System.Net.Http.Headers;
using System.Text;

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

    [TestMethod]
    public void TryResolveHttpsRedirect_ResolvesRelativePathWithoutSlash()
    {
        var current = new Uri("https://example.com/v1/oracle");
        var location = new Uri("next", UriKind.Relative);
        Assert.IsTrue(OracleHttpsProtocol.TryResolveHttpsRedirect(current, location, out var next));
        Assert.AreEqual("https://example.com/v1/next", next.AbsoluteUri);
    }

    [TestMethod]
    public void TryResolveHttpsRedirect_RejectsFtp()
    {
        var current = new Uri("https://example.com/oracle");
        Assert.IsFalse(OracleHttpsProtocol.TryResolveHttpsRedirect(current, new Uri("ftp://example.com/file"), out _));
    }

    [TestMethod]
    public void IsSupportedContentType_MissingHeader_IsRejected()
    {
        using var message = new HttpResponseMessage();
        Assert.IsNull(message.Content.Headers.ContentType);
        Assert.IsFalse(OracleHttpsProtocol.IsSupportedContentType(message.Content.Headers, ["application/json"]));
    }

    [TestMethod]
    public void IsSupportedContentType_JsonIsAllowed_XmlIsNot()
    {
        using var ok = new HttpResponseMessage();
        ok.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var bad = new HttpResponseMessage();
        bad.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        string[] allowed = ["application/json"];
        Assert.IsTrue(OracleHttpsProtocol.IsSupportedContentType(ok.Content.Headers, allowed));
        Assert.IsFalse(OracleHttpsProtocol.IsSupportedContentType(bad.Content.Headers, allowed));
    }

    [TestMethod]
    public void GetEncoding_DefaultsToUtf8_WhenCharsetMissing()
    {
        using var message = new HttpResponseMessage();
        Assert.AreEqual(Encoding.UTF8, OracleHttpsProtocol.GetEncoding(message.Content.Headers));
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Assert.AreEqual(Encoding.UTF8, OracleHttpsProtocol.GetEncoding(message.Content.Headers));
    }

    [TestMethod]
    public void GetEncoding_UsesDeclaredCharset()
    {
        using var message = new HttpResponseMessage();
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        Assert.AreEqual(Encoding.UTF8, OracleHttpsProtocol.GetEncoding(message.Content.Headers));
        message.Content.Headers.ContentType.CharSet = "utf-16";
        Assert.AreEqual(Encoding.Unicode, OracleHttpsProtocol.GetEncoding(message.Content.Headers));
    }

    [TestMethod]
    public void HttpsSettings_MaxRedirects_DefaultsToTwo()
    {
        var https = new HttpsSettings(EmptySection());
        Assert.AreEqual(HttpsSettings.DefaultMaxRedirects, https.MaxRedirects);
        Assert.AreEqual(2, https.MaxRedirects);
    }

    [TestMethod]
    public void HttpsSettings_MaxRedirects_ReadsFromConfig()
    {
        var https = new HttpsSettings(Section(("MaxRedirects", "5")));
        Assert.AreEqual(5, https.MaxRedirects);
    }

    [TestMethod]
    public void HttpsSettings_MaxRedirects_ClampsNegativeToZero()
    {
        var https = new HttpsSettings(Section(("MaxRedirects", "-3")));
        Assert.AreEqual(0, https.MaxRedirects);
    }

    private static IConfigurationSection EmptySection()
        => new ConfigurationBuilder().AddInMemoryCollection().Build().GetSection("Https");

    private static IConfigurationSection Section(params (string Key, string Value)[] values)
    {
        var data = values.ToDictionary(p => "Https:" + p.Key, p => p.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(data!).Build().GetSection("Https");
    }
}
