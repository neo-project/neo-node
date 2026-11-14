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
using Neo.Network.P2P.Payloads;
using Neo.Plugins.OracleService.Protocols;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Neo.Plugins.OracleService.Tests;

[TestClass]
public class UT_OracleHttpsProtocol
{
    private static readonly Uri s_uri = new("https://example.com/oracle");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Oracle:AllowPrivateHost"] = "true" })
            .Build().GetSection("Oracle");
        OracleSettings.Load(section);
    }

    private sealed class TrackedResponse(HttpStatusCode status) : HttpResponseMessage(status)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeStream(byte[] data, int chunk = int.MaxValue, Func<CancellationToken, Task> onRead = null) : Stream
    {
        private int _position;
        public int BytesRead => _position;
        public int ReadCalls { get; private set; }
        public bool Disposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            if (onRead != null) await onRead(cancellationToken);
            int n = Math.Min(Math.Min(chunk, buffer.Length), data.Length - _position);
            data.AsMemory(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }

    private static TrackedResponse CreateResponse(FakeStream stream, string contentType = "application/json",
        HttpStatusCode status = HttpStatusCode.OK, long? contentLength = null)
    {
        var response = new TrackedResponse(status) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (contentLength.HasValue) response.Content.Headers.ContentLength = contentLength;
        return response;
    }

    private static async Task<(OracleResponseCode Code, string Result)> RunAsync(
        Func<HttpRequestMessage, HttpResponseMessage> respond, CancellationToken token = default)
    {
        using var protocol = new OracleHttpsProtocol(new FakeHandler(respond));
        return await protocol.ProcessAsync(s_uri, token);
    }

    private static byte[] Ascii(int length) => Enumerable.Repeat((byte)'a', length).ToArray();

    [TestMethod]
    public async Task ProcessAsync_SmallResponse_ReturnsSuccess()
    {
        var json = "{\"price\":42}";
        var stream = new FakeStream(Encoding.UTF8.GetBytes(json));
        var (code, result) = await RunAsync(_ => CreateResponse(stream));
        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.AreEqual(json, result);
    }

    [TestMethod]
    public async Task ProcessAsync_ExactlyMaxSize_IsAccepted()
    {
        var stream = new FakeStream(Ascii(OracleResponse.MaxResultSize));
        var (code, result) = await RunAsync(_ => CreateResponse(stream));
        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.AreEqual(OracleResponse.MaxResultSize, result.Length);
    }

    [TestMethod]
    public async Task ProcessAsync_OneByteOverMax_ReturnsResponseTooLarge()
    {
        var stream = new FakeStream(Ascii(OracleResponse.MaxResultSize + 1));
        var (code, result) = await RunAsync(_ => CreateResponse(stream));
        Assert.AreEqual(OracleResponseCode.ResponseTooLarge, code);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ProcessAsync_DeclaredContentLengthOverMax_DoesNotReadBody()
    {
        var stream = new FakeStream(Ascii(OracleResponse.MaxResultSize + 1));
        TrackedResponse response = null!;
        var (code, _) = await RunAsync(_ => response = CreateResponse(stream, contentLength: OracleResponse.MaxResultSize + 1));
        Assert.AreEqual(OracleResponseCode.ResponseTooLarge, code);
        Assert.AreEqual(0, stream.ReadCalls);
        Assert.IsTrue(response.Disposed);
        Assert.IsTrue(stream.Disposed);
    }

    [TestMethod]
    public async Task ProcessAsync_NoContentLengthOverMax_StopsReadingAtLimit()
    {
        var stream = new FakeStream(Ascii(OracleResponse.MaxResultSize * 10), chunk: 4096);
        TrackedResponse response = null!;
        long? declaredLength = 0;
        var (code, _) = await RunAsync(_ =>
        {
            response = CreateResponse(stream);
            declaredLength = response.Content.Headers.ContentLength;
            return response;
        });
        Assert.IsNull(declaredLength);
        Assert.AreEqual(OracleResponseCode.ResponseTooLarge, code);
        Assert.AreEqual(OracleResponse.MaxResultSize + 1, stream.BytesRead);
        Assert.IsTrue(stream.Disposed);
    }

    [TestMethod]
    public async Task ProcessAsync_PartialReads_ReconstructsFullContent()
    {
        var json = "{\"price\":42,\"name\":\"neo\"}";
        var stream = new FakeStream(Encoding.UTF8.GetBytes(json), chunk: 3);
        var (code, result) = await RunAsync(_ => CreateResponse(stream));
        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.AreEqual(json, result);
        Assert.IsGreaterThan(1, stream.ReadCalls);
    }

    [TestMethod]
    public async Task ProcessAsync_CancelledWhileReading_PropagatesCancellationAndDisposes()
    {
        using var cts = new CancellationTokenSource();
        var stream = new FakeStream([], onRead: async token =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, token);
        });
        TrackedResponse response = null!;
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => RunAsync(_ => response = CreateResponse(stream), cts.Token));
        Assert.IsTrue(response.Disposed);
        Assert.IsTrue(stream.Disposed);
    }

    [TestMethod]
    public async Task ProcessAsync_DisposesResponses_OnSuccessErrorAndRedirect()
    {
        var ok = new FakeStream(Encoding.UTF8.GetBytes("{}"));
        TrackedResponse success = null!;
        Assert.AreEqual(OracleResponseCode.Success, (await RunAsync(_ => success = CreateResponse(ok))).Code);
        Assert.IsTrue(success.Disposed);
        Assert.IsTrue(ok.Disposed);

        TrackedResponse error = null!;
        var errorStream = new FakeStream([]);
        Assert.AreEqual(OracleResponseCode.Error, (await RunAsync(_ => error = CreateResponse(errorStream, status: HttpStatusCode.InternalServerError))).Code);
        Assert.IsTrue(error.Disposed);

        TrackedResponse redirect = null!, final = null!;
        var finalStream = new FakeStream(Encoding.UTF8.GetBytes("{}"));
        int calls = 0;
        var (code, _) = await RunAsync(_ =>
        {
            if (calls++ == 0)
            {
                redirect = CreateResponse(new FakeStream([]), status: HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://example.com/next");
                return redirect;
            }
            return final = CreateResponse(finalStream);
        });
        Assert.AreEqual(OracleResponseCode.Success, code);
        Assert.IsTrue(redirect.Disposed);
        Assert.IsTrue(final.Disposed);
    }

    [TestMethod]
    public async Task ProcessAsync_UnsupportedContentType_IsRejected()
    {
        var stream = new FakeStream(Encoding.UTF8.GetBytes("<html/>"));
        TrackedResponse response = null!;
        var (code, _) = await RunAsync(_ => response = CreateResponse(stream, "text/html"));
        Assert.AreEqual(OracleResponseCode.ContentTypeNotSupported, code);
        Assert.AreEqual(0, stream.ReadCalls);
        Assert.IsTrue(response.Disposed);
    }

    [TestMethod]
    public async Task ProcessAsync_UnsupportedCharset_ReturnsError()
    {
        var stream = new FakeStream(Encoding.Unicode.GetBytes("{}"));
        var (code, _) = await RunAsync(_ => CreateResponse(stream, "application/json; charset=utf-16"));
        Assert.AreEqual(OracleResponseCode.Error, code);
    }

    [TestMethod]
    public async Task ProcessAsync_UnderstatedContentLength_StillEnforcesLimitOnActualBytes()
    {
        var stream = new FakeStream(Ascii(OracleResponse.MaxResultSize * 4), chunk: 1024);
        var (code, _) = await RunAsync(_ => CreateResponse(stream, contentLength: 10));
        Assert.AreEqual(OracleResponseCode.ResponseTooLarge, code);
        Assert.AreEqual(OracleResponse.MaxResultSize + 1, stream.BytesRead);
    }
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
