// Copyright (C) 2015-2026 The Neo Project.
//
// NodeOpsEvent.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Neo.Plugins.NodeOps;

internal sealed record NodeOpsEvent(
    string EventId,
    string EventType,
    string Severity,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? Source,
    string Fingerprint,
    bool IsTerminating,
    DateTimeOffset Timestamp,
    string RuntimeVersion,
    string OSDescription,
    string ProcessArchitecture,
    int ProcessId,
    string ServiceName,
    string Environment,
    string? NodeName,
    uint? Network,
    string? NodeVersion,
    string? PluginVersion)
{
    public bool IsHeartbeat => EventType == "Heartbeat";

    public static NodeOpsEvent FromException(string eventType, Exception exception, bool isTerminating, NodeOpsSettings settings, NeoSystem? system)
    {
        var message = Truncate(exception.GetBaseException().Message, settings.MaxMessageLength);
        var stackTrace = settings.IncludeStackTrace
            ? Truncate(exception.ToString(), settings.MaxStackTraceLength)
            : null;

        return new NodeOpsEvent(
            EventId: Guid.NewGuid().ToString("N"),
            EventType: eventType,
            Severity: isTerminating ? "fatal" : "error",
            Message: message,
            ExceptionType: exception.GetBaseException().GetType().FullName,
            StackTrace: stackTrace,
            Source: exception.Source,
            Fingerprint: CreateFingerprint(exception),
            IsTerminating: isTerminating,
            Timestamp: DateTimeOffset.UtcNow,
            RuntimeVersion: RuntimeInformation.FrameworkDescription,
            OSDescription: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessId: System.Environment.ProcessId,
            ServiceName: settings.ServiceName,
            Environment: settings.Environment,
            NodeName: string.IsNullOrWhiteSpace(settings.NodeName) ? null : settings.NodeName,
            Network: system?.Settings.Network,
            NodeVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
            PluginVersion: typeof(NodeOpsPlugin).Assembly.GetName().Version?.ToString());
    }

    public static NodeOpsEvent FromObject(string eventType, object value, bool isTerminating, NodeOpsSettings settings, NeoSystem? system)
    {
        var message = Truncate(value.ToString() ?? value.GetType().FullName ?? "Unknown non-exception error", settings.MaxMessageLength);
        return new NodeOpsEvent(
            EventId: Guid.NewGuid().ToString("N"),
            EventType: eventType,
            Severity: isTerminating ? "fatal" : "error",
            Message: message,
            ExceptionType: value.GetType().FullName,
            StackTrace: null,
            Source: null,
            Fingerprint: CreateFingerprint(value.GetType().FullName ?? message),
            IsTerminating: isTerminating,
            Timestamp: DateTimeOffset.UtcNow,
            RuntimeVersion: RuntimeInformation.FrameworkDescription,
            OSDescription: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessId: System.Environment.ProcessId,
            ServiceName: settings.ServiceName,
            Environment: settings.Environment,
            NodeName: string.IsNullOrWhiteSpace(settings.NodeName) ? null : settings.NodeName,
            Network: system?.Settings.Network,
            NodeVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
            PluginVersion: typeof(NodeOpsPlugin).Assembly.GetName().Version?.ToString());
    }

    public static NodeOpsEvent Heartbeat(NodeOpsSettings settings, NeoSystem? system)
    {
        return new NodeOpsEvent(
            EventId: Guid.NewGuid().ToString("N"),
            EventType: "Heartbeat",
            Severity: "info",
            Message: "Node heartbeat",
            ExceptionType: null,
            StackTrace: null,
            Source: null,
            Fingerprint: CreateFingerprint($"Heartbeat:{settings.ServiceName}:{settings.Environment}:{settings.NodeName}"),
            IsTerminating: false,
            Timestamp: DateTimeOffset.UtcNow,
            RuntimeVersion: RuntimeInformation.FrameworkDescription,
            OSDescription: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessId: System.Environment.ProcessId,
            ServiceName: settings.ServiceName,
            Environment: settings.Environment,
            NodeName: string.IsNullOrWhiteSpace(settings.NodeName) ? null : settings.NodeName,
            Network: system?.Settings.Network,
            NodeVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
            PluginVersion: typeof(NodeOpsPlugin).Assembly.GetName().Version?.ToString());
    }

    private static string CreateFingerprint(Exception exception)
    {
        var baseException = exception.GetBaseException();
        var firstFrame = new StackTrace(baseException, false).GetFrames()?.FirstOrDefault();
        var method = firstFrame?.GetMethod();
        var signature = method is null
            ? baseException.StackTrace?.Split(System.Environment.NewLine).FirstOrDefault()
            : $"{method.DeclaringType?.FullName}.{method.Name}";
        return CreateFingerprint($"{baseException.GetType().FullName}:{signature}");
    }

    private static string CreateFingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return value[..maxLength];
    }
}
