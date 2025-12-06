// Copyright (C) 2015-2025 The Neo Project.
//
// OracleDnsProtocol.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Neo.Plugins.OracleService.Protocols;

/// <summary>
/// DNS oracle protocol implementing RFC 8484 (DNS over HTTPS) with application/dns-message format.
/// </summary>
class OracleDnsProtocol : IOracleProtocol
{
    private const ushort DnsClassIN = 1;
    private const int DnsHeaderSize = 12;
    private static readonly MediaTypeHeaderValue DnsMessageMediaType = new("application/dns-message");
    private sealed class ResponseTooLargeException : Exception { }

    /// <summary>
    /// Represents a parsed DNS resource record from wire format (RFC 1035).
    /// </summary>
    private sealed class DnsResourceRecord
    {
        public string Name { get; set; }
        public ushort Type { get; set; }
        public ushort Class { get; set; }
        public uint Ttl { get; set; }
        public byte[] RData { get; set; }
    }

    /// <summary>
    /// Represents a parsed DNS response message (RFC 1035).
    /// </summary>
    private sealed class DnsMessage
    {
        public ushort Id { get; set; }
        public ushort Flags { get; set; }
        public int ResponseCode => Flags & 0x0F;
        public List<DnsResourceRecord> Answers { get; set; } = [];
    }

