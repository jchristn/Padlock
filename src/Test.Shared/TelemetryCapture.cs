namespace Test.Shared
{
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Diagnostics.Metrics;
	using Padlocks;

	/// <summary>
	/// In-memory subscriber to the Padlock meter and activity source, used to prove telemetry is emitted.
	/// </summary>
	public sealed class TelemetryCapture : IDisposable
	{
		/// <summary>
		/// Name of an activity source tests use to open parent spans.
		/// </summary>
		public const string TestSourceName = "Test.Padlock";

		private readonly MeterListener _MeterListener;
		private readonly ActivityListener _ActivityListener;
		private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
		private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();

		/// <summary>
		/// Starts capturing Padlock metrics and spans.
		/// </summary>
		/// <param name="captureMetrics">True to subscribe to the meter.</param>
		/// <param name="captureTraces">True to subscribe to the activity source.</param>
		public TelemetryCapture(bool captureMetrics = true, bool captureTraces = true)
		{
			_MeterListener = new MeterListener();
			if (captureMetrics)
			{
				_MeterListener.InstrumentPublished = (instrument, listener) =>
				{
					if (instrument.Meter.Name == PadlockTelemetry.MeterName) listener.EnableMeasurementEvents(instrument);
				};
				_MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Capture(instrument, value, tags));
				_MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Capture(instrument, value, tags));
				_MeterListener.Start();
			}

			_ActivityListener = new ActivityListener
			{
				ShouldListenTo = source => captureTraces && (source.Name == PadlockTelemetry.ActivitySourceName || source.Name == TestSourceName),
				Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
				ActivityStopped = activity => _Activities.Enqueue(activity)
			};
			ActivitySource.AddActivityListener(_ActivityListener);
		}

		/// <summary>
		/// Captured measurements.
		/// </summary>
		public IReadOnlyList<CapturedMeasurement> Measurements => _Measurements.ToList();

		/// <summary>
		/// Captured, stopped activities.
		/// </summary>
		public IReadOnlyList<Activity> Activities => _Activities.ToList();

		/// <summary>
		/// Returns measurements for one instrument and one padlock name.
		/// </summary>
		/// <param name="instrument">Instrument name.</param>
		/// <param name="padlockName">Value of the padlock.name tag.</param>
		/// <returns>Matching measurements.</returns>
		public List<CapturedMeasurement> For(string instrument, string padlockName)
		{
			return _Measurements
				.Where(m => m.Instrument == instrument && m.Tag(PadlockTelemetry.AttributeName) == padlockName)
				.ToList();
		}

		/// <summary>
		/// Returns acquisition spans for one padlock name.
		/// </summary>
		/// <param name="padlockName">Value of the padlock.name tag.</param>
		/// <returns>Matching activities.</returns>
		public List<Activity> SpansFor(string padlockName)
		{
			return _Activities
				.Where(a => a.OperationName == PadlockTelemetry.AcquireSpanName
					&& (a.GetTagItem(PadlockTelemetry.AttributeName) as string) == padlockName)
				.ToList();
		}

		/// <summary>
		/// Invokes every observable gauge callback so its current values are captured.
		/// </summary>
		public void Observe()
		{
			_MeterListener.RecordObservableInstruments();
		}

		/// <summary>
		/// Stops capturing.
		/// </summary>
		public void Dispose()
		{
			_MeterListener.Dispose();
			_ActivityListener.Dispose();
		}

		private void Capture(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
		{
			Dictionary<string, object?> copy = new Dictionary<string, object?>();
			foreach (KeyValuePair<string, object?> tag in tags) copy[tag.Key] = tag.Value;
			_Measurements.Enqueue(new CapturedMeasurement(instrument.Name, value, copy));
		}
	}
}
