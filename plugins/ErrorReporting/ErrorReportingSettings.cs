// Copyright (C) 2015-2026 The Neo Project.
//
// ErrorReportingSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.ErrorReporting;

internal sealed class ErrorReportingSettings : IPluginSettings
{
    public Uri? Endpoint { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public string Environment { get; }
    public string ServiceName { get; }
    public string NodeName { get; }
    public bool CaptureUnhandledExceptions { get; }
    public bool CaptureUnobservedTaskExceptions { get; }
    public bool IncludeStackTrace { get; }
    public int MaxQueueSize { get; }
    public int BatchSize { get; }
    public int MaxRetries { get; }
    public int RetryDelayMilliseconds { get; }
    public int FlushTimeoutMilliseconds { get; }
    public int MaxMessageLength { get; }
    public int MaxStackTraceLength { get; }
    public UnhandledExceptionPolicy ExceptionPolicy { get; }

    public static ErrorReportingSettings Default { get; private set; } = new(new ConfigurationBuilder().Build().GetSection("PluginConfiguration"));

    public bool Enabled => Endpoint is not null && (CaptureUnhandledExceptions || CaptureUnobservedTaskExceptions);
    public TimeSpan RetryDelay => TimeSpan.FromMilliseconds(RetryDelayMilliseconds);

    private ErrorReportingSettings(IConfigurationSection section)
    {
        Endpoint = ParseEndpoint(section.GetValue("Endpoint", string.Empty));
        Headers = section.GetSection("Headers").GetChildren()
            .Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrEmpty(p.Value))
            .ToDictionary(p => p.Key, p => p.Value!, StringComparer.OrdinalIgnoreCase);
        Environment = section.GetValue("Environment", "production")!;
        ServiceName = section.GetValue("ServiceName", "neo-node")!;
        NodeName = section.GetValue("NodeName", string.Empty)!;
        CaptureUnhandledExceptions = section.GetValue("CaptureUnhandledExceptions", true);
        CaptureUnobservedTaskExceptions = section.GetValue("CaptureUnobservedTaskExceptions", true);
        IncludeStackTrace = section.GetValue("IncludeStackTrace", true);
        MaxQueueSize = section.GetValue("MaxQueueSize", 1024);
        BatchSize = section.GetValue("BatchSize", 10);
        MaxRetries = section.GetValue("MaxRetries", 3);
        RetryDelayMilliseconds = section.GetValue("RetryDelayMilliseconds", 1000);
        FlushTimeoutMilliseconds = section.GetValue("FlushTimeoutMilliseconds", 5000);
        MaxMessageLength = section.GetValue("MaxMessageLength", 4096);
        MaxStackTraceLength = section.GetValue("MaxStackTraceLength", 32768);
        ExceptionPolicy = section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.Ignore);

        Validate();
    }

    private ErrorReportingSettings(
        Uri? endpoint,
        IReadOnlyDictionary<string, string> headers,
        string environment,
        string serviceName,
        string nodeName,
        bool captureUnhandledExceptions,
        bool captureUnobservedTaskExceptions,
        bool includeStackTrace,
        int maxQueueSize,
        int batchSize,
        int maxRetries,
        int retryDelayMilliseconds,
        int flushTimeoutMilliseconds,
        int maxMessageLength,
        int maxStackTraceLength,
        UnhandledExceptionPolicy exceptionPolicy)
    {
        Endpoint = endpoint;
        Headers = headers;
        Environment = environment;
        ServiceName = serviceName;
        NodeName = nodeName;
        CaptureUnhandledExceptions = captureUnhandledExceptions;
        CaptureUnobservedTaskExceptions = captureUnobservedTaskExceptions;
        IncludeStackTrace = includeStackTrace;
        MaxQueueSize = maxQueueSize;
        BatchSize = batchSize;
        MaxRetries = maxRetries;
        RetryDelayMilliseconds = retryDelayMilliseconds;
        FlushTimeoutMilliseconds = flushTimeoutMilliseconds;
        MaxMessageLength = maxMessageLength;
        MaxStackTraceLength = maxStackTraceLength;
        ExceptionPolicy = exceptionPolicy;

        Validate();
    }

    public static void Load(IConfigurationSection section) =>
        Default = new ErrorReportingSettings(section);

    internal static ErrorReportingSettings Create(
        Uri? endpoint,
        IReadOnlyDictionary<string, string>? headers = null,
        string environment = "test",
        string serviceName = "neo-node",
        string nodeName = "",
        bool captureUnhandledExceptions = true,
        bool captureUnobservedTaskExceptions = true,
        bool includeStackTrace = true,
        int maxQueueSize = 1024,
        int batchSize = 10,
        int maxRetries = 0,
        int retryDelayMilliseconds = 1,
        int flushTimeoutMilliseconds = 1000,
        int maxMessageLength = 4096,
        int maxStackTraceLength = 32768,
        UnhandledExceptionPolicy exceptionPolicy = UnhandledExceptionPolicy.Ignore) =>
        new(
            endpoint,
            headers ?? new Dictionary<string, string>(),
            environment,
            serviceName,
            nodeName,
            captureUnhandledExceptions,
            captureUnobservedTaskExceptions,
            includeStackTrace,
            maxQueueSize,
            batchSize,
            maxRetries,
            retryDelayMilliseconds,
            flushTimeoutMilliseconds,
            maxMessageLength,
            maxStackTraceLength,
            exceptionPolicy);

    private static Uri? ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            throw new ArgumentException("Endpoint must be an absolute HTTP or HTTPS URI.", nameof(endpoint));
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Endpoint must use HTTP or HTTPS.", nameof(endpoint));
        return uri;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Environment))
            throw new ArgumentException("Environment cannot be empty.", nameof(Environment));
        if (string.IsNullOrWhiteSpace(ServiceName))
            throw new ArgumentException("ServiceName cannot be empty.", nameof(ServiceName));
        if (MaxQueueSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxQueueSize), MaxQueueSize, "MaxQueueSize must be greater than zero.");
        if (BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "BatchSize must be greater than zero.");
        if (BatchSize > MaxQueueSize)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "BatchSize cannot exceed MaxQueueSize.");
        if (MaxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRetries), MaxRetries, "MaxRetries cannot be negative.");
        if (RetryDelayMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(RetryDelayMilliseconds), RetryDelayMilliseconds, "RetryDelayMilliseconds cannot be negative.");
        if (FlushTimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(FlushTimeoutMilliseconds), FlushTimeoutMilliseconds, "FlushTimeoutMilliseconds must be greater than zero.");
        if (MaxMessageLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMessageLength), MaxMessageLength, "MaxMessageLength must be greater than zero.");
        if (MaxStackTraceLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxStackTraceLength), MaxStackTraceLength, "MaxStackTraceLength must be greater than zero.");
    }
}
