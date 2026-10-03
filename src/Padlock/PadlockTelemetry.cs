namespace Padlocks
{
    using System;

    /// <summary>
    /// Stable public names for the telemetry Padlock emits through <see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/>. These names are a public contract consumed by collectors and
    /// dashboards and do not change between minor versions.
    /// </summary>
    /// <remarks>
    /// Padlock takes no dependency on any telemetry SDK or exporter. A host subscribes by name, for example with Radiant
    /// (<c>settings.Sources.AddMeter(PadlockTelemetry.MeterName)</c> and
    /// <c>settings.Sources.AddActivitySource(PadlockTelemetry.ActivitySourceName)</c>) or with the OpenTelemetry SDK
    /// (<c>AddMeter</c> / <c>AddSource</c>). When nothing subscribes, emission costs a few flag checks and allocates nothing.
    /// This class is thread safe; it contains only constants.
    /// </remarks>
    public static class PadlockTelemetry
    {
        /// <summary>
        /// Name of the <see cref="System.Diagnostics.Metrics.Meter"/> that carries every Padlock metric.
        /// </summary>
        public const string MeterName = "Padlock";

        /// <summary>
        /// Name of the <see cref="System.Diagnostics.ActivitySource"/> that carries every Padlock span.
        /// </summary>
        public const string ActivitySourceName = "Padlock";

        /// <summary>
        /// Default value of <see cref="Padlock{T}.Name"/>, used as the <see cref="AttributeName"/> label when no name is set.
        /// </summary>
        public const string DefaultName = "default";

        /// <summary>
        /// Name of the span opened for each lock acquisition. Its duration is the time spent waiting for the lock.
        /// </summary>
        public const string AcquireSpanName = "padlock.acquire";

        /// <summary>
        /// Name of the span event added to the current activity when <see cref="Padlock{T}.SetMaxCount(int)"/> is called.
        /// </summary>
        public const string MaxCountChangedEventName = "padlock.max_count.changed";

        /// <summary>
        /// Histogram, seconds: time spent acquiring a lock, from the call until the lock is held or the attempt fails.
        /// Labels: <see cref="AttributeName"/>, <see cref="AttributeMode"/>, <see cref="AttributeOutcome"/>,
        /// <see cref="AttributeContended"/>, and <see cref="AttributeErrorType"/> on failures.
        /// </summary>
        public const string LockWaitDuration = "padlock.lock.wait.duration";

        /// <summary>
        /// Histogram, seconds: time a lock was held, from acquisition until the handle is disposed.
        /// Labels: <see cref="AttributeName"/>, <see cref="AttributeMode"/>.
        /// </summary>
        public const string LockHoldDuration = "padlock.lock.hold.duration";

        /// <summary>
        /// UpDownCounter, {holder}: locks currently held. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string LockHolders = "padlock.lock.holders";

        /// <summary>
        /// UpDownCounter, {request}: lock acquisitions currently in progress (queued waiters).
        /// Labels: <see cref="AttributeName"/>, <see cref="AttributeMode"/>.
        /// </summary>
        public const string LockPending = "padlock.lock.pending";

        /// <summary>
        /// ObservableGauge, {key}: keys currently tracked (held or waited on). Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string KeysActive = "padlock.keys.active";

        /// <summary>
        /// ObservableGauge, {holder}: configured maximum concurrent holders per key. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string MaxCount = "padlock.max_count";

        /// <summary>
        /// Counter, {change}: calls to <see cref="Padlock{T}.SetMaxCount(int)"/>. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string MaxCountChanges = "padlock.max_count.changes";

        /// <summary>
        /// ObservableGauge, {entry}: lock entries currently held in the reuse pool. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string PoolSize = "padlock.pool.size";

        /// <summary>
        /// ObservableGauge, {entry}: configured pool capacity. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string PoolCapacity = "padlock.pool.capacity";

        /// <summary>
        /// Counter, {request}: requests for a lock entry, by whether the pool supplied one.
        /// Labels: <see cref="AttributeName"/>, <see cref="AttributePoolResult"/>.
        /// </summary>
        public const string PoolRequests = "padlock.pool.requests";

        /// <summary>
        /// Counter, {entry}: lock entries discarded because the pool was full. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string PoolDiscards = "padlock.pool.discards";

        /// <summary>
        /// ObservableGauge, {instance}: live Padlock instances. Label: <see cref="AttributeName"/>.
        /// </summary>
        public const string Instances = "padlock.instances";

        /// <summary>
        /// ObservableGauge, always 1: build information. Label: <see cref="AttributeVersion"/>.
        /// </summary>
        public const string BuildInfo = "padlock.build.info";

        /// <summary>
        /// Attribute: the instance name from <see cref="Padlock{T}.Name"/>. Bounded by the application.
        /// </summary>
        public const string AttributeName = "padlock.name";

        /// <summary>
        /// Attribute: <see cref="ModeSync"/> or <see cref="ModeAsync"/>.
        /// </summary>
        public const string AttributeMode = "padlock.mode";

        /// <summary>
        /// Attribute: <see cref="OutcomeAcquired"/>, <see cref="OutcomeCancelled"/>, or <see cref="OutcomeError"/>.
        /// </summary>
        public const string AttributeOutcome = "padlock.outcome";

        /// <summary>
        /// Attribute: true when the lock was not immediately available and the caller had to wait.
        /// </summary>
        public const string AttributeContended = "padlock.contended";

        /// <summary>
        /// Attribute (spans only): the per-key concurrency limit in effect for the instance at acquisition time.
        /// </summary>
        public const string AttributeMaxCount = "padlock.max_count";

        /// <summary>
        /// Attribute (span events only): the previous maximum when <see cref="Padlock{T}.SetMaxCount(int)"/> is called.
        /// </summary>
        public const string AttributePreviousMaxCount = "padlock.max_count.previous";

        /// <summary>
        /// Attribute: <see cref="PoolResultHit"/> or <see cref="PoolResultMiss"/>.
        /// </summary>
        public const string AttributePoolResult = "padlock.pool.result";

        /// <summary>
        /// Attribute: the Padlock assembly version.
        /// </summary>
        public const string AttributeVersion = "padlock.version";

        /// <summary>
        /// Attribute: the full type name of the exception that failed an acquisition (OpenTelemetry <c>error.type</c>).
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// Mode value for <see cref="Padlock{T}.Lock(T)"/>.
        /// </summary>
        public const string ModeSync = "sync";

        /// <summary>
        /// Mode value for <see cref="Padlock{T}.LockAsync(T, System.Threading.CancellationToken)"/>.
        /// </summary>
        public const string ModeAsync = "async";

        /// <summary>
        /// Outcome value: the lock was acquired.
        /// </summary>
        public const string OutcomeAcquired = "acquired";

        /// <summary>
        /// Outcome value: the acquisition was cancelled through its <see cref="System.Threading.CancellationToken"/>.
        /// </summary>
        public const string OutcomeCancelled = "cancelled";

        /// <summary>
        /// Outcome value: the acquisition failed with an exception other than cancellation.
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Pool result value: an entry was reused from the pool.
        /// </summary>
        public const string PoolResultHit = "hit";

        /// <summary>
        /// Pool result value: a new entry was allocated.
        /// </summary>
        public const string PoolResultMiss = "miss";
    }
}
