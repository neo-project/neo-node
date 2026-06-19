// Copyright (C) 2015-2026 The Neo Project.
//
// NodeOpsDispatcher.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

namespace Neo.Plugins.NodeOps;

internal sealed class NodeOpsDispatcher : IDisposable
{
    private readonly NodeOpsSettings _settings;
    private readonly Func<NeoSystem?> _systemProvider;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly Channel<NodeOpsEvent> _channel;
    private readonly CancellationTokenSource _shutdown = new();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private Task? _eventWorker;
    private Task? _heartbeatWorker;
    private long _droppedEvents;
    private DateTimeOffset _lastDropWarning = DateTimeOffset.MinValue;

    public NodeOpsDispatcher(NodeOpsSettings settings, Func<NeoSystem?> systemProvider)
        : this(settings, systemProvider, new HttpClient(), disposeClient: true)
    {
    }

    internal NodeOpsDispatcher(NodeOpsSettings settings, HttpClient httpClient, bool disposeClient = false)
        : this(settings, () => null, httpClient, disposeClient)
    {
    }

    private NodeOpsDispatcher(NodeOpsSettings settings, Func<NeoSystem?> systemProvider, HttpClient httpClient, bool disposeClient)
    {
        _settings = settings;
        _systemProvider = systemProvider;
        _httpClient = httpClient;
        _disposeClient = disposeClient;
        _channel = Channel.CreateBounded<NodeOpsEvent>(new BoundedChannelOptions(settings.MaxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public void Start()
    {
        if (!_settings.Enabled) return;
        if (_settings.HasEventSinks && _eventWorker is null)
            _eventWorker = Task.Run(ProcessQueueAsync);
        if (_settings.HasHeartbeatSinks && _heartbeatWorker is null)
            _heartbeatWorker = Task.Run(ProcessHeartbeatAsync);
    }

    public bool TryEnqueue(NodeOpsEvent nodeEvent)
    {
        if (!_settings.Enabled || !_settings.HasEventSinks) return false;
        if (_channel.Writer.TryWrite(nodeEvent)) return true;

        var dropped = Interlocked.Increment(ref _droppedEvents);
        var now = DateTimeOffset.UtcNow;
        if (now - _lastDropWarning > TimeSpan.FromMinutes(1))
        {
            _lastDropWarning = now;
            Logs.RuntimeLogger.Warning(
                "NodeOps event queue is full. Dropped {DroppedEvents} events. Increase MaxQueueSize or reduce sink latency.",
                dropped);
        }
        return false;
    }

    public bool SendNow(NodeOpsEvent nodeEvent)
    {
        if (!_settings.Enabled) return false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(_settings.FlushTimeoutMilliseconds));
        try
        {
            return SendEventsAsync([nodeEvent], timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeOps failed to flush event");
            return false;
        }
    }

    internal Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken) =>
        SendEventsAsync([NodeOpsEvent.Heartbeat(_settings, _systemProvider())], cancellationToken);

    internal async Task<bool> SendEventsAsync(IReadOnlyList<NodeOpsEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0 || !_settings.Enabled) return false;

        var sinks = _settings.Sinks
            .Where(sink => sink.Enabled && events.Any(sink.Accepts))
            .ToArray();
        if (sinks.Length == 0) return false;

        var allSucceeded = true;
        foreach (var sink in sinks)
        {
            var selected = events.Where(sink.Accepts).ToArray();
            var sent = await SendToSinkAsync(sink, selected, cancellationToken).ConfigureAwait(false);
            allSucceeded &= sent;
        }

        return allSucceeded;
    }

    private async Task<bool> SendToSinkAsync(NodeOpsSinkSettings sink, IReadOnlyList<NodeOpsEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0) return false;

        if (sink.Provider is NodeOpsProvider.Sentry or NodeOpsProvider.GoogleCloudErrorReporting)
        {
            var allSucceeded = true;
            foreach (var nodeEvent in events)
                allSucceeded &= await SendPayloadWithRetryAsync(sink, CreateProviderPayload(sink.Provider, nodeEvent), cancellationToken).ConfigureAwait(false);
            return allSucceeded;
        }

        if (sink.Provider is NodeOpsProvider.BetterStackHeartbeat or NodeOpsProvider.HealthchecksHeartbeat)
            return await SendPayloadWithRetryAsync(sink, null, cancellationToken).ConfigureAwait(false);

        return await SendPayloadWithRetryAsync(
            sink,
            new NodeOpsBatchPayload(
                ServiceName: _settings.ServiceName,
                Environment: _settings.Environment,
                NodeName: string.IsNullOrWhiteSpace(_settings.NodeName) ? null : _settings.NodeName,
                SentAt: DateTimeOffset.UtcNow,
                Events: events),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> SendPayloadWithRetryAsync(NodeOpsSinkSettings sink, object? payload, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(_settings.RequestTimeoutMilliseconds));
                using var request = new HttpRequestMessage(new HttpMethod(sink.Method), sink.Endpoint);
                if (payload is not null && sink.Method != "GET")
                    request.Content = JsonContent.Create(payload, options: SerializerOptions);

                foreach (var header in sink.Headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                AddToken(request, sink);

                using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return true;

                Logs.RuntimeLogger.Warning(
                    "NodeOps sink {SinkName} failed with status {StatusCode}",
                    sink.Name,
                    (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                Logs.RuntimeLogger.Warning(
                    "NodeOps sink {SinkName} timed out after {TimeoutMilliseconds} ms",
                    sink.Name,
                    _settings.RequestTimeoutMilliseconds);
            }
            catch (Exception ex)
            {
                Logs.RuntimeLogger.Warning(ex, "NodeOps sink {SinkName} upload attempt failed", sink.Name);
            }

            if (attempt < _settings.MaxRetries)
                await Task.Delay(_settings.RetryDelay, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static void AddToken(HttpRequestMessage request, NodeOpsSinkSettings sink)
    {
        if (string.IsNullOrEmpty(sink.Token)) return;
        var value = string.IsNullOrWhiteSpace(sink.TokenScheme)
            ? sink.Token
            : $"{sink.TokenScheme} {sink.Token}";
        request.Headers.TryAddWithoutValidation(sink.TokenHeader, value);
    }

    private static object CreateProviderPayload(NodeOpsProvider provider, NodeOpsEvent nodeEvent) =>
        provider switch
        {
            NodeOpsProvider.Sentry => new
            {
                event_id = nodeEvent.EventId,
                timestamp = nodeEvent.Timestamp,
                platform = "csharp",
                level = nodeEvent.Severity,
                logger = "neo-node",
                server_name = nodeEvent.NodeName,
                release = nodeEvent.NodeVersion,
                environment = nodeEvent.Environment,
                message = nodeEvent.Message,
                exception = nodeEvent.ExceptionType is null
                    ? null
                    : new
                    {
                        values = new[]
                        {
                            new
                            {
                                type = nodeEvent.ExceptionType,
                                value = nodeEvent.Message,
                                stacktrace = nodeEvent.StackTrace
                            }
                        }
                    },
                tags = CreateTags(nodeEvent)
            },
            NodeOpsProvider.GoogleCloudErrorReporting => new
            {
                serviceContext = new
                {
                    service = nodeEvent.ServiceName,
                    version = nodeEvent.NodeVersion ?? nodeEvent.PluginVersion
                },
                message = nodeEvent.StackTrace ?? nodeEvent.Message,
                context = new
                {
                    reportLocation = new
                    {
                        filePath = nodeEvent.Source ?? "neo-node",
                        functionName = nodeEvent.EventType,
                        lineNumber = 0
                    }
                }
            },
            _ => nodeEvent
        };

    private static Dictionary<string, string> CreateTags(NodeOpsEvent nodeEvent)
    {
        var tags = new Dictionary<string, string>
        {
            ["event_type"] = nodeEvent.EventType,
            ["fingerprint"] = nodeEvent.Fingerprint
        };
        if (nodeEvent.Network is not null)
            tags["network"] = nodeEvent.Network.Value.ToString("X8");
        return tags;
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<NodeOpsEvent>(_settings.BatchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                while (batch.Count < _settings.BatchSize && _channel.Reader.TryRead(out var nodeEvent))
                    batch.Add(nodeEvent);

                if (batch.Count == 0) continue;

                await SendEventsAsync(batch, _shutdown.Token).ConfigureAwait(false);
                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeOps queue processing stopped");
        }
    }

    private async Task ProcessHeartbeatAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await SendHeartbeatAsync(_shutdown.Token).ConfigureAwait(false);
                await Task.Delay(_settings.HeartbeatInterval, _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeOps heartbeat processing stopped");
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            _eventWorker?.Wait(TimeSpan.FromSeconds(2));
            _heartbeatWorker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        _shutdown.Dispose();
        if (_disposeClient)
            _httpClient.Dispose();
    }

    private sealed record NodeOpsBatchPayload(
        string ServiceName,
        string Environment,
        string? NodeName,
        DateTimeOffset SentAt,
        IReadOnlyList<NodeOpsEvent> Events);
}
