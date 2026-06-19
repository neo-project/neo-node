// Copyright (C) 2015-2026 The Neo Project.
//
// NodeOpsSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.NodeOps;

internal sealed class NodeOpsSettings : IPluginSettings
{
    public IReadOnlyList<NodeOpsSinkSettings> Sinks { get; }
    public string Environment { get; }
    public string ServiceName { get; }
    public string NodeName { get; }
    public bool CaptureUnhandledExceptions { get; }
    public bool CaptureUnobservedTaskExceptions { get; }
    public bool IncludeStackTrace { get; }
    public int HeartbeatIntervalSeconds { get; }
    public int MaxQueueSize { get; }
    public int BatchSize { get; }
    public int MaxRetries { get; }
    public int RetryDelayMilliseconds { get; }
    public int FlushTimeoutMilliseconds { get; }
    public int MaxMessageLength { get; }
    public int MaxStackTraceLength { get; }
    public UnhandledExceptionPolicy ExceptionPolicy { get; }

    public static NodeOpsSettings Default { get; private set; } = new(new ConfigurationBuilder().Build().GetSection("PluginConfiguration"));

    public bool Enabled => Sinks.Any(p => p.Enabled);
    public bool HasEventSinks => Sinks.Any(p => p.Enabled && p.Kind is NodeOpsSinkKind.ErrorCollector or NodeOpsSinkKind.Notification);
    public bool HasHeartbeatSinks => Sinks.Any(p => p.Enabled && p.Kind == NodeOpsSinkKind.StatusMonitor);
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);
    public TimeSpan RetryDelay => TimeSpan.FromMilliseconds(RetryDelayMilliseconds);

    private NodeOpsSettings(IConfigurationSection section)
    {
        Environment = section.GetValue("Environment", "production")!;
        ServiceName = section.GetValue("ServiceName", "neo-node")!;
        NodeName = section.GetValue("NodeName", string.Empty)!;
        CaptureUnhandledExceptions = section.GetValue("CaptureUnhandledExceptions", true);
        CaptureUnobservedTaskExceptions = section.GetValue("CaptureUnobservedTaskExceptions", true);
        IncludeStackTrace = section.GetValue("IncludeStackTrace", true);
        HeartbeatIntervalSeconds = section.GetValue("HeartbeatIntervalSeconds", 60);
        MaxQueueSize = section.GetValue("MaxQueueSize", 1024);
        BatchSize = section.GetValue("BatchSize", 10);
        MaxRetries = section.GetValue("MaxRetries", 3);
        RetryDelayMilliseconds = section.GetValue("RetryDelayMilliseconds", 1000);
        FlushTimeoutMilliseconds = section.GetValue("FlushTimeoutMilliseconds", 5000);
        MaxMessageLength = section.GetValue("MaxMessageLength", 4096);
        MaxStackTraceLength = section.GetValue("MaxStackTraceLength", 32768);
        ExceptionPolicy = section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.Ignore);
        Sinks = section.GetSection("Sinks").GetChildren().Select(p => new NodeOpsSinkSettings(p)).ToArray();

        Validate();
    }

    private NodeOpsSettings(
        IReadOnlyList<NodeOpsSinkSettings> sinks,
        string environment,
        string serviceName,
        string nodeName,
        bool captureUnhandledExceptions,
        bool captureUnobservedTaskExceptions,
        bool includeStackTrace,
        int heartbeatIntervalSeconds,
        int maxQueueSize,
        int batchSize,
        int maxRetries,
        int retryDelayMilliseconds,
        int flushTimeoutMilliseconds,
        int maxMessageLength,
        int maxStackTraceLength,
        UnhandledExceptionPolicy exceptionPolicy)
    {
        Sinks = sinks;
        Environment = environment;
        ServiceName = serviceName;
        NodeName = nodeName;
        CaptureUnhandledExceptions = captureUnhandledExceptions;
        CaptureUnobservedTaskExceptions = captureUnobservedTaskExceptions;
        IncludeStackTrace = includeStackTrace;
        HeartbeatIntervalSeconds = heartbeatIntervalSeconds;
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
        Default = new NodeOpsSettings(section);

    internal static NodeOpsSettings Create(
        IReadOnlyList<NodeOpsSinkSettings>? sinks = null,
        string environment = "test",
        string serviceName = "neo-node",
        string nodeName = "",
        bool captureUnhandledExceptions = true,
        bool captureUnobservedTaskExceptions = true,
        bool includeStackTrace = true,
        int heartbeatIntervalSeconds = 60,
        int maxQueueSize = 1024,
        int batchSize = 10,
        int maxRetries = 0,
        int retryDelayMilliseconds = 1,
        int flushTimeoutMilliseconds = 1000,
        int maxMessageLength = 4096,
        int maxStackTraceLength = 32768,
        UnhandledExceptionPolicy exceptionPolicy = UnhandledExceptionPolicy.Ignore) =>
        new(
            sinks ?? Array.Empty<NodeOpsSinkSettings>(),
            environment,
            serviceName,
            nodeName,
            captureUnhandledExceptions,
            captureUnobservedTaskExceptions,
            includeStackTrace,
            heartbeatIntervalSeconds,
            maxQueueSize,
            batchSize,
            maxRetries,
            retryDelayMilliseconds,
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