    private sealed class ResultAnswer
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public uint Ttl { get; set; }
        public string Data { get; set; }
    }

    private sealed class ResultEnvelope
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public ResultAnswer[] Answers { get; set; }
    }

    private static readonly IReadOnlyDictionary<string, int> RecordTypeLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = 1,
        ["NS"] = 2,
        ["CNAME"] = 5,
        ["SOA"] = 6,
        ["MX"] = 15,
        ["TXT"] = 16,
        ["AAAA"] = 28,
        ["SRV"] = 33,
        ["CERT"] = 37,
        ["DNSKEY"] = 48,
        ["TLSA"] = 52,
    };

    private static readonly IReadOnlyDictionary<int, string> ReverseRecordTypeLookup =
        RecordTypeLookup.ToDictionary(p => p.Value, p => p.Key);

    private readonly HttpClient client;
    private readonly JsonSerializerOptions resultSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly object syncRoot = new();
    private bool configured;
    private Uri endpoint;

    public OracleDnsProtocol(HttpMessageHandler handler = null)
    {
        // Do not allow automatic redirects; resolver endpoints must be explicitly allowed.
        client = handler is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        CustomAttributeData attribute = Assembly.GetExecutingAssembly().CustomAttributes.First(p => p.AttributeType == typeof(AssemblyInformationalVersionAttribute));
        string version = (string)attribute.ConstructorArguments[0].Value;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NeoOracleService", version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));
    }

    public void Configure()
    {
        EnsureConfigured(force: true);
    }

    public void Dispose()
    {
        client.Dispose();
    }

    public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
    {
        EnsureConfigured();

        string queryName;
        NameValueCollection query;
        Uri resolverEndpoint;
        try
        {
            query = ParseQueryString(uri.Query);
            queryName = BuildQueryName(uri, query);
            ValidateClass(query);
            resolverEndpoint = GetResolverEndpoint(uri);
        }
        catch (Exception ex)
        {
            return (OracleResponseCode.Error, ex.Message);
        }

        int recordType;
        string recordTypeLabel;
        try
        {
            recordType = ParseRecordType(query);
            recordTypeLabel = GetRecordTypeLabel(recordType);
        }
        catch (Exception ex)
        {
            return (OracleResponseCode.Error, ex.Message);
        }

        Utility.Log(nameof(OracleDnsProtocol), LogLevel.Debug, $"Request: {queryName} ({recordTypeLabel}) via {resolverEndpoint.Host}");

        DnsMessage dnsResponse;
        try
        {
            dnsResponse = await ResolveAsync(queryName, (ushort)recordType, resolverEndpoint, cancellation);
        }
        catch (TaskCanceledException)
        {
            return (OracleResponseCode.Timeout, null);
        }
        catch (ResponseTooLargeException)
        {
            return (OracleResponseCode.ResponseTooLarge, null);
        }
        catch (Exception ex)
        {
            return (OracleResponseCode.Error, ex.Message);
        }

        if (dnsResponse is null)
            return (OracleResponseCode.Error, "Invalid DNS response.");

        // RCODE 3 = NXDOMAIN
        if (dnsResponse.ResponseCode == 3)
            return (OracleResponseCode.NotFound, null);

        if (dnsResponse.ResponseCode != 0)
            return (OracleResponseCode.Error, $"DNS error (RCODE {dnsResponse.ResponseCode})");

        if (dnsResponse.Answers is null || dnsResponse.Answers.Count == 0)
            return (OracleResponseCode.NotFound, null);

        ResultAnswer[] answers = dnsResponse.Answers
            .Select(a => new ResultAnswer
            {
                Name = a.Name?.TrimEnd('.'),
                Type = GetRecordTypeLabel(a.Type),
                Ttl = a.Ttl,
                Data = FormatRData(a.Type, a.RData)
            })
            .ToArray();

        ResultEnvelope envelope = new()
        {
            Name = queryName,
            Type = recordTypeLabel,
            Answers = answers
        };

        string payload = JsonSerializer.Serialize(envelope, resultSerializerOptions);
        if (Encoding.UTF8.GetByteCount(payload) > OracleResponse.MaxResultSize)
            return (OracleResponseCode.ResponseTooLarge, null);

        return (OracleResponseCode.Success, payload);
    }

    /// <summary>
    /// Sends a DNS query using RFC 8484 POST method with application/dns-message format.
    /// </summary>
    private async Task<DnsMessage> ResolveAsync(string name, ushort type, Uri resolverEndpoint, CancellationToken cancellation)
    {
        byte[] queryMessage = BuildDnsQuery(name, type);

        using ByteArrayContent content = new(queryMessage);
        content.Headers.ContentType = DnsMessageMediaType;

        await EnsureEndpointAllowed(resolverEndpoint, cancellation);

        using HttpResponseMessage response = await client.PostAsync(resolverEndpoint, content, cancellation);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DoH endpoint returned {(int)response.StatusCode} ({response.StatusCode})");

        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength > OracleResponse.MaxResultSize)
            throw new ResponseTooLargeException();

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellation);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellation)) > 0)
        {
            if (buffer.Length + read > OracleResponse.MaxResultSize)
                throw new ResponseTooLargeException();
            buffer.Write(chunk, 0, read);
        }

        byte[] responseData = buffer.ToArray();
        return ParseDnsResponse(responseData);
    }

    private async Task EnsureEndpointAllowed(Uri resolverEndpoint, CancellationToken cancellation)
    {
        if (OracleSettings.Default.AllowPrivateHost)
            return;

        if (IsPrivateEndpoint(resolverEndpoint.Host))
            throw new InvalidOperationException("Private resolver endpoints are not allowed.");

        try
        {
            IPHostEntry entry = await Dns.GetHostEntryAsync(resolverEndpoint.Host, cancellation);
            if (entry.IsInternal())
                throw new InvalidOperationException("Private resolver endpoints are not allowed.");
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"Failed to resolve resolver endpoint: {ex.Message}", ex);
        }
    }

    private static bool IsPrivateEndpoint(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(host, out IPAddress address))
            return address.IsInternal();

        return false;
    }

    /// <summary>
    /// Gets the DoH resolver endpoint from the URI authority or falls back to the configured default.
    /// Per RFC 4501, the authority component specifies the DNS server to query.
    /// </summary>
    private Uri GetResolverEndpoint(Uri uri)
    {
        // Check if URI has an authority (e.g., dns://resolver.example.com/domain)
        if (!string.IsNullOrEmpty(uri.Host))
        {
            // Build DoH endpoint from the authority
            // Default to HTTPS and /dns-query path per RFC 8484
            UriBuilder builder = new()
            {
                Scheme = "https",
                Host = uri.Host,
                Path = "/dns-query"
            };

            if (uri.Port > 0 && uri.Port != 443)
                builder.Port = uri.Port;

            return builder.Uri;
        }

        // Fall back to configured endpoint
        return endpoint;
    }

    /// <summary>
    /// Builds a DNS query message in wire format (RFC 1035).
    /// </summary>
    internal static byte[] BuildDnsQuery(string name, ushort type)
    {
        // Estimate size: header (12) + name (name.Length + 2 for length bytes + 1 for null) + type (2) + class (2)
        List<byte> message = new(DnsHeaderSize + name.Length + 5 + 4);

        // Header section (12 bytes)
        // ID: random identifier
        ushort id = (ushort)Random.Shared.Next(0, 65536);
        message.Add((byte)(id >> 8));
        message.Add((byte)(id & 0xFF));

        // Flags: standard query, recursion desired (0x0100)
        message.Add(0x01);
        message.Add(0x00);

        // QDCOUNT: 1 question
        message.Add(0x00);
        message.Add(0x01);

        // ANCOUNT, NSCOUNT, ARCOUNT: 0
        message.AddRange(new byte[6]);

        // Question section
        // QNAME: domain name in label format
        EncodeDnsName(message, name);

        // QTYPE
        message.Add((byte)(type >> 8));
        message.Add((byte)(type & 0xFF));

        // QCLASS: IN (1)
        message.Add(0x00);
        message.Add(0x01);

        return [.. message];
    }

    /// <summary>
    /// Encodes a domain name in DNS wire format (RFC 1035 section 4.1.2).
    /// </summary>
    private static void EncodeDnsName(List<byte> buffer, string name)
    {
        if (string.IsNullOrEmpty(name) || name == ".")
        {
            buffer.Add(0x00);
            return;
        }

        string[] labels = name.TrimEnd('.').Split('.');
        foreach (string label in labels)
        {
            if (label.Length > 63)
                throw new FormatException($"DNS label exceeds 63 characters: {label}");
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);
            buffer.Add((byte)labelBytes.Length);
            buffer.AddRange(labelBytes);
        }
        buffer.Add(0x00); // Root label
    }

    /// <summary>
    /// Parses a DNS response message from wire format (RFC 1035).
    /// </summary>
    private static DnsMessage ParseDnsResponse(byte[] data)
    {
        if (data is null || data.Length < DnsHeaderSize)
            throw new FormatException("DNS response too short.");

        DnsMessage message = new()
        {
            Id = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0, 2)),
            Flags = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2, 2))
        };

        ushort qdCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2));
        ushort anCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6, 2));

        int offset = DnsHeaderSize;

        // Skip question section
        for (int i = 0; i < qdCount; i++)
        {
            offset = SkipDnsName(data, offset);
            offset += 4; // QTYPE (2) + QCLASS (2)
        }

        // Parse answer section
        for (int i = 0; i < anCount; i++)
        {
            (string name, int newOffset) = DecodeDnsName(data, offset);
            offset = newOffset;

            if (offset + 10 > data.Length)
                throw new FormatException("DNS response truncated in answer section.");

            ushort type = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
            ushort cls = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2));
            uint ttl = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4, 4));
            ushort rdLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 8, 2));
            offset += 10;

            if (offset + rdLength > data.Length)
                throw new FormatException("DNS response truncated in RDATA.");

            byte[] rdata = new byte[rdLength];
            Array.Copy(data, offset, rdata, 0, rdLength);
            offset += rdLength;

            message.Answers.Add(new DnsResourceRecord
            {
                Name = name,
                Type = type,
                Class = cls,
                Ttl = ttl,
                RData = rdata
            });
        }

        return message;
    }

    /// <summary>
    /// Decodes a DNS name from wire format, handling compression (RFC 1035 section 4.1.4).
    /// </summary>
    private static (string Name, int NewOffset) DecodeDnsName(byte[] data, int offset)
    {
        StringBuilder name = new();
        int originalOffset = offset;
        bool jumped = false;
        int jumpCount = 0;
        const int maxJumps = 128; // Prevent infinite loops

        while (offset < data.Length)
        {
            byte length = data[offset];

            if (length == 0)
            {
                offset++;
                break;
            }

            // Check for compression pointer (top 2 bits set)
            if ((length & 0xC0) == 0xC0)
            {
                if (offset + 1 >= data.Length)
                    throw new FormatException("DNS name compression pointer truncated.");

                if (++jumpCount > maxJumps)
                    throw new FormatException("DNS name compression loop detected.");

                int pointer = ((length & 0x3F) << 8) | data[offset + 1];
                if (!jumped)
                {
                    originalOffset = offset + 2;
                    jumped = true;
                }
                offset = pointer;
                continue;
            }

            offset++;
            if (offset + length > data.Length)
                throw new FormatException("DNS label extends beyond message.");

            if (name.Length > 0)
                name.Append('.');

            name.Append(Encoding.ASCII.GetString(data, offset, length));
            offset += length;
        }

        return (name.ToString(), jumped ? originalOffset : offset);
    }

    /// <summary>
    /// Skips over a DNS name in wire format.
    /// </summary>
    private static int SkipDnsName(byte[] data, int offset)
    {
        while (offset < data.Length)
        {
            byte length = data[offset];

            if (length == 0)
                return offset + 1;

            // Compression pointer
            if ((length & 0xC0) == 0xC0)
                return offset + 2;

            offset += 1 + length;
        }

        throw new FormatException("DNS name extends beyond message.");
    }

    /// <summary>
    /// Formats RDATA based on record type for human-readable output.
    /// </summary>
    private static string FormatRData(ushort type, byte[] rdata)
    {
        if (rdata is null || rdata.Length == 0)
            return string.Empty;

        return type switch
        {
            1 when rdata.Length == 4 => new IPAddress(rdata).ToString(), // A
            28 when rdata.Length == 16 => new IPAddress(rdata).ToString(), // AAAA
            16 => FormatTxtRecord(rdata), // TXT
            37 => FormatCertRecord(rdata), // CERT
            _ => Convert.ToBase64String(rdata)
        };
    }

    /// <summary>
    /// Formats a TXT record (RFC 1035 section 3.3.14).
    /// </summary>
    private static string FormatTxtRecord(byte[] rdata)
    {
        StringBuilder result = new();
        int offset = 0;

        while (offset < rdata.Length)
        {
            int length = rdata[offset++];
            if (offset + length > rdata.Length)
                break;

            if (result.Length > 0)
                result.Append(' ');

            result.Append('"');
            result.Append(Encoding.UTF8.GetString(rdata, offset, length));
            result.Append('"');
            offset += length;
        }

        return result.ToString();
    }

    /// <summary>
    /// Formats a CERT record (RFC 4398).
    /// </summary>
    private static string FormatCertRecord(byte[] rdata)
    {
        if (rdata.Length < 5)
            return Convert.ToBase64String(rdata);

        ushort certType = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
        ushort keyTag = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(2, 2));
        byte algorithm = rdata[4];
        byte[] certData = rdata[5..];

        return $"{certType} {keyTag} {algorithm} {Convert.ToBase64String(certData)}";
    }

    private void EnsureConfigured(bool force = false)
    {
        if (configured && !force)
            return;
        lock (syncRoot)
        {
            if (configured && !force)
                return;
            var dnsSettings = OracleSettings.Default?.Dns ?? throw new InvalidOperationException("DNS settings are not loaded.");
            endpoint = dnsSettings.EndPoint;
            client.Timeout = dnsSettings.Timeout;
            configured = true;
        }
    }

    internal static string BuildQueryName(Uri uri, NameValueCollection queryParameters = null)
    {
        queryParameters ??= ParseQueryString(uri.Query);
        string dnsName = NormalizeDnsName(uri.GetComponents(UriComponents.Path, UriFormat.Unescaped));
        if (string.IsNullOrEmpty(dnsName))
            throw new FormatException("dns: URI must include a dnsname.");

        return dnsName;
    }

    private static NameValueCollection ParseQueryString(string query)
    {
        string normalized = string.IsNullOrEmpty(query)
            ? string.Empty
            : query.TrimStart('?').Replace(';', '&');
        return HttpUtility.ParseQueryString(normalized);
    }

    private static string GetQueryValue(NameValueCollection query, string key)
    {
        if (query is null)
            return null;
        foreach (string existing in query)
        {
            if (existing is null)
                continue;
            if (existing.Equals(key, StringComparison.OrdinalIgnoreCase))
                return query[existing];
        }
        return null;
    }

    private static void ValidateClass(NameValueCollection query)
    {
        string classRaw = GetQueryValue(query, "class");
        if (string.IsNullOrWhiteSpace(classRaw))
            return;
        classRaw = classRaw.Trim();
        if (classRaw.Equals("IN", StringComparison.OrdinalIgnoreCase) || classRaw == "1")
            return;
        throw new FormatException($"Unsupported DNS class '{classRaw}', only IN is supported.");
    }

    private static string NormalizeDnsName(string value)
    {
        string normalized = NormalizeLabel(value?.Trim('/'));
        if (string.IsNullOrEmpty(normalized))
            throw new FormatException("dns: URI must include a dnsname.");
        if (normalized.Contains('/'))
            throw new FormatException("dnsname must not contain path segments.");
        return normalized;
    }

    private static string NormalizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Uri.UnescapeDataString(value).Trim().Trim('.');
    }

    private static int ParseRecordType(NameValueCollection query)
    {
        string typeRaw = GetQueryValue(query, "type");
        if (string.IsNullOrWhiteSpace(typeRaw))
            return RecordTypeLookup["A"];
        typeRaw = typeRaw.Trim();
        if (int.TryParse(typeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            if (numeric < 0 || numeric > ushort.MaxValue)
                throw new FormatException($"Unsupported DNS record type '{typeRaw}'");
            return numeric;
        }
        if (RecordTypeLookup.TryGetValue(typeRaw, out int mapped))
            return mapped;
        throw new FormatException($"Unsupported DNS record type '{typeRaw}'");
    }

    private static string GetRecordTypeLabel(int type)
    {
        if (ReverseRecordTypeLookup.TryGetValue(type, out string label))
            return label;
        return type.ToString(CultureInfo.InvariantCulture);
    }
}
