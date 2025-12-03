// Copyright (C) 2015-2025 The Neo Project.
//
// UT_OracleDnsProtocol.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.OracleService.Protocols;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins.OracleService.Tests;

[TestClass]
public class UT_OracleDnsProtocol
{
    [TestInitialize]
    public void Setup()
    {
        LoadSettings();
    }

    [TestMethod]
    public void BuildQueryName_ParsesDnsUri()
    {
        var uri = new Uri("dns:simon.example.org?TYPE=TXT");
        string name = OracleDnsProtocol.BuildQueryName(uri);
        Assert.AreEqual("simon.example.org", name);
    }

    [TestMethod]
    public void BuildQueryName_RespectsAuthoritySyntax()
    {
        var uri = new Uri("dns://resolver.example/ftp.example.org?TYPE=TXT");
        string name = OracleDnsProtocol.BuildQueryName(uri);
        Assert.AreEqual("ftp.example.org", name);
    }

    [TestMethod]
    public void BuildQueryName_ThrowsWithoutDnsName()
    {
        var uri = new Uri("dns://resolver.example/");
        try
        {
            OracleDnsProtocol.BuildQueryName(uri);
            Assert.Fail("Expected FormatException for missing dnsname.");
        }
        catch (FormatException)
        {
        }
    }

    [TestMethod]
    public void BuildQueryName_UsesNameOverride()
    {
        var uri = new Uri("dns://resolver.example/ignored?name=override.example.com");
        string name = OracleDnsProtocol.BuildQueryName(uri);
        Assert.AreEqual("override.example.com", name);
    }

    [TestMethod]
    public void BuildQueryName_RejectsPathSegments()
    {
        var uri = new Uri("dns:example.com/extra");
        try
        {
            OracleDnsProtocol.BuildQueryName(uri);
            Assert.Fail("Expected FormatException for path segments.");
        }
        catch (FormatException)
        {
        }
    }

