# NodeOps

NodeOps provides lightweight node operations telemetry for Neo nodes. It can send crash and runtime exception events to error analysis services, publish status heartbeats to uptime monitors, and notify operator-owned webhooks when the node reports an exception.

The plugin is disabled until at least one sink has a non-empty `Endpoint`.

## Supported sinks

Each item in `Sinks` has a `Kind` and a `Provider`.

- `ErrorCollector`: receives exception and crash events.
- `StatusMonitor`: receives periodic heartbeats while the node process is running.
- `Notification`: receives exception events intended for alerting workflows.

Available providers:

- `CustomWebhook`: generic JSON webhook for internal gateways, alert routers, or vendor-specific wrappers.
- `Sentry`: sends Sentry-shaped event payloads to a configured Sentry ingestion URL.
- `GoogleCloudErrorReporting`: sends Google Cloud Error Reporting `events.report` shaped payloads.
- `BetterStackHeartbeat`: sends periodic requests to a Better Stack heartbeat URL.
- `HealthchecksHeartbeat`: sends periodic requests to a Healthchecks-compatible ping URL.

## Configuration

Configure the plugin in `NodeOps.json`.

```json
{
  "PluginConfiguration": {
    "Environment": "production",
    "ServiceName": "neo-node",
    "NodeName": "seed-1",
    "HeartbeatIntervalSeconds": 60,
    "RequestTimeoutMilliseconds": 5000,
    "MaxRetries": 3,
    "Sinks": [
      {
        "Name": "internal-errors",
        "Kind": "ErrorCollector",
        "Provider": "CustomWebhook",
        "Endpoint": "https://ops.example.com/neo/errors",
        "Token": "replace-with-secret",
        "TokenHeader": "Authorization",
        "TokenScheme": "Bearer",
        "MinimumSeverity": "Error"
      },
      {
        "Name": "betterstack",
        "Kind": "StatusMonitor",
        "Provider": "BetterStackHeartbeat",
        "Endpoint": "https://uptime.betterstack.com/api/v1/heartbeat/replace-with-id"
      }
    ]
  }
}
```

Do not commit production tokens to source control. Prefer injecting the final plugin configuration from deployment tooling or a secret manager.

## Reliability behavior

- Exception events are queued in memory and sent in batches where the provider supports batching.
- Fatal unhandled exceptions are flushed immediately with `FlushTimeoutMilliseconds`.
- Each HTTP request is bounded by `RequestTimeoutMilliseconds`.
- Failed requests are retried up to `MaxRetries` with `RetryDelayMilliseconds` between attempts.
- If the queue fills, new events are dropped and the runtime logger emits a throttled warning.

## Provider notes

- Better Stack and Healthchecks heartbeats usually provide a dedicated URL. Put that URL in `Endpoint`.
- Google Cloud Error Reporting can use an API key in the endpoint URL or an authorization header configured with `Token`, `TokenHeader`, and `TokenScheme`.
- Sentry deployments differ between DSN store endpoints and envelope endpoints. Configure `Endpoint` and token headers to match the ingestion endpoint used by the target deployment.
