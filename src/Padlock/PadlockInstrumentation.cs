namespace Padlocks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Process-wide Padlock meter, activity source, and instruments. Every method is best-effort: a failure inside
    /// instrumentation (including an exception thrown by a subscribed listener) is swallowed and never affects locking.
    /// </summary>
    internal static class PadlockInstrumentation
    {
        internal static readonly string Version = GetVersion();

        internal static readonly ActivitySource Source = new ActivitySource(PadlockTelemetry.ActivitySourceName, Version);

        internal static readonly Meter Meter = new Meter(PadlockTelemetry.MeterName, Version);

        private static readonly double[] _DurationBuckets = new double[]
        {
            0.00001, 0.00005, 0.0001, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05,
            0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120
        };

        private static readonly Histogram<double> _WaitDuration = Meter.CreateHistogram<double>(
            PadlockTelemetry.LockWaitDuration,
            "s",
            "Time spent acquiring a lock, from the call until the lock is held or the attempt fails.",
            null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _DurationBuckets });

        private static readonly Histogram<double> _HoldDuration = Meter.CreateHistogram<double>(
            PadlockTelemetry.LockHoldDuration,
            "s",
            "Time a lock was held, from acquisition until the handle was disposed.",
            null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _DurationBuckets });

        private static readonly UpDownCounter<long> _Holders = Meter.CreateUpDownCounter<long>(
            PadlockTelemetry.LockHolders,
            "{holder}",
            "Locks currently held.");

        private static readonly UpDownCounter<long> _Pending = Meter.CreateUpDownCounter<long>(
            PadlockTelemetry.LockPending,
            "{request}",
            "Lock acquisitions currently in progress.");

        private static readonly Counter<long> _MaxCountChanges = Meter.CreateCounter<long>(
            PadlockTelemetry.MaxCountChanges,
            "{change}",
            "Runtime changes to the maximum concurrent holders per key.");

        private static readonly Counter<long> _PoolRequests = Meter.CreateCounter<long>(
            PadlockTelemetry.PoolRequests,
            "{request}",
            "Requests for a lock entry, by whether the pool supplied one.");

        private static readonly Counter<long> _PoolDiscards = Meter.CreateCounter<long>(
            PadlockTelemetry.PoolDiscards,
            "{entry}",
            "Lock entries discarded because the pool was full.");

        private static readonly object _RegistryLock = new object();

        private static readonly List<WeakReference<IPadlockObservable>> _Registry = new List<WeakReference<IPadlockObservable>>();

        private static int _RegistrationsSincePrune = 0;

        static PadlockInstrumentation()
        {
            Meter.CreateObservableGauge<long>(
                PadlockTelemetry.KeysActive,
                () => Observe(p => p.TelemetryActiveKeys, false),
                "{key}",
                "Keys currently tracked (held or waited on).");

            Meter.CreateObservableGauge<long>(
                PadlockTelemetry.MaxCount,
                () => Observe(p => p.TelemetryMaxCount, true),
                "{holder}",
                "Configured maximum concurrent holders per key.");

            Meter.CreateObservableGauge<long>(
                PadlockTelemetry.PoolSize,
                () => Observe(p => p.TelemetryPoolSize, false),
                "{entry}",
                "Lock entries currently held in the reuse pool.");

            Meter.CreateObservableGauge<long>(
                PadlockTelemetry.PoolCapacity,
                () => Observe(p => p.TelemetryPoolCapacity, false),
                "{entry}",
                "Configured pool capacity.");

            Meter.CreateObservableGauge<long>(
                PadlockTelemetry.Instances,
                () => Observe(p => 1, false),
                "{instance}",
                "Live Padlock instances.");

            Meter.CreateObservableGauge<long>(
                PadlockTelemetry.BuildInfo,
                () => new Measurement<long>(1, new KeyValuePair<string, object>(PadlockTelemetry.AttributeVersion, Version)),
                null,
                "Padlock build information.");
        }

        internal static void Register(IPadlockObservable padlock)
        {
            try
            {
                lock (_RegistryLock)
                {
                    _Registry.Add(new WeakReference<IPadlockObservable>(padlock));
                    _RegistrationsSincePrune++;
                    if (_RegistrationsSincePrune >= 256)
                    {
                        _RegistrationsSincePrune = 0;
                        _Registry.RemoveAll(r => !r.TryGetTarget(out _));
                    }
                }
            }
            catch
            {
            }
        }

        internal static long GetWaitStartTimestamp()
        {
            return _WaitDuration.Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        internal static long GetHoldStartTimestamp()
        {
            return _HoldDuration.Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        internal static bool AddPending(string name, string mode)
        {
            if (!_Pending.Enabled) return false;
            try
            {
                _Pending.Add(1, new TagList
                {
                    { PadlockTelemetry.AttributeName, name },
                    { PadlockTelemetry.AttributeMode, mode }
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void RemovePending(bool counted, string name, string mode)
        {
            if (!counted) return;
            try
            {
                _Pending.Add(-1, new TagList
                {
                    { PadlockTelemetry.AttributeName, name },
                    { PadlockTelemetry.AttributeMode, mode }
                });
            }
            catch
            {
            }
        }

        internal static Activity StartAcquire(string name, string mode, int maxCount)
        {
            if (!Source.HasListeners()) return null;
            try
            {
                Activity activity = Source.StartActivity(PadlockTelemetry.AcquireSpanName, ActivityKind.Internal);
                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(PadlockTelemetry.AttributeName, name);
                    activity.SetTag(PadlockTelemetry.AttributeMode, mode);
                    activity.SetTag(PadlockTelemetry.AttributeMaxCount, maxCount);
                }
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void AcquireSucceeded(Activity activity, string name, string mode, bool contended, long waitStartTimestamp)
        {
            try
            {
                if (activity != null)
                {
                    activity.SetTag(PadlockTelemetry.AttributeContended, contended);
                    activity.SetTag(PadlockTelemetry.AttributeOutcome, PadlockTelemetry.OutcomeAcquired);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }

                if (waitStartTimestamp != 0)
                {
                    _WaitDuration.Record(ElapsedSeconds(waitStartTimestamp), new TagList
                    {
                        { PadlockTelemetry.AttributeName, name },
                        { PadlockTelemetry.AttributeMode, mode },
                        { PadlockTelemetry.AttributeOutcome, PadlockTelemetry.OutcomeAcquired },
                        { PadlockTelemetry.AttributeContended, contended }
                    });
                }
            }
            catch
            {
            }
        }

        internal static void AcquireFailed(Activity activity, string name, string mode, bool contended, long waitStartTimestamp, Exception exception)
        {
            try
            {
                string outcome = exception is OperationCanceledException ? PadlockTelemetry.OutcomeCancelled : PadlockTelemetry.OutcomeError;
                string errorType = exception.GetType().FullName;

                if (activity != null)
                {
                    activity.SetTag(PadlockTelemetry.AttributeContended, contended);
                    activity.SetTag(PadlockTelemetry.AttributeOutcome, outcome);
                    activity.SetTag(PadlockTelemetry.AttributeErrorType, errorType);
                    activity.SetStatus(
                        ActivityStatusCode.Error,
                        outcome == PadlockTelemetry.OutcomeCancelled ? "Lock acquisition was cancelled." : "Lock acquisition failed.");
                    activity.AddEvent(new ActivityEvent("exception", default, new ActivityTagsCollection
                    {
                        { "exception.type", errorType },
                        { "exception.message", exception.Message },
                        { "exception.stacktrace", exception.ToString() }
                    }));
                }

                if (waitStartTimestamp != 0)
                {
                    _WaitDuration.Record(ElapsedSeconds(waitStartTimestamp), new TagList
                    {
                        { PadlockTelemetry.AttributeName, name },
                        { PadlockTelemetry.AttributeMode, mode },
                        { PadlockTelemetry.AttributeOutcome, outcome },
                        { PadlockTelemetry.AttributeContended, contended },
                        { PadlockTelemetry.AttributeErrorType, errorType }
                    });
                }
            }
            catch
            {
            }
        }

        internal static void StopActivity(Activity activity)
        {
            if (activity == null) return;
            try
            {
                activity.Dispose();
            }
            catch
            {
            }
        }

        internal static bool AddHolder(string name)
        {
            if (!_Holders.Enabled) return false;
            try
            {
                _Holders.Add(1, new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, name));
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void ReleaseHolder(bool counted, string name, string mode, long holdStartTimestamp)
        {
            try
            {
                if (counted)
                {
                    _Holders.Add(-1, new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, name));
                }

                if (holdStartTimestamp != 0)
                {
                    _HoldDuration.Record(
                        ElapsedSeconds(holdStartTimestamp),
                        new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, name),
                        new KeyValuePair<string, object>(PadlockTelemetry.AttributeMode, mode));
                }
            }
            catch
            {
            }
        }

        internal static void RecordPoolRequest(string name, bool hit)
        {
            if (!_PoolRequests.Enabled) return;
            try
            {
                _PoolRequests.Add(
                    1,
                    new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, name),
                    new KeyValuePair<string, object>(PadlockTelemetry.AttributePoolResult, hit ? PadlockTelemetry.PoolResultHit : PadlockTelemetry.PoolResultMiss));
            }
            catch
            {
            }
        }

        internal static void RecordPoolDiscard(string name)
        {
            if (!_PoolDiscards.Enabled) return;
            try
            {
                _PoolDiscards.Add(1, new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, name));
            }
            catch
            {
            }
        }

        internal static void RecordMaxCountChange(string name, int previous, int current)
        {
            try
            {
                if (_MaxCountChanges.Enabled)
                {
                    _MaxCountChanges.Add(1, new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, name));
                }

                Activity activity = Activity.Current;
                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.AddEvent(new ActivityEvent(PadlockTelemetry.MaxCountChangedEventName, default, new ActivityTagsCollection
                    {
                        { PadlockTelemetry.AttributeName, name },
                        { PadlockTelemetry.AttributePreviousMaxCount, previous },
                        { PadlockTelemetry.AttributeMaxCount, current }
                    }));
                }
            }
            catch
            {
            }
        }

        private static double ElapsedSeconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
        }

        private static IEnumerable<Measurement<long>> Observe(Func<IPadlockObservable, int> selector, bool useMaximum)
        {
            Dictionary<string, long> values = new Dictionary<string, long>(StringComparer.Ordinal);
            try
            {
                lock (_RegistryLock)
                {
                    _Registry.RemoveAll(r => !r.TryGetTarget(out _));
                    _RegistrationsSincePrune = 0;

                    foreach (WeakReference<IPadlockObservable> reference in _Registry)
                    {
                        if (!reference.TryGetTarget(out IPadlockObservable padlock)) continue;
                        string name = padlock.TelemetryName;
                        long value = selector(padlock);
                        if (values.TryGetValue(name, out long existing))
                        {
                            values[name] = useMaximum ? Math.Max(existing, value) : existing + value;
                        }
                        else
                        {
                            values[name] = value;
                        }
                    }
                }
            }
            catch
            {
            }

            List<Measurement<long>> measurements = new List<Measurement<long>>(values.Count);
            foreach (KeyValuePair<string, long> pair in values)
            {
                measurements.Add(new Measurement<long>(pair.Value, new KeyValuePair<string, object>(PadlockTelemetry.AttributeName, pair.Key)));
            }
            return measurements;
        }

        private static string GetVersion()
        {
            try
            {
                Version version = typeof(PadlockInstrumentation).Assembly.GetName().Version;
                return version != null ? version.ToString(3) : "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }
    }
}
