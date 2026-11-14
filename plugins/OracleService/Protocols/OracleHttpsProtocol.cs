// Copyright (C) 2015-2026 The Neo Project.
//
// OracleHttpsProtocol.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace Neo.Plugins.OracleService.Protocols;

class OracleHttpsProtocol : IOracleProtocol
{
    private readonly HttpClient client;

    public OracleHttpsProtocol() : this(new HttpClientHandler() { AllowAutoRedirect = false })
    {
    }

    internal OracleHttpsProtocol(HttpMessageHandler handler)
    {
        client = new HttpClient(handler);
        CustomAttributeData attribute = Assembly.GetExecutingAssembly().CustomAttributes.First(p => p.AttributeType == typeof(AssemblyInformationalVersionAttribute));
        string version = (string)attribute.ConstructorArguments[0].Value;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NeoOracleService", version));
    }

    public void Configure()
    {
        client.DefaultRequestHeaders.Accept.Clear();
        foreach (string type in OracleSettings.Default.AllowedContentTypes)
            client.DefaultRequestHeaders.Accept.ParseAdd(type);
        client.Timeout = OracleSettings.Default.Https.Timeout;
    }

    public void Dispose()
    {
        client.Dispose();
    }

    public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
    {
        OracleService.PluginLogger?.Debug("Request: {Uri}", uri.AbsoluteUri);

        HttpResponseMessage message;
        try
        {
            int redirects = OracleSettings.Default.Https.MaxRedirects;
            do
            {
                if (!OracleSettings.Default.AllowPrivateHost)
                {
                    IPHostEntry entry = await Dns.GetHostEntryAsync(uri.Host, cancellation);
                    if (entry.IsInternal())
                        return (OracleResponseCode.Forbidden, null);
                }
                message = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
                if (message.Headers.Location is not null)
                {
                    if (!TryResolveHttpsRedirect(uri, message.Headers.Location, out var next))
                    {
                        message.Dispose();
                        return (OracleResponseCode.ProtocolNotSupported, null);
                    }
                    message.Dispose();
                    uri = next;
                    message = null;
                }
            } while (message == null && redirects-- > 0);
        }
        catch
        {
            return (OracleResponseCode.Timeout, null);
        }
        if (message is null)
            return (OracleResponseCode.Timeout, null);

        using var _ = message;
        if (message.StatusCode == HttpStatusCode.NotFound)
            return (OracleResponseCode.NotFound, null);
        if (message.StatusCode == HttpStatusCode.Forbidden)
            return (OracleResponseCode.Forbidden, null);
        if (!message.IsSuccessStatusCode)
            return (OracleResponseCode.Error, message.StatusCode.ToString());
        if (!IsSupportedContentType(message.Content.Headers, OracleSettings.Default.AllowedContentTypes))
            return (OracleResponseCode.ContentTypeNotSupported, null);
        if (message.Content.Headers.ContentLength.HasValue && message.Content.Headers.ContentLength > OracleResponse.MaxResultSize)
            return (OracleResponseCode.ResponseTooLarge, null);

        byte[] buffer = new byte[OracleResponse.MaxResultSize + 1];
        int read;
        await using (var stream = await message.Content.ReadAsStreamAsync(cancellation))
            read = await ReadUpToAsync(stream, buffer, cancellation);

        if (read > OracleResponse.MaxResultSize)
            return (OracleResponseCode.ResponseTooLarge, null);

        var encoding = GetEncoding(message.Content.Headers);
        if (!encoding.Equals(Encoding.UTF8))
            return (OracleResponseCode.Error, null);

        return (OracleResponseCode.Success, buffer.ToStrictUtf8String(0, read));
    }

    /// <summary>
    /// Reads from the stream until the buffer is full or EOF is reached.
    /// A result equal to the buffer length means the stream may contain more data.
    /// </summary>
    internal static async Task<int> ReadUpToAsync(Stream stream, byte[] buffer, CancellationToken cancellation)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellation);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    internal static bool IsSupportedContentType(HttpContentHeaders headers, IEnumerable<string> allowed)
    {
        var mediaType = headers.ContentType?.MediaType;
        return !string.IsNullOrEmpty(mediaType) && allowed.Contains(mediaType);
    }

    internal static Encoding GetEncoding(HttpContentHeaders headers)
    {
        Encoding encoding = null;
        if ((headers.ContentType != null) && (headers.ContentType.CharSet != null))
        {
            encoding = Encoding.GetEncoding(headers.ContentType.CharSet);
        }

        return encoding ?? Encoding.UTF8;
    }

    /// <summary>
    /// Resolves an HTTP Location header against the current request URI.
    /// Relative redirects stay on HTTPS when the current request is HTTPS.
    /// </summary>
    internal static bool TryResolveHttpsRedirect(Uri current, Uri location, out Uri next)
    {
        next = location.IsAbsoluteUri ? location : new Uri(current, location);
        return next.IsAbsoluteUri && next.Scheme == Uri.UriSchemeHttps;
    }
}
