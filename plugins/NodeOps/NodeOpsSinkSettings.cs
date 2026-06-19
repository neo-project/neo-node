// Copyright (C) 2015-2026 The Neo Project.
//
// NodeOpsSinkSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.NodeOps;

internal enum NodeOpsSinkKind
{
    ErrorCollector,
    StatusMonitor,
    Notification
}

internal enum NodeOpsProvider
{
    CustomWebhook,
    Sentry,
    GoogleCloudErrorReporting,
    BetterStackHeartbeat,
    HealthchecksHeartbeat
}

internal enum NodeOpsSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

internal sealed class NodeOpsSinkSettings
{
    public string Name { get; }
    public NodeOpsSinkKind Kind { get; }
    public NodeOpsProvider Provider { get; }
    public Uri? Endpoint { get; }
    public string Method { get; }
    public string Token { get; }
    public string TokenHeader { get; }
    public string TokenScheme { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public NodeOpsSeverity MinimumSeverity { get; }
    public bool Enabled => Endpoint is not null;

    public NodeOpsSinkSettings(IConfigurationSection section)
        : this(
            section.GetValue("Name", string.Empty)!,
            section.GetValue("Kind", NodeOpsSinkKind.ErrorCollector),
            section.GetValue("Provider", NodeOpsProvider.CustomWebhook),
            ParseEndpoint(section.GetValue("Endpoint", string.Empty)),
            section.GetValue("Method", string.Empty)!,
            section.GetValue("Token", string.Empty)!,
            section.GetValue("TokenHeader", "Authorization")!,
            section.GetValue("TokenScheme", "Bearer")!,
            section.GetSection("Headers").GetChildren()
                .Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => p.Value!, StringComparer.OrdinalIgnoreCase),
            section.GetValue("MinimumSeverity", NodeOpsSeverity.Error))
    {
    }

    internal NodeOpsSinkSettings(
        string name,
        NodeOpsSinkKind kind,
        NodeOpsProvider provider,
        Uri? endpoint,
        string method = "",
        string token = "",
        string tokenHeader = "Authorization",
        string tokenScheme = "Bearer",
        IReadOnlyDictionary<string, string>? headers = null,
        NodeOpsSeverity minimumSeverity = NodeOpsSeverity.Error)
    {
        Name = string.IsNullOrWhiteSpace(name) ? provider.ToString() : name;
        Kind = kind;
        Provider = provider;
        Endpoint = endpoint;
        Method = string.IsNullOrWhiteSpace(method) ? GetDefaultMethod(provider) : method.ToUpperInvariant();
        Token = token;
        TokenHeader = tokenHeader;
        TokenScheme = tokenScheme;
        Headers = headers ?? new Dictionary<string, string>();
        MinimumSeverity = minimumSeverity;

        Validate();
    }

    public bool Accepts(NodeOpsEvent nodeEvent)
    {
        if (!Enabled) return false;
        if (nodeEvent.IsHeartbeat) return Kind == NodeOpsSinkKind.StatusMonitor;
        if (Kind == NodeOpsSinkKind.StatusMonitor) return false;
        return ParseSeverity(nodeEvent.Severity) >= MinimumSeverity;
    }

    private static Uri? ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            throw new ArgumentException("Endpoint must be an absolute HTTP or HTTPS URI.", nameof(endpoint));
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Endpoint must use HTTP or HTTPS.", nameof(endpoint));
        return uri;
    }

    private static string GetDefaultMethod(NodeOpsProvider provider) =>
        provider is NodeOpsProvider.BetterStackHeartbeat or NodeOpsProvider.HealthchecksHeartbeat ? "GET" : "POST";

    private static NodeOpsSeverity ParseSeverity(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "fatal" => NodeOpsSeverity.Fatal,
            "error" => NodeOpsSeverity.Error,
            "warning" => NodeOpsSeverity.Warning,
            _ => NodeOpsSeverity.Info
        };

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Sink name cannot be empty.", nameof(Name));
        if (string.IsNullOrWhiteSpace(Method))
            throw new ArgumentException("Method cannot be empty.", nameof(Method));
        if (Enabled && Method != "GET" && Method != "POST" && Method != "PUT")
            throw new ArgumentException("Method must be GET, POST, or PUT.", nameof(Method));
        if (!string.IsNullOrEmpty(Token) && string.IsNullOrWhiteSpace(TokenHeader))
            throw new ArgumentException("TokenHeader cannot be empty when Token is configured.", nameof(TokenHeader));
    }
}
