namespace Padlocks
{
    /// <summary>
    /// State a Padlock instance exposes to its observable gauges. Read only at collection time.
    /// </summary>
    internal interface IPadlockObservable
    {
        /// <summary>
        /// Instance name used as the metric label.
        /// </summary>
        string TelemetryName { get; }

        /// <summary>
        /// Configured maximum concurrent holders per key.
        /// </summary>
        int TelemetryMaxCount { get; }

        /// <summary>
        /// Keys currently tracked.
        /// </summary>
        int TelemetryActiveKeys { get; }

        /// <summary>
        /// Entries currently pooled.
        /// </summary>
        int TelemetryPoolSize { get; }

        /// <summary>
        /// Configured pool capacity.
        /// </summary>
        int TelemetryPoolCapacity { get; }
    }
}
