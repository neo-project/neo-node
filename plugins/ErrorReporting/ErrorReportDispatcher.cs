// Copyright (C) 2015-2026 The Neo Project.
//
// ErrorReportDispatcher.cs file belongs to the neo project and is free
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

namespace Neo.Plugins.ErrorReporting;

internal sealed class ErrorReportDispatcher : IDisposable
{
    private readonly ErrorReportingSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly Channel<ErrorReport> _channel;
    private readonly CancellationTokenSource _shutdown = new();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private Task? _worker;

    public ErrorReportDispatcher(ErrorReportingSettings settings)
        : this(settings, new HttpClient(), disposeClient: true)
    {
    }

    internal ErrorReportDispatcher(ErrorReportingSettings settings, HttpClient httpClient, bool disposeClient = false)
    {
        _settings = settings;
        _httpClient = httpClient;
        _disposeClient = disposeClient;
        _channel = Channel.CreateBounded<ErrorReport>(new BoundedChannelOptions(settings.MaxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public void Start()
    {
        if (!_settings.Enabled || _worker is not null) return;
        _worker = Task.Run(ProcessQueueAsync);
    }

    public bool TryEnqueue(ErrorReport report)
    {
        if (!_settings.Enabled) return false;
        return _channel.Writer.TryWrite(report);
    }

    public bool SendNow(ErrorReport report)
    {
        if (!_settings.Enabled) return false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(_settings.FlushTimeoutMilliseconds));
        try
        {
            return SendBatchAsync([report], timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "ErrorReporting failed to flush fatal report");
            return false;
        }
    }

    internal async Task<bool> SendBatchAsync(IReadOnlyList<ErrorReport> reports, CancellationToken cancellationToken)
    {
        if (reports.Count == 0 || !_settings.Enabled) return false;

        var payload = new ErrorReportBatch(
            ServiceName: _settings.ServiceName,
            Environment: _settings.Environment,
            NodeName: string.IsNullOrWhiteSpace(_settings.NodeName) ? null : _settings.NodeName,
            SentAt: DateTimeOffset.UtcNow,
            Reports: reports);

        for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
                {
                    Content = JsonContent.Create(payload, options: SerializerOptions)
                };
                foreach (var header in _settings.Headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return true;

                Logs.RuntimeLogger.Warning(
                    "ErrorReporting upload failed with status {StatusCode}",
                    (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logs.RuntimeLogger.Warning(ex, "ErrorReporting upload attempt failed");
            }

            if (attempt < _settings.MaxRetries)
                await Task.Delay(_settings.RetryDelay, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<ErrorReport>(_settings.BatchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                while (batch.Count < _settings.BatchSize && _channel.Reader.TryRead(out var report))
                    batch.Add(report);

                if (batch.Count == 0) continue;

                await SendBatchAsync(batch, _shutdown.Token).ConfigureAwait(false);
                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logs.RuntimeLogger.Warning(ex, "ErrorReporting queue processing stopped");
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        _shutdown.Dispose();
        if (_disposeClient)
            _httpClient.Dispose();
    }

    private sealed record ErrorReportBatch(
        string ServiceName,
        string Environment,
        string? NodeName,
        DateTimeOffset SentAt,
        IReadOnlyList<ErrorReport> Reports);
}
