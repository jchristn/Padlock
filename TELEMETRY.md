# Padlock Telemetry

Padlock emits metrics and traces through the standard .NET `System.Diagnostics` APIs: a `Meter` and an `ActivitySource`, both named **`Padlock`**. It takes no dependency on any telemetry SDK or exporter and never opens a connection. Your host collects the data and exports it to Prometheus, Tempo, or any OTLP backend.

When nothing subscribes, each lock operation costs only a few flag checks and allocates nothing extra. Instrumentation is best-effort. A failure inside it, including an exception thrown by a subscribed listener, is swallowed and never affects locking.

All names below are constants on `Padlocks.PadlockTelemetry`. They are a public contract: dashboards and alerts may depend on them.

## What it answers

- **Where did the time go?** `padlock.lock.wait.duration` and the `padlock.acquire` span show how long callers waited for a key. The span nests under the caller's current activity, so a slow request trace shows the lock wait as its own bar.
- **What failed?** Acquisitions that were cancelled or threw are labeled `padlock.outcome="cancelled"` or `"error"` with `error.type`, and their spans are marked `Error` with an `exception` event.
- **Is it saturated?** Pending acquisitions, current holders, contended ratio, hold times, active keys, and pool behavior.

## Subscribing

Subscribe to the meter and activity source by name.

With [Radiant](https://www.nuget.org/packages/Radiant):

```csharp
RadiantSettings settings = new RadiantSettings("my-service");
settings.Sources.AddMeter(PadlockTelemetry.MeterName);            // "Padlock"
settings.Sources.AddActivitySource(PadlockTelemetry.ActivitySourceName); // "Padlock"
using (RadiantHost host = RadiantHost.Start(settings)) { /* run the app */ }
```

With the OpenTelemetry SDK:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(PadlockTelemetry.MeterName))
    .WithTracing(t => t.AddSource(PadlockTelemetry.ActivitySourceName));
```

Subscribe at startup, before locks are used. The `holders` and `pending` up-down counters only record changes made while a listener is attached.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Padlock<T>.Name` | `"default"` | Value of the `padlock.name` label on every metric and span from this instance. Use a small, fixed set of names (one per subsystem, for example `"orders"` or `"cache-fill"`). Never derive it from a key, an id, or user input. Instances that share a name are aggregated together. Null or blank values throw. |

```csharp
Padlock<string> padlock = new Padlock<string>(maxCount: 1) { Name = "orders" };
```

There are no other knobs. To turn telemetry off, don't subscribe. To reduce trace volume, use your tracer's sampler or drop the `Padlock` source.

## Metrics catalog

Meter: `Padlock` (version = assembly version). Prometheus names come from the OpenTelemetry Prometheus exporter, which replaces dots with underscores and adds unit and `_total` suffixes.

| Name | Prometheus name | Type | Unit | Labels | Description |
|------|-----------------|------|------|--------|-------------|
| `padlock.lock.wait.duration` | `padlock_lock_wait_duration_seconds` | Histogram | `s` | `padlock.name`, `padlock.mode`, `padlock.outcome`, `padlock.contended`, `error.type` (failures only) | Time from the `Lock`/`LockAsync` call until the lock is held or the attempt fails. Use `_count` for acquisition rate by outcome. |
| `padlock.lock.hold.duration` | `padlock_lock_hold_duration_seconds` | Histogram | `s` | `padlock.name`, `padlock.mode` | Time from acquisition until the handle is disposed. |
| `padlock.lock.holders` | `padlock_lock_holders` | UpDownCounter | `{holder}` | `padlock.name` | Locks currently held. |
| `padlock.lock.pending` | `padlock_lock_pending` | UpDownCounter | `{request}` | `padlock.name`, `padlock.mode` | Acquisitions in progress (the wait queue depth). |
| `padlock.keys.active` | `padlock_keys_active` | ObservableGauge | `{key}` | `padlock.name` | Keys currently tracked (held or waited on). |
| `padlock.max_count` | `padlock_max_count` | ObservableGauge | `{holder}` | `padlock.name` | Configured maximum holders per key (largest value when several instances share a name). |
| `padlock.max_count.changes` | `padlock_max_count_changes_total` | Counter | `{change}` | `padlock.name` | Calls to `SetMaxCount`. |
| `padlock.pool.size` | `padlock_pool_size` | ObservableGauge | `{entry}` | `padlock.name` | Lock entries currently pooled for reuse. |
| `padlock.pool.capacity` | `padlock_pool_capacity` | ObservableGauge | `{entry}` | `padlock.name` | Configured pool capacity (`poolSize`). |
| `padlock.pool.requests` | `padlock_pool_requests_total` | Counter | `{request}` | `padlock.name`, `padlock.pool.result` | Requests for a lock entry. `hit` means the entry was reused; `miss` means it was allocated. |
| `padlock.pool.discards` | `padlock_pool_discards_total` | Counter | `{entry}` | `padlock.name` | Entries discarded because the pool was full. |
| `padlock.instances` | `padlock_instances` | ObservableGauge | `{instance}` | `padlock.name` | Live Padlock instances. |
| `padlock.build.info` | `padlock_build_info` | ObservableGauge | | `padlock.version` | Always 1; carries the library version. |

Histograms suggest explicit bucket boundaries from 10 µs to 120 s (`0.00001` to `120`) through `InstrumentAdvice`, so sub-millisecond waits are resolved. Quantiles are derived in the backend from buckets; Padlock computes none in-process.

### Label values

