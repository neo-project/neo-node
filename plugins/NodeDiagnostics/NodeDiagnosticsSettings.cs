// Copyright (C) 2015-2026 The Neo Project.
//
// NodeDiagnosticsSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.NodeDiagnostics;

internal sealed class NodeDiagnosticsSettings : IPluginSettings
{
    public IReadOnlyList<NodeDiagnosticsSinkSettings> Sinks { get; }
    public string Environment { get; }
    public string ServiceName { get; }
    public string NodeName { get; }
    public IReadOnlyDictionary<string, string> Tags { get; }
    public bool CaptureUnhandledExceptions { get; }
    public bool CaptureUnobservedTaskExceptions { get; }
    public bool CaptureApplicationFaults { get; }
    public bool SendStartupDiagnosticEvent { get; }
    public bool IncludeStackTrace { get; }
    public int HeartbeatIntervalSeconds { get; }
    public int MaxApplicationFaultsPerBlock { get; }
    public int MaxQueueSize { get; }
    public int BatchSize { get; }
    public int MaxRetries { get; }
    public int RetryDelayMilliseconds { get; }
    public int RequestTimeoutMilliseconds { get; }
    public int FlushTimeoutMilliseconds { get; }
    public int MaxMessageLength { get; }
    public int MaxStackTraceLength { get; }
    public UnhandledExceptionPolicy ExceptionPolicy { get; }

    public static NodeDiagnosticsSettings Default { get; private set; } = new(new ConfigurationBuilder().Build().GetSection("PluginConfiguration"));