    [TestMethod]
    public async Task ProcessAsync_RejectsUnsupportedClass()
    {
        using var protocol = new OracleDnsProtocol(new StubHandler(_ => throw new InvalidOperationException("Should not send when class is invalid")));
        (OracleResponseCode code, string message) = await protocol.ProcessAsync(new Uri("dns:example.com?CLASS=CH"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.Error, code);
        StringAssert.Contains(message, "class");
    }

    [TestMethod]
    public async Task ProcessAsync_AllowsClassIn()
    {
        byte[] dnsResponse = BuildDnsResponse("example.com", 16, 120, Encoding.ASCII.GetBytes("\x05hello"));
        var handler = new StubHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("application/dns-message", request.Content.Headers.ContentType.MediaType);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dnsResponse)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
                }
            };
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?CLASS=IN;TYPE=TXT"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("TXT", doc.RootElement.GetProperty("Answers")[0].GetProperty("Type").GetString());
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsTooLargeForOversizedResponse()
    {
        // Create a response that will exceed MaxResultSize when serialized to JSON
        // Generate many TXT records to make the final JSON payload too large
        List<byte> response = new();

        // Header (12 bytes)
        response.AddRange(new byte[] { 0x00, 0x01 }); // ID
        response.AddRange(new byte[] { 0x81, 0x80 }); // Flags
        response.AddRange(new byte[] { 0x00, 0x01 }); // QDCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x10 }); // ANCOUNT: 16 answers
        response.AddRange(new byte[] { 0x00, 0x00 }); // NSCOUNT: 0
        response.AddRange(new byte[] { 0x00, 0x00 }); // ARCOUNT: 0

        // Question section
        EncodeDnsName(response, "big.example.com");
        response.AddRange(new byte[] { 0x00, 0x10 }); // TYPE TXT
        response.AddRange(new byte[] { 0x00, 0x01 }); // CLASS IN

        // Add 16 answer records, each with ~4KB of data
        string largeText = new('A', 4000);
        byte[] txtRdata = BuildTxtRdata(largeText);
        for (int i = 0; i < 16; i++)
        {
            EncodeDnsName(response, "big.example.com");
            response.AddRange(new byte[] { 0x00, 0x10 }); // TYPE TXT
            response.AddRange(new byte[] { 0x00, 0x01 }); // CLASS IN
            response.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x3C }); // TTL: 60
            response.Add((byte)(txtRdata.Length >> 8));
            response.Add((byte)(txtRdata.Length & 0xFF));
            response.AddRange(txtRdata);
        }

        byte[] dnsResponse = [.. response];

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:big.example.com?TYPE=TXT"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.ResponseTooLarge, code);
        Assert.IsNull(payload);
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsCertificateFromTxtRecord()
    {
        string base64Cert = GenerateCertificateBase64("CN=example.com");
        byte[] txtRdata = BuildTxtRdata(base64Cert);
        byte[] dnsResponse = BuildDnsResponse("oracle.example.com", 16, 60, txtRdata);

        var handler = new StubHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dnsResponse)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
                }
            };
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:oracle.example.com?TYPE=TXT;FORMAT=x509"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.IsNotNull(payload);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("oracle.example.com", doc.RootElement.GetProperty("Name").GetString());
        var certElement = doc.RootElement.GetProperty("Certificate");
        Assert.AreEqual(base64Cert, certElement.GetProperty("Der").GetString());
        using var parsedCert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(base64Cert));
        var pkElement = certElement.GetProperty("PublicKey");
        string expectedPublicKey = Convert.ToBase64String(parsedCert.GetPublicKey());
        string expectedAlgorithm = parsedCert.PublicKey.Oid?.FriendlyName ?? parsedCert.PublicKey.Oid?.Value;
        Assert.AreEqual(expectedPublicKey, pkElement.GetProperty("Encoded").GetString());
        Assert.AreEqual(expectedAlgorithm, pkElement.GetProperty("Algorithm").GetString());
        using RSA rsa = parsedCert.GetRSAPublicKey();
        Assert.IsNotNull(rsa);
        RSAParameters parameters = rsa.ExportParameters(false);
        Assert.AreEqual(Convert.ToHexString(parameters.Modulus), pkElement.GetProperty("Modulus").GetString());
        Assert.AreEqual(Convert.ToHexString(parameters.Exponent), pkElement.GetProperty("Exponent").GetString());
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsEcPublicKeyFields()
    {
        string base64Cert = GenerateEcCertificateBase64("CN=example-ec.com");
        byte[] txtRdata = BuildTxtRdata(base64Cert);
        byte[] dnsResponse = BuildDnsResponse("ec.example.com", 16, 60, txtRdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:ec.example.com?TYPE=TXT;FORMAT=x509"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        var pkElement = doc.RootElement.GetProperty("Certificate").GetProperty("PublicKey");
        using var parsedCert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(base64Cert));
        using ECDsa ecdsa = parsedCert.GetECDsaPublicKey();
        Assert.IsNotNull(ecdsa);
        ECParameters parameters = ecdsa.ExportParameters(false);
        string expectedCurve = parameters.Curve.Oid?.FriendlyName ?? parameters.Curve.Oid?.Value;
        Assert.AreEqual(expectedCurve, pkElement.GetProperty("Curve").GetString());
        Assert.AreEqual(Convert.ToHexString(parameters.Q.X), pkElement.GetProperty("X").GetString());
        Assert.AreEqual(Convert.ToHexString(parameters.Q.Y), pkElement.GetProperty("Y").GetString());
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsNotFoundForNxDomain()
    {
        // Build NXDOMAIN response (RCODE = 3)
        byte[] dnsResponse = BuildDnsResponseWithRcode("example.com", 1, 3);
        var handler = new StubHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dnsResponse)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
                }
            };
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.NotFound, code);
        Assert.IsNull(payload);
    }

    [TestMethod]
    public async Task ProcessAsync_OmitsCertificateWhenNotRequested()
    {
        byte[] txtRdata = BuildTxtRdata("hello");
        byte[] dnsResponse = BuildDnsResponse("plain.example.com", 16, 120, txtRdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:plain.example.com?TYPE=TXT"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.IsFalse(doc.RootElement.TryGetProperty("Certificate", out _));
    }

    [TestMethod]
    public async Task ProcessAsync_ParsesDkimTxtRecord()
    {
        const string dkimData = "k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAp1+6V9wVDqveufqdpypuXn7Z1xXHrp236UMtO4Zwzp1KimG1HjMATkUMlzUxr87hcPLZ9eczsQnUnxE27XGr0C+MEY0S8NxVkg4CSkiUbSSjMBDuNIQP5CKEM5Qn2ATqNnS/xPbbGr3HdWu3UwG+329xNXO/SuKD5d/mswHxZ34rnOG0r8QwMCKaRZ3eLaxhUJW6QcgO5Kb/6VQwWi4KFOeFHrgb3R04QLbTjaCj1eO0MJdHj7FVGHvXZHzVvzJeY9q24apqYh6gMPkTFogyXv3gZH/BqhGlymM4T/6QAEyy6AdZkGouVp21Hb+Jseb3CidRubc4QZAlWTMwVzKhI6+wIDAQAB";
        byte[] txtRdata = BuildTxtRdata(dkimData);
        byte[] dnsResponse = BuildDnsResponse("1alhai._domainkey.icloud.com", 16, 299, txtRdata);

        var handler = new StubHandler(request =>
        {
            Assert.IsTrue(request.Headers.Accept.Any(h => h.MediaType == "application/dns-message"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dnsResponse)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
                }
            };
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:1alhai._domainkey.icloud.com?TYPE=TXT"), CancellationToken.None);
        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("1alhai._domainkey.icloud.com", doc.RootElement.GetProperty("Name").GetString());
        var answers = doc.RootElement.GetProperty("Answers");
        Assert.AreEqual(1, answers.GetArrayLength());
        Assert.AreEqual("TXT", answers[0].GetProperty("Type").GetString());
        StringAssert.Contains(answers[0].GetProperty("Data").GetString(), "k=rsa");
    }

    [TestMethod]
    public void BuildDnsQuery_CreatesValidWireFormat()
    {
        byte[] query = OracleDnsProtocol.BuildDnsQuery("example.com", 16);

        // Verify header - query should have at least 12 bytes for header
        Assert.IsGreaterThanOrEqualTo(12, query.Length, $"Query length {query.Length} should be >= 12");

        // Flags should be 0x0100 (standard query, recursion desired)
        Assert.AreEqual(0x01, query[2]);
        Assert.AreEqual(0x00, query[3]);

        // QDCOUNT should be 1
        Assert.AreEqual(0x00, query[4]);
        Assert.AreEqual(0x01, query[5]);

        // Verify question section contains the domain name
        // After header (12 bytes), we should have: 7example3com0 (encoded name)
        Assert.AreEqual(7, query[12]); // length of "example"
        Assert.AreEqual((byte)'e', query[13]);
        Assert.AreEqual((byte)'x', query[14]);
    }

    #region DNS Wire Format Tests

    [TestMethod]
    public void BuildDnsQuery_EncodesSubdomainsCorrectly()
    {
        byte[] query = OracleDnsProtocol.BuildDnsQuery("sub.domain.example.com", 1);

        // Verify the encoded name: 3sub6domain7example3com0
        int offset = 12; // After header
        Assert.AreEqual(3, query[offset]); // "sub" length
        Assert.AreEqual((byte)'s', query[offset + 1]);
        Assert.AreEqual(6, query[offset + 4]); // "domain" length
        Assert.AreEqual(7, query[offset + 11]); // "example" length
        Assert.AreEqual(3, query[offset + 19]); // "com" length
    }

    [TestMethod]
    public void BuildDnsQuery_HandlesTrailingDot()
    {
        byte[] query1 = OracleDnsProtocol.BuildDnsQuery("example.com", 1);
        byte[] query2 = OracleDnsProtocol.BuildDnsQuery("example.com.", 1);

        // Both should produce the same encoded name (compare first 2 bytes which are random ID, then rest should match)
        Assert.HasCount(query1.Length, query2);
        CollectionAssert.AreEqual(query1[2..], query2[2..]);
    }

    [TestMethod]
    public void BuildDnsQuery_SetsCorrectRecordType()
    {
        // Test A record (type 1)
        byte[] queryA = OracleDnsProtocol.BuildDnsQuery("example.com", 1);
        int typeOffset = 12 + 13; // header + encoded name length for "example.com"
        Assert.AreEqual(0x00, queryA[typeOffset]);
        Assert.AreEqual(0x01, queryA[typeOffset + 1]);

        // Test AAAA record (type 28)
        byte[] queryAAAA = OracleDnsProtocol.BuildDnsQuery("example.com", 28);
        Assert.AreEqual(0x00, queryAAAA[typeOffset]);
        Assert.AreEqual(0x1C, queryAAAA[typeOffset + 1]);

        // Test TXT record (type 16)
        byte[] queryTXT = OracleDnsProtocol.BuildDnsQuery("example.com", 16);
        Assert.AreEqual(0x00, queryTXT[typeOffset]);
        Assert.AreEqual(0x10, queryTXT[typeOffset + 1]);
    }

    #endregion

    #region A Record Tests

    [TestMethod]
    public async Task ProcessAsync_ParsesARecord()
    {
        // A record: 192.168.1.1
        byte[] rdata = [192, 168, 1, 1];
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, 300, rdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=A"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("A", doc.RootElement.GetProperty("Type").GetString());
        Assert.AreEqual("192.168.1.1", doc.RootElement.GetProperty("Answers")[0].GetProperty("Data").GetString());
    }

    [TestMethod]
    public async Task ProcessAsync_DefaultsToARecord()
    {
        byte[] rdata = [10, 0, 0, 1];
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, 300, rdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        // No TYPE specified - should default to A
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("A", doc.RootElement.GetProperty("Type").GetString());
    }

    #endregion

    #region AAAA Record Tests

    [TestMethod]
    public async Task ProcessAsync_ParsesAAAARecord()
    {
        // AAAA record: 2001:db8::1
        byte[] rdata = [0x20, 0x01, 0x0d, 0xb8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
        byte[] dnsResponse = BuildDnsResponse("example.com", 28, 300, rdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=AAAA"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("AAAA", doc.RootElement.GetProperty("Type").GetString());
        string ipv6 = doc.RootElement.GetProperty("Answers")[0].GetProperty("Data").GetString();
        Assert.IsTrue(ipv6.Contains("2001:db8", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region DNS Name Compression Tests

    [TestMethod]
    public async Task ProcessAsync_HandlesNameCompression()
    {
        // Build response with compression pointer in answer section
        List<byte> response = new();

        // Header
        response.AddRange(new byte[] { 0x00, 0x01 }); // ID
        response.AddRange(new byte[] { 0x81, 0x80 }); // Flags
        response.AddRange(new byte[] { 0x00, 0x01 }); // QDCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x01 }); // ANCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x00 }); // NSCOUNT: 0
        response.AddRange(new byte[] { 0x00, 0x00 }); // ARCOUNT: 0

        // Question section - name starts at offset 12
        EncodeDnsName(response, "example.com");
        response.AddRange(new byte[] { 0x00, 0x01 }); // TYPE A
        response.AddRange(new byte[] { 0x00, 0x01 }); // CLASS IN

        // Answer section with compression pointer to offset 12
        response.AddRange(new byte[] { 0xC0, 0x0C }); // Compression pointer to offset 12
        response.AddRange(new byte[] { 0x00, 0x01 }); // TYPE A
        response.AddRange(new byte[] { 0x00, 0x01 }); // CLASS IN
        response.AddRange(new byte[] { 0x00, 0x00, 0x01, 0x2C }); // TTL: 300
        response.AddRange(new byte[] { 0x00, 0x04 }); // RDLENGTH: 4
        response.AddRange(new byte[] { 0x08, 0x08, 0x08, 0x08 }); // 8.8.8.8

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([.. response])
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=A"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("example.com", doc.RootElement.GetProperty("Answers")[0].GetProperty("Name").GetString());
        Assert.AreEqual("8.8.8.8", doc.RootElement.GetProperty("Answers")[0].GetProperty("Data").GetString());
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task ProcessAsync_ReturnsErrorForHttpFailure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string message) = await protocol.ProcessAsync(new Uri("dns:example.com"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Error, code);
        StringAssert.Contains(message, "500");
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsTimeoutOnCancellation()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Timeout, code);
        Assert.IsNull(payload);
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsErrorForServFail()
    {
        // SERVFAIL = RCODE 2
        byte[] dnsResponse = BuildDnsResponseWithRcode("example.com", 1, 2);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string message) = await protocol.ProcessAsync(new Uri("dns:example.com"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Error, code);
        StringAssert.Contains(message, "RCODE 2");
    }

    [TestMethod]
    public async Task ProcessAsync_ReturnsNotFoundForEmptyAnswer()
    {
        // NOERROR but no answers
        byte[] dnsResponse = BuildDnsResponseWithRcode("example.com", 1, 0);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.NotFound, code);
        Assert.IsNull(payload);
    }

    [TestMethod]
    public async Task ProcessAsync_RejectsUnsupportedRecordType()
    {
        using var protocol = new OracleDnsProtocol(new StubHandler(_ => throw new InvalidOperationException("Should not send")));
        (OracleResponseCode code, string message) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=INVALID"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Error, code);
        StringAssert.Contains(message, "INVALID");
    }

    #endregion

    #region Record Type Parsing Tests

    [TestMethod]
    public async Task ProcessAsync_AcceptsNumericRecordType()
    {
        byte[] rdata = [127, 0, 0, 1];
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, 300, rdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        // Use numeric type 1 instead of "A"
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=1"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("A", doc.RootElement.GetProperty("Type").GetString());
    }

    [TestMethod]
    public async Task ProcessAsync_AcceptsCaseInsensitiveRecordType()
    {
        byte[] txtRdata = BuildTxtRdata("test");
        byte[] dnsResponse = BuildDnsResponse("example.com", 16, 300, txtRdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=txt"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
    }

    #endregion

    #region Query Parameter Tests

    [TestMethod]
    public async Task ProcessAsync_AcceptsClass1AsIN()
    {
        byte[] rdata = [1, 2, 3, 4];
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, 300, rdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, _) = await protocol.ProcessAsync(new Uri("dns:example.com?CLASS=1"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
    }

    [TestMethod]
    public async Task ProcessAsync_AcceptsSemicolonSeparator()
    {
        byte[] txtRdata = BuildTxtRdata("test");
        byte[] dnsResponse = BuildDnsResponse("example.com", 16, 300, txtRdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        // RFC 4501 uses semicolon as separator
        (OracleResponseCode code, _) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=TXT;CLASS=IN"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
    }

    [TestMethod]
    public async Task ProcessAsync_AcceptsAmpersandSeparator()
    {
        byte[] txtRdata = BuildTxtRdata("test");
        byte[] dnsResponse = BuildDnsResponse("example.com", 16, 300, txtRdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, _) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=TXT&CLASS=IN"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
    }

    #endregion

    #region Certificate Format Tests

    [TestMethod]
    public async Task ProcessAsync_ExtractsCertificateWithCertFormat()
    {
        string base64Cert = GenerateCertificateBase64("CN=test.com");
        byte[] txtRdata = BuildTxtRdata(base64Cert);
        byte[] dnsResponse = BuildDnsResponse("test.com", 16, 60, txtRdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        // Use FORMAT=cert instead of FORMAT=x509
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:test.com?TYPE=TXT;FORMAT=cert"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.IsTrue(doc.RootElement.TryGetProperty("Certificate", out _));
    }

    [TestMethod]
    public async Task ProcessAsync_ExtractsCertificateFromCertRecord()
    {
        // Build CERT record (type 37) with certificate data
        string base64Cert = GenerateCertificateBase64("CN=cert.example.com");
        byte[] certBytes = Convert.FromBase64String(base64Cert);

        // CERT RDATA format: type(2) + keyTag(2) + algorithm(1) + certificate
        List<byte> certRdata = new();
        certRdata.AddRange(new byte[] { 0x00, 0x01 }); // PKIX type
        certRdata.AddRange(new byte[] { 0x00, 0x00 }); // Key tag
        certRdata.Add(0x00); // Algorithm
        certRdata.AddRange(certBytes);

        byte[] dnsResponse = BuildDnsResponse("cert.example.com", 37, 60, [.. certRdata]);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:cert.example.com?TYPE=CERT"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("CERT", doc.RootElement.GetProperty("Type").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("Certificate", out var certElement));
        Assert.AreEqual("CN=cert.example.com", certElement.GetProperty("Subject").GetString());
    }

    #endregion

    #region Multiple Answers Tests

    [TestMethod]
    public async Task ProcessAsync_HandlesMultipleAnswers()
    {
        // Build response with multiple A records
        List<byte> response = new();

        // Header
        response.AddRange(new byte[] { 0x00, 0x01 }); // ID
        response.AddRange(new byte[] { 0x81, 0x80 }); // Flags
        response.AddRange(new byte[] { 0x00, 0x01 }); // QDCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x03 }); // ANCOUNT: 3
        response.AddRange(new byte[] { 0x00, 0x00 }); // NSCOUNT: 0
        response.AddRange(new byte[] { 0x00, 0x00 }); // ARCOUNT: 0

        // Question section
        EncodeDnsName(response, "example.com");
        response.AddRange(new byte[] { 0x00, 0x01 }); // TYPE A
        response.AddRange(new byte[] { 0x00, 0x01 }); // CLASS IN

        // Three answer records
        byte[][] ips = [[1, 1, 1, 1], [8, 8, 8, 8], [9, 9, 9, 9]];
        foreach (byte[] ip in ips)
        {
            EncodeDnsName(response, "example.com");
            response.AddRange(new byte[] { 0x00, 0x01 }); // TYPE A
            response.AddRange(new byte[] { 0x00, 0x01 }); // CLASS IN
            response.AddRange(new byte[] { 0x00, 0x00, 0x01, 0x2C }); // TTL: 300
            response.AddRange(new byte[] { 0x00, 0x04 }); // RDLENGTH: 4
            response.AddRange(ip);
        }

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([.. response])
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=A"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        var answers = doc.RootElement.GetProperty("Answers");
        Assert.AreEqual(3, answers.GetArrayLength());
        Assert.AreEqual("1.1.1.1", answers[0].GetProperty("Data").GetString());
        Assert.AreEqual("8.8.8.8", answers[1].GetProperty("Data").GetString());
        Assert.AreEqual("9.9.9.9", answers[2].GetProperty("Data").GetString());
    }

    #endregion

    #region TTL Tests

    [TestMethod]
    public async Task ProcessAsync_PreservesTtlValue()
    {
        byte[] rdata = [1, 2, 3, 4];
        uint expectedTtl = 86400; // 1 day
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, expectedTtl, rdata);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(dnsResponse)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
            }
        });
        using var protocol = new OracleDnsProtocol(handler);
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(new Uri("dns:example.com?TYPE=A"), CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual(expectedTtl, doc.RootElement.GetProperty("Answers")[0].GetProperty("Ttl").GetUInt32());
    }

    #endregion

    #region URI Parsing Edge Cases

    [TestMethod]
    public void BuildQueryName_HandlesPercentEncoding()
    {
        // %2e is encoded dot
        var uri = new Uri("dns:example%2ecom");
        string name = OracleDnsProtocol.BuildQueryName(uri);
        Assert.AreEqual("example.com", name);
    }

    [TestMethod]
    public void BuildQueryName_TrimsTrailingDots()
    {
        var uri = new Uri("dns:example.com.");
        string name = OracleDnsProtocol.BuildQueryName(uri);
        Assert.AreEqual("example.com", name);
    }

    [TestMethod]
    public void BuildQueryName_HandlesCaseInsensitiveNameParam()
    {
        var uri = new Uri("dns:ignored?NAME=override.com");
        string name = OracleDnsProtocol.BuildQueryName(uri);
        Assert.AreEqual("override.com", name);
    }

    #endregion

    #region User-Specified Authority Tests

    [TestMethod]
    public async Task ProcessAsync_UsesAuthorityFromUri()
    {
        byte[] rdata = [1, 2, 3, 4];
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, 300, rdata);

        Uri capturedUri = null;
        var handler = new StubHandler(request =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dnsResponse)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
                }
            };
        });
        using var protocol = new OracleDnsProtocol(handler);

        // Use authority syntax: dns://custom-resolver.example/domain
        (OracleResponseCode code, _) = await protocol.ProcessAsync(
            new Uri("dns://custom-resolver.example/example.com?TYPE=A"),
            CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.IsNotNull(capturedUri);
        Assert.AreEqual("custom-resolver.example", capturedUri.Host);
        Assert.AreEqual("/dns-query", capturedUri.AbsolutePath);
        Assert.AreEqual("https", capturedUri.Scheme);
    }

    [TestMethod]
    public async Task ProcessAsync_FallsBackToConfiguredEndpoint()
    {
        byte[] rdata = [1, 2, 3, 4];
        byte[] dnsResponse = BuildDnsResponse("example.com", 1, 300, rdata);

        Uri capturedUri = null;
        var handler = new StubHandler(request =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(dnsResponse)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message") }
                }
            };
        });
        using var protocol = new OracleDnsProtocol(handler);

        // No authority - should use configured endpoint
        (OracleResponseCode code, _) = await protocol.ProcessAsync(
            new Uri("dns:example.com?TYPE=A"),
            CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.IsNotNull(capturedUri);
        // Should use the configured endpoint from LoadSettings()
        Assert.AreEqual("unit.test", capturedUri.Host);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_UserSpecifiedAuthority_GoogleDoH()
    {
        LoadSettingsWithEndpoint("https://cloudflare-dns.com/dns-query"); // Configure Cloudflare as default
        using var protocol = new OracleDnsProtocol();

        // But use Google via authority
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(
            new Uri("dns://dns.google/google.com?TYPE=A"),
            CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.IsNotNull(payload);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("google.com", doc.RootElement.GetProperty("Name").GetString());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_UserSpecifiedAuthority_CloudflareDoH()
    {
        LoadSettingsWithEndpoint("https://dns.google/dns-query"); // Configure Google as default
        using var protocol = new OracleDnsProtocol();

        // But use Cloudflare via authority
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(
            new Uri("dns://cloudflare-dns.com/cloudflare.com?TYPE=A"),
            CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.IsNotNull(payload);
        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual("cloudflare.com", doc.RootElement.GetProperty("Name").GetString());
    }

    #endregion

    #region Real DoH Integration Tests (RFC 8484)

    /// <summary>
    /// Integration test against Cloudflare DoH (https://cloudflare-dns.com/dns-query).
    /// Verifies RFC 8484 application/dns-message format works with real service.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_CloudflareDoH_ResolvesARecord()
    {
        await TestRealDoHEndpoint("https://cloudflare-dns.com/dns-query", "cloudflare.com", "A");
    }

    /// <summary>
    /// Integration test against Google DoH (https://dns.google/dns-query).
    /// Verifies RFC 8484 application/dns-message format works with real service.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_GoogleDoH_ResolvesARecord()
    {
        await TestRealDoHEndpoint("https://dns.google/dns-query", "google.com", "A");
    }

    /// <summary>
    /// Integration test against Quad9 DoH (https://dns.quad9.net/dns-query).
    /// Verifies RFC 8484 application/dns-message format works with real service.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_Quad9DoH_ResolvesARecord()
    {
        await TestRealDoHEndpoint("https://dns.quad9.net/dns-query", "quad9.net", "A");
    }

    /// <summary>
    /// Integration test for TXT record resolution.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_CloudflareDoH_ResolvesTxtRecord()
    {
        await TestRealDoHEndpoint("https://cloudflare-dns.com/dns-query", "cloudflare.com", "TXT");
    }

    /// <summary>
    /// Integration test for AAAA (IPv6) record resolution.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_GoogleDoH_ResolvesAAAARecord()
    {
        await TestRealDoHEndpoint("https://dns.google/dns-query", "google.com", "AAAA");
    }

    /// <summary>
    /// Integration test for NXDOMAIN response.
    /// </summary>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Integration_CloudflareDoH_ReturnsNotFoundForNxDomain()
    {
        LoadSettingsWithEndpoint("https://cloudflare-dns.com/dns-query");
        using var protocol = new OracleDnsProtocol();
        (OracleResponseCode code, _) = await protocol.ProcessAsync(
            new Uri("dns:this-domain-does-not-exist-12345.invalid"),
            CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.NotFound, code);
    }

    private static async Task TestRealDoHEndpoint(string endpoint, string domain, string recordType)
    {
        LoadSettingsWithEndpoint(endpoint);
        using var protocol = new OracleDnsProtocol();
        (OracleResponseCode code, string payload) = await protocol.ProcessAsync(
            new Uri($"dns:{domain}?TYPE={recordType}"),
            CancellationToken.None);

        Assert.AreEqual(OracleResponseCode.Success, code, $"Failed to resolve {domain} via {endpoint}");
        Assert.IsNotNull(payload);

        using JsonDocument doc = JsonDocument.Parse(payload);
        Assert.AreEqual(domain, doc.RootElement.GetProperty("Name").GetString());
        Assert.AreEqual(recordType, doc.RootElement.GetProperty("Type").GetString());

        var answers = doc.RootElement.GetProperty("Answers");
        Assert.IsGreaterThan(0, answers.GetArrayLength(), $"Expected at least one answer for {domain}");
    }

    private static void LoadSettingsWithEndpoint(string dnsEndpoint)
    {
        var values = new Dictionary<string, string>
        {
            ["PluginConfiguration:Network"] = "5195086",
            ["PluginConfiguration:Nodes:0"] = "http://127.0.0.1:20332",
            ["PluginConfiguration:AllowedContentTypes:0"] = "application/json",
            ["PluginConfiguration:Https:Timeout"] = "5000",
            ["PluginConfiguration:NeoFS:EndPoint"] = "http://127.0.0.1:8080",
            ["PluginConfiguration:NeoFS:Timeout"] = "15000",
            ["PluginConfiguration:Dns:EndPoint"] = dnsEndpoint,
            ["PluginConfiguration:Dns:Timeout"] = "10000"
        };
        IConfigurationSection section = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection("PluginConfiguration");
        OracleSettings.Load(section);
    }

    #endregion

    private static void LoadSettings()
    {
        var values = new Dictionary<string, string>
        {
            ["PluginConfiguration:Network"] = "5195086",
            ["PluginConfiguration:Nodes:0"] = "http://127.0.0.1:20332",
            ["PluginConfiguration:AllowedContentTypes:0"] = "application/json",
            ["PluginConfiguration:Https:Timeout"] = "5000",
            ["PluginConfiguration:NeoFS:EndPoint"] = "http://127.0.0.1:8080",
            ["PluginConfiguration:NeoFS:Timeout"] = "15000",
            ["PluginConfiguration:Dns:EndPoint"] = "https://unit.test/dns-query",
            ["PluginConfiguration:Dns:Timeout"] = "3000"
        };
        IConfigurationSection section = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection("PluginConfiguration");
        OracleSettings.Load(section);
    }

    private static string GenerateCertificateBase64(string subject)
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
    }

    private static string GenerateEcCertificateBase64(string subject)
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
    }

    /// <summary>
    /// Builds a DNS response message in wire format (RFC 1035).
    /// </summary>
    private static byte[] BuildDnsResponse(string name, ushort type, uint ttl, byte[] rdata)
    {
        List<byte> response = new();

        // Header (12 bytes)
        response.AddRange(new byte[] { 0x00, 0x01 }); // ID
        response.AddRange(new byte[] { 0x81, 0x80 }); // Flags: response, recursion desired, recursion available
        response.AddRange(new byte[] { 0x00, 0x01 }); // QDCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x01 }); // ANCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x00 }); // NSCOUNT: 0
        response.AddRange(new byte[] { 0x00, 0x00 }); // ARCOUNT: 0

        // Question section
        EncodeDnsName(response, name);
        response.Add((byte)(type >> 8));
        response.Add((byte)(type & 0xFF));
        response.Add(0x00);
        response.Add(0x01); // CLASS IN

        // Answer section
        EncodeDnsName(response, name);
        response.Add((byte)(type >> 8));
        response.Add((byte)(type & 0xFF));
        response.Add(0x00);
        response.Add(0x01); // CLASS IN

        // TTL (4 bytes)
        response.Add((byte)(ttl >> 24));
        response.Add((byte)(ttl >> 16));
        response.Add((byte)(ttl >> 8));
        response.Add((byte)(ttl & 0xFF));

        // RDLENGTH and RDATA
        response.Add((byte)(rdata.Length >> 8));
        response.Add((byte)(rdata.Length & 0xFF));
        response.AddRange(rdata);

        return [.. response];
    }

    /// <summary>
    /// Builds a DNS response with a specific RCODE (no answers).
    /// </summary>
    private static byte[] BuildDnsResponseWithRcode(string name, ushort type, int rcode)
    {
        List<byte> response = new();

        // Header (12 bytes)
        response.AddRange(new byte[] { 0x00, 0x01 }); // ID
        response.Add(0x81); // QR=1, Opcode=0, AA=0, TC=0, RD=1
        response.Add((byte)(0x80 | (rcode & 0x0F))); // RA=1, Z=0, RCODE
        response.AddRange(new byte[] { 0x00, 0x01 }); // QDCOUNT: 1
        response.AddRange(new byte[] { 0x00, 0x00 }); // ANCOUNT: 0
        response.AddRange(new byte[] { 0x00, 0x00 }); // NSCOUNT: 0
        response.AddRange(new byte[] { 0x00, 0x00 }); // ARCOUNT: 0

        // Question section
        EncodeDnsName(response, name);
        response.Add((byte)(type >> 8));
        response.Add((byte)(type & 0xFF));
        response.Add(0x00);
        response.Add(0x01); // CLASS IN

        return [.. response];
    }

    /// <summary>
    /// Encodes a domain name in DNS wire format.
    /// </summary>
    private static void EncodeDnsName(List<byte> buffer, string name)
    {
        string[] labels = name.TrimEnd('.').Split('.');
        foreach (string label in labels)
        {
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);
            buffer.Add((byte)labelBytes.Length);
            buffer.AddRange(labelBytes);
        }
        buffer.Add(0x00); // Root label
    }

    /// <summary>
    /// Builds TXT record RDATA (length-prefixed strings).
    /// </summary>
    private static byte[] BuildTxtRdata(string text)
    {
        List<byte> rdata = new();
        byte[] textBytes = Encoding.UTF8.GetBytes(text);

        // TXT records are split into 255-byte chunks
        int offset = 0;
        while (offset < textBytes.Length)
        {
            int chunkLength = Math.Min(255, textBytes.Length - offset);
            rdata.Add((byte)chunkLength);
            rdata.AddRange(textBytes.Skip(offset).Take(chunkLength));
            offset += chunkLength;
        }

        return [.. rdata];
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