| Label | Values |
|-------|--------|
| `padlock.name` | Application-defined, from `Padlock<T>.Name` (default `default`) |
| `padlock.mode` | `sync`, `async` |
| `padlock.outcome` | `acquired`, `cancelled`, `error` |
| `padlock.contended` | `true` when the key had no free slot and the caller waited, otherwise `false`. For sync locks this is sampled just before waiting. For async locks it means the wait did not complete synchronously. |
| `padlock.pool.result` | `hit`, `miss` |
| `error.type` | Exception full type name, for example `System.OperationCanceledException`, `System.ArgumentNullException` |
| `padlock.version` | Assembly version, for example `1.2.1` |

Keys are **never** recorded on metrics or spans. They may be ids or personal data, and they are unbounded.

## Spans catalog

Activity source: `Padlock`.

| Span | Kind | When | Attributes | Status |
|------|------|------|------------|--------|
| `padlock.acquire` | Internal | Every `Lock` / `LockAsync` call, while a listener samples it. Duration equals the wait. Its parent is the caller's `Activity.Current`, so W3C trace context follows the caller. | `padlock.name`, `padlock.mode`, `padlock.max_count`, `padlock.contended`, `padlock.outcome`, `error.type` (failures) | `Ok` when acquired. `Error` when cancelled or failed, with an `exception` event (`exception.type`, `exception.message`, `exception.stacktrace`). |

| Span event | Added to | Attributes |
|------------|----------|------------|
| `padlock.max_count.changed` | The caller's `Activity.Current`, when `SetMaxCount` is called inside one | `padlock.name`, `padlock.max_count.previous`, `padlock.max_count` |

The critical section is not wrapped in a span, because doing so would replace the caller's `Activity.Current`. Hold time is a metric (`padlock.lock.hold.duration`). Your own spans inside the `using` block remain children of your request span.

## Recommended PromQL

Panels:

```promql
# Acquisition rate by outcome
sum by (padlock_name, padlock_outcome) (rate(padlock_lock_wait_duration_seconds_count[5m]))

# p95 / p99 lock wait
histogram_quantile(0.95, sum by (le, padlock_name) (rate(padlock_lock_wait_duration_seconds_bucket[5m])))

# Contention ratio (share of acquisitions that had to wait)
sum by (padlock_name) (rate(padlock_lock_wait_duration_seconds_count{padlock_contended="true"}[5m]))
  / sum by (padlock_name) (rate(padlock_lock_wait_duration_seconds_count[5m]))

# p95 hold time
histogram_quantile(0.95, sum by (le, padlock_name) (rate(padlock_lock_hold_duration_seconds_bucket[5m])))

# Queue depth and holders
sum by (padlock_name) (padlock_lock_pending)
sum by (padlock_name) (padlock_lock_holders)

# Pool hit ratio
sum by (padlock_name) (rate(padlock_pool_requests_total{padlock_pool_result="hit"}[5m]))
  / sum by (padlock_name) (rate(padlock_pool_requests_total[5m]))
```

Alerts (tune thresholds to your workload):

```yaml
groups:
  - name: padlock
    rules:
      - alert: PadlockWaitP95High
        expr: histogram_quantile(0.95, sum by (le, padlock_name) (rate(padlock_lock_wait_duration_seconds_bucket[5m]))) > 1
        for: 10m
        annotations:
          summary: "p95 lock wait above 1s on {{ $labels.padlock_name }}"
      - alert: PadlockAcquisitionErrors
        expr: sum by (padlock_name, error_type) (rate(padlock_lock_wait_duration_seconds_count{padlock_outcome="error"}[5m])) > 0
        for: 5m
        annotations:
          summary: "Lock acquisitions failing on {{ $labels.padlock_name }} ({{ $labels.error_type }})"
      - alert: PadlockCancellationsHigh
        expr: |
          sum by (padlock_name) (rate(padlock_lock_wait_duration_seconds_count{padlock_outcome="cancelled"}[5m]))
            / sum by (padlock_name) (rate(padlock_lock_wait_duration_seconds_count[5m])) > 0.05
        for: 10m
        annotations:
          summary: "More than 5% of acquisitions cancelled on {{ $labels.padlock_name }} (timeouts while waiting)"
      - alert: PadlockQueueBacklog
        expr: sum by (padlock_name) (padlock_lock_pending) > 100
        for: 5m
        annotations:
          summary: "More than 100 callers waiting on {{ $labels.padlock_name }}"
      - alert: PadlockHoldersLeaking
        expr: sum by (padlock_name) (padlock_lock_holders) > 0 and sum by (padlock_name) (rate(padlock_lock_hold_duration_seconds_count[15m])) == 0
        for: 15m
        annotations:
          summary: "Locks held but none released for 15m on {{ $labels.padlock_name }} (undisposed handles?)"
```

## Dashboards

Padlock is a library and ships no Grafana stack or dashboards. Add the panels above to the host service's dashboards, typically a "Concurrency" or "Locks" dashboard in the product folder, next to the domain that uses the lock. In Tempo, search for `name="padlock.acquire" && status=error` or for long `padlock.acquire` spans to find the requests that waited.

## Testing

`src/Test.Shared/PadlockTelemetrySuites.cs` subscribes an in-memory `MeterListener` and `ActivityListener` (`TelemetryCapture`) and covers: success metrics and spans for sync and async, span parenting and preserving `Activity.Current`, contention, cancellation, errors, pool hits/misses/discards, every observable gauge, `SetMaxCount`, the no-listener path, and a throwing listener.