    public bool Enabled => Sinks.Any(p => p.Enabled);
    public bool HasEventSinks => Sinks.Any(p => p.Enabled && p.Kind is NodeDiagnosticsSinkKind.ErrorCollector or NodeDiagnosticsSinkKind.Notification);
    public bool HasHeartbeatSinks => Sinks.Any(p => p.Enabled && p.Kind == NodeDiagnosticsSinkKind.StatusMonitor);
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);
    public TimeSpan RetryDelay => TimeSpan.FromMilliseconds(RetryDelayMilliseconds);

    private NodeDiagnosticsSettings(IConfigurationSection section)
    {
        Environment = section.GetValue("Environment", "production")!;
        ServiceName = section.GetValue("ServiceName", "neo-node")!;
        NodeName = section.GetValue("NodeName", string.Empty)!;
        Tags = LoadTags(section.GetSection("Tags"));
        CaptureUnhandledExceptions = section.GetValue("CaptureUnhandledExceptions", true);
        CaptureUnobservedTaskExceptions = section.GetValue("CaptureUnobservedTaskExceptions", true);
        CaptureApplicationFaults = section.GetValue("CaptureApplicationFaults", false);
        SendStartupDiagnosticEvent = section.GetValue("SendStartupDiagnosticEvent", false);
        IncludeStackTrace = section.GetValue("IncludeStackTrace", true);
        HeartbeatIntervalSeconds = section.GetValue("HeartbeatIntervalSeconds", 60);
        MaxApplicationFaultsPerBlock = section.GetValue("MaxApplicationFaultsPerBlock", 32);
        MaxQueueSize = section.GetValue("MaxQueueSize", 1024);
        BatchSize = section.GetValue("BatchSize", 10);
        MaxRetries = section.GetValue("MaxRetries", 3);
        RetryDelayMilliseconds = section.GetValue("RetryDelayMilliseconds", 1000);
        RequestTimeoutMilliseconds = section.GetValue("RequestTimeoutMilliseconds", 5000);
        FlushTimeoutMilliseconds = section.GetValue("FlushTimeoutMilliseconds", 5000);
        MaxMessageLength = section.GetValue("MaxMessageLength", 4096);
        MaxStackTraceLength = section.GetValue("MaxStackTraceLength", 32768);
        ExceptionPolicy = section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.Ignore);
        Sinks = section.GetSection("Sinks").GetChildren().Select(p => new NodeDiagnosticsSinkSettings(p)).ToArray();

        Validate();
    }

    private NodeDiagnosticsSettings(
        IReadOnlyList<NodeDiagnosticsSinkSettings> sinks,
        string environment,
        string serviceName,
        string nodeName,
        IReadOnlyDictionary<string, string> tags,
        bool captureUnhandledExceptions,
        bool captureUnobservedTaskExceptions,
        bool captureApplicationFaults,
        bool sendStartupDiagnosticEvent,
        bool includeStackTrace,
        int heartbeatIntervalSeconds,
        int maxApplicationFaultsPerBlock,
        int maxQueueSize,
        int batchSize,
        int maxRetries,
        int retryDelayMilliseconds,
        int requestTimeoutMilliseconds,
        int flushTimeoutMilliseconds,
        int maxMessageLength,
        int maxStackTraceLength,
        UnhandledExceptionPolicy exceptionPolicy)
    {
        Sinks = sinks;
        Environment = environment;
        ServiceName = serviceName;
        NodeName = nodeName;
        Tags = tags;
        CaptureUnhandledExceptions = captureUnhandledExceptions;
        CaptureUnobservedTaskExceptions = captureUnobservedTaskExceptions;
        CaptureApplicationFaults = captureApplicationFaults;
        SendStartupDiagnosticEvent = sendStartupDiagnosticEvent;
        IncludeStackTrace = includeStackTrace;
        HeartbeatIntervalSeconds = heartbeatIntervalSeconds;
        MaxApplicationFaultsPerBlock = maxApplicationFaultsPerBlock;
        MaxQueueSize = maxQueueSize;
        BatchSize = batchSize;
        MaxRetries = maxRetries;
        RetryDelayMilliseconds = retryDelayMilliseconds;
        RequestTimeoutMilliseconds = requestTimeoutMilliseconds;
        FlushTimeoutMilliseconds = flushTimeoutMilliseconds;
        MaxMessageLength = maxMessageLength;
        MaxStackTraceLength = maxStackTraceLength;
        ExceptionPolicy = exceptionPolicy;

        Validate();
    }

    public static void Load(IConfigurationSection section) =>
        Default = new NodeDiagnosticsSettings(section);

    internal static NodeDiagnosticsSettings Create(
        IReadOnlyList<NodeDiagnosticsSinkSettings>? sinks = null,
        string environment = "test",
        string serviceName = "neo-node",
        string nodeName = "",
        IReadOnlyDictionary<string, string>? tags = null,
        bool captureUnhandledExceptions = true,
        bool captureUnobservedTaskExceptions = true,
        bool captureApplicationFaults = false,
        bool sendStartupDiagnosticEvent = false,
        bool includeStackTrace = true,
        int heartbeatIntervalSeconds = 60,
        int maxApplicationFaultsPerBlock = 32,
        int maxQueueSize = 1024,
        int batchSize = 10,
        int maxRetries = 0,
        int retryDelayMilliseconds = 1,
        int requestTimeoutMilliseconds = 1000,
        int flushTimeoutMilliseconds = 1000,
        int maxMessageLength = 4096,
        int maxStackTraceLength = 32768,
        UnhandledExceptionPolicy exceptionPolicy = UnhandledExceptionPolicy.Ignore) =>
        new(
            sinks ?? Array.Empty<NodeDiagnosticsSinkSettings>(),
            environment,
            serviceName,
            nodeName,
            tags ?? new Dictionary<string, string>(),
            captureUnhandledExceptions,
            captureUnobservedTaskExceptions,
            captureApplicationFaults,
            sendStartupDiagnosticEvent,
            includeStackTrace,
            heartbeatIntervalSeconds,
            maxApplicationFaultsPerBlock,
            maxQueueSize,
            batchSize,
            maxRetries,
            retryDelayMilliseconds,
            requestTimeoutMilliseconds,
            flushTimeoutMilliseconds,
            maxMessageLength,
            maxStackTraceLength,
            exceptionPolicy);

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Environment))
            throw new ArgumentException("Environment cannot be empty.", nameof(Environment));
        if (string.IsNullOrWhiteSpace(ServiceName))
            throw new ArgumentException("ServiceName cannot be empty.", nameof(ServiceName));
        if (HeartbeatIntervalSeconds < 30)
            throw new ArgumentOutOfRangeException(nameof(HeartbeatIntervalSeconds), HeartbeatIntervalSeconds, "HeartbeatIntervalSeconds must be at least 30.");
        if (MaxApplicationFaultsPerBlock <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxApplicationFaultsPerBlock), MaxApplicationFaultsPerBlock, "MaxApplicationFaultsPerBlock must be greater than zero.");
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
        if (RequestTimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(RequestTimeoutMilliseconds), RequestTimeoutMilliseconds, "RequestTimeoutMilliseconds must be greater than zero.");
        if (FlushTimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(FlushTimeoutMilliseconds), FlushTimeoutMilliseconds, "FlushTimeoutMilliseconds must be greater than zero.");
        if (MaxMessageLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMessageLength), MaxMessageLength, "MaxMessageLength must be greater than zero.");
        if (MaxStackTraceLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxStackTraceLength), MaxStackTraceLength, "MaxStackTraceLength must be greater than zero.");
    }

    private static IReadOnlyDictionary<string, string> LoadTags(IConfigurationSection section) =>
        section.GetChildren()
            .Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
            .ToDictionary(p => p.Key, p => p.Value!, StringComparer.OrdinalIgnoreCase);
}
