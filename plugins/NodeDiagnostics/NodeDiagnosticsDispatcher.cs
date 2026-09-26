// Copyright (C) 2015-2026 The Neo Project.
//
// NodeDiagnosticsDispatcher.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Native;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

namespace Neo.Plugins.NodeDiagnostics;

internal sealed class NodeDiagnosticsDispatcher : IDisposable
{
    private readonly NodeDiagnosticsSettings _settings;
    private readonly Func<NeoSystem?> _systemProvider;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly Channel<NodeDiagnosticsEvent> _channel;
    private readonly CancellationTokenSource _shutdown = new();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ConsensusStallCheckInterval = TimeSpan.FromSeconds(1);
    private static readonly string UserAgent = $"Neo.NodeDiagnostics/{typeof(NodeDiagnosticsPlugin).Assembly.GetName().Version?.ToString() ?? "unknown"}";
    private Task? _eventWorker;
    private Task? _heartbeatWorker;
    private Task? _consensusStallWorker;
    private long _droppedEvents;
    private readonly Lock _dropWarningLock = new();
    private DateTimeOffset _lastDropWarning = DateTimeOffset.MinValue;
    private readonly Lock _livenessLock = new();
    private uint? _lastObservedBlockHeight;
    private DateTimeOffset? _lastBlockAdvanceAt;
    private uint? _lastReportedConsensusStallHeight;

    public NodeDiagnosticsDispatcher(NodeDiagnosticsSettings settings, Func<NeoSystem?> systemProvider)
        : this(settings, systemProvider, new HttpClient(), disposeClient: true) { }

    internal NodeDiagnosticsDispatcher(NodeDiagnosticsSettings settings, HttpClient httpClient, bool disposeClient = false)
        : this(settings, () => null, httpClient, disposeClient) { }

    private NodeDiagnosticsDispatcher(NodeDiagnosticsSettings settings, Func<NeoSystem?> systemProvider, HttpClient httpClient, bool disposeClient)
    {
        _settings = settings;
        _systemProvider = systemProvider;
        _httpClient = httpClient;
        _disposeClient = disposeClient;
        _channel = Channel.CreateBounded<NodeDiagnosticsEvent>(new BoundedChannelOptions(settings.MaxQueueSize)
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
        if (_settings.HasEventSinks && _consensusStallWorker is null)
            _consensusStallWorker = Task.Run(ProcessConsensusStallAsync);
    }

    public bool TryEnqueue(NodeDiagnosticsEvent nodeEvent)
    {
        if (!_settings.Enabled || !_settings.HasEventSinks) return false;
        if (_channel.Writer.TryWrite(nodeEvent)) return true;

        var dropped = Interlocked.Increment(ref _droppedEvents);
        lock (_dropWarningLock)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastDropWarning > TimeSpan.FromMinutes(1))
            {
                _lastDropWarning = now;
                Logs.RuntimeLogger.Warning(
                    "NodeDiagnostics event queue is full. Dropped {DroppedEvents} events. Increase MaxQueueSize or reduce sink latency.",
                    dropped);
            }
        }
        return false;
    }

    public bool SendNow(NodeDiagnosticsEvent nodeEvent)
    {
        if (!_settings.Enabled) return false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(_settings.FlushTimeoutMilliseconds));
        try
        {
            // Fatal exception handling needs a bounded synchronous flush before the process exits.
            return SendEventsAsync([nodeEvent], timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeDiagnostics failed to flush event");
            return false;
        }
    }

    internal Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var system = _systemProvider();
        return SendEventsAsync([NodeDiagnosticsEvent.Heartbeat(_settings, system, GetNodeState(system, DateTimeOffset.UtcNow))], cancellationToken);
    }

    internal void RecordBlockPersisted(uint blockIndex) =>
        RecordBlockPersisted(blockIndex, DateTimeOffset.UtcNow);

    internal void RecordBlockPersisted(uint blockIndex, DateTimeOffset timestamp) =>
        ObserveBlockHeight(blockIndex, timestamp);

    internal Task<bool> SendConsensusStallIfNeededAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var system = _systemProvider();
        var nodeState = GetNodeState(system, now);
        if (!ShouldReportConsensusStall(nodeState)) return Task.FromResult(false);
        return SendEventsAsync([NodeDiagnosticsEvent.ConsensusStall(_settings, system, nodeState)], cancellationToken);
    }

    internal async Task<bool> SendEventsAsync(IReadOnlyList<NodeDiagnosticsEvent> events, CancellationToken cancellationToken)
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

    private async Task<bool> SendToSinkAsync(NodeDiagnosticsSinkSettings sink, IReadOnlyList<NodeDiagnosticsEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0) return false;

        if (sink.Provider is NodeDiagnosticsProvider.Sentry or NodeDiagnosticsProvider.GoogleCloudErrorReporting)
        {
            var allSucceeded = true;
            foreach (var nodeEvent in events)
                allSucceeded &= await SendPayloadWithRetryAsync(sink, CreateProviderPayload(sink.Provider, nodeEvent), cancellationToken).ConfigureAwait(false);
            return allSucceeded;
        }

        if (sink.Provider is NodeDiagnosticsProvider.BetterStackHeartbeat or NodeDiagnosticsProvider.HealthchecksHeartbeat)
            return await SendPayloadWithRetryAsync(sink, null, cancellationToken).ConfigureAwait(false);

        return await SendPayloadWithRetryAsync(
            sink,
            new NodeDiagnosticsBatchPayload(
                ServiceName: _settings.ServiceName,
                Environment: _settings.Environment,
                NodeName: string.IsNullOrWhiteSpace(_settings.NodeName) ? null : _settings.NodeName,
                SentAt: DateTimeOffset.UtcNow,
                Events: events),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> SendPayloadWithRetryAsync(NodeDiagnosticsSinkSettings sink, object? payload, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(_settings.RequestTimeoutMilliseconds));
                using var request = new HttpRequestMessage(new HttpMethod(sink.Method), sink.Endpoint);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                if (payload is not null && sink.Method != "GET")
                    request.Content = JsonContent.Create(payload, options: SerializerOptions);

                foreach (var header in sink.Headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                AddToken(request, sink);

                using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return true;

                Logs.RuntimeLogger.Warning(
                    "NodeDiagnostics sink {SinkName} failed with status {StatusCode}",
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
                    "NodeDiagnostics sink {SinkName} timed out after {TimeoutMilliseconds} ms",
                    sink.Name,
                    _settings.RequestTimeoutMilliseconds);
            }
            catch (Exception ex)
            {
                Logs.RuntimeLogger.Warning(ex, "NodeDiagnostics sink {SinkName} upload attempt failed", sink.Name);
            }

            if (attempt < _settings.MaxRetries)
                await Task.Delay(_settings.RetryDelay, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static void AddToken(HttpRequestMessage request, NodeDiagnosticsSinkSettings sink)
    {
        if (string.IsNullOrEmpty(sink.Token)) return;
        var value = string.IsNullOrWhiteSpace(sink.TokenScheme)
            ? sink.Token
            : $"{sink.TokenScheme} {sink.Token}";
        request.Headers.TryAddWithoutValidation(sink.TokenHeader, value);
    }

    private static object CreateProviderPayload(NodeDiagnosticsProvider provider, NodeDiagnosticsEvent nodeEvent) =>
        provider switch
        {
            NodeDiagnosticsProvider.Sentry => new
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
            NodeDiagnosticsProvider.GoogleCloudErrorReporting => new
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

    private static Dictionary<string, string> CreateTags(NodeDiagnosticsEvent nodeEvent)
    {
        var tags = new Dictionary<string, string>();
        foreach (var tag in nodeEvent.Tags)
            if (!string.IsNullOrWhiteSpace(tag.Key) && !string.IsNullOrWhiteSpace(tag.Value))
                tags[tag.Key] = tag.Value;
        tags["event_type"] = nodeEvent.EventType;
        tags["fingerprint"] = nodeEvent.Fingerprint;
        if (nodeEvent.Network is not null)
            tags["network"] = nodeEvent.Network.Value.ToString("X8");
        if (nodeEvent.BlockHeight is not null)
            tags["block_height"] = nodeEvent.BlockHeight.Value.ToString();
        if (nodeEvent.HeaderHeight is not null)
            tags["header_height"] = nodeEvent.HeaderHeight.Value.ToString();
        if (nodeEvent.SecondsSinceLastBlockAdvance is not null)
            tags["seconds_since_last_block_advance"] = nodeEvent.SecondsSinceLastBlockAdvance.Value.ToString();
        return tags;
    }

    private NodeDiagnosticsNodeState GetNodeState(NeoSystem? system, DateTimeOffset now)
    {
        uint? blockHeight = null;
        uint? headerHeight = null;

        if (system is not null)
        {
            try
            {
                blockHeight = NativeContract.Ledger.CurrentIndex(system.StoreView);
                headerHeight = system.HeaderCache.Last?.Index ?? blockHeight;
                ObserveBlockHeight(blockHeight.Value, now);
            }
            catch (Exception ex)
            {
                Logs.RuntimeLogger.Debug(ex, "NodeDiagnostics failed to read node liveness state");
            }
        }

        uint? lastObservedBlockHeight;
        DateTimeOffset? lastBlockAdvanceAt;
        lock (_livenessLock)
        {
            lastObservedBlockHeight = _lastObservedBlockHeight;
            lastBlockAdvanceAt = _lastBlockAdvanceAt;
        }

        int? secondsSinceLastBlockAdvance = lastBlockAdvanceAt is null
            ? null
            : Math.Max(0, (int)(now - lastBlockAdvanceAt.Value).TotalSeconds);

        return new NodeDiagnosticsNodeState(
            blockHeight ?? lastObservedBlockHeight,
            headerHeight,
            secondsSinceLastBlockAdvance);
    }

    private void ObserveBlockHeight(uint blockHeight, DateTimeOffset timestamp)
    {
        lock (_livenessLock)
        {
            if (_lastObservedBlockHeight == blockHeight) return;
            _lastObservedBlockHeight = blockHeight;
            _lastBlockAdvanceAt = timestamp;
            _lastReportedConsensusStallHeight = null;
        }
    }

    private bool ShouldReportConsensusStall(NodeDiagnosticsNodeState nodeState)
    {
        if (nodeState.BlockHeight is null || nodeState.SecondsSinceLastBlockAdvance is null)
            return false;
        if (nodeState.SecondsSinceLastBlockAdvance.Value < _settings.ConsensusStallThresholdSeconds)
            return false;

        lock (_livenessLock)
        {
            if (_lastReportedConsensusStallHeight == nodeState.BlockHeight)
                return false;
            _lastReportedConsensusStallHeight = nodeState.BlockHeight;
            return true;
        }
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<NodeDiagnosticsEvent>(_settings.BatchSize);

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
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Expected during plugin shutdown.
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeDiagnostics queue processing stopped");
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
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Expected during plugin shutdown.
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeDiagnostics heartbeat processing stopped");
        }
    }

    private async Task ProcessConsensusStallAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await SendConsensusStallIfNeededAsync(DateTimeOffset.UtcNow, _shutdown.Token).ConfigureAwait(false);
                await Task.Delay(ConsensusStallCheckInterval, _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Expected during plugin shutdown.
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeDiagnostics consensus stall processing stopped");
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
        WaitForWorkers();
        _shutdown.Dispose();
        if (_disposeClient)
            _httpClient.Dispose();
    }

    private void WaitForWorkers()
    {
        var workers = new[] { _eventWorker, _heartbeatWorker, _consensusStallWorker }
            .Where(static worker => worker is not null)
            .Cast<Task>()
            .ToArray();
        if (workers.Length == 0) return;

        var timeout = TimeSpan.FromMilliseconds(Math.Max(2000, _settings.RequestTimeoutMilliseconds + 500));
        try
        {
            if (!Task.WaitAll(workers, timeout))
                Logs.RuntimeLogger.Warning(
                    "NodeDiagnostics worker shutdown timed out after {TimeoutMilliseconds} ms",
                    (int)timeout.TotalMilliseconds);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static p => p is OperationCanceledException))
        {
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "NodeDiagnostics worker shutdown failed");
        }
    }

    private sealed record NodeDiagnosticsBatchPayload(
        string ServiceName,
        string Environment,
        string? NodeName,
        DateTimeOffset SentAt,
        IReadOnlyList<NodeDiagnosticsEvent> Events);
}
