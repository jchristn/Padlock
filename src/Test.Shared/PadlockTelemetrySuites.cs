namespace Test.Shared
{
	using System.Diagnostics;
	using System.Diagnostics.Metrics;
	using Padlocks;
	using Touchstone.Core;

	/// <summary>
	/// Proves Padlock emits its documented metrics and spans, on success and failure paths, and that locking keeps
	/// working with no listener or with a misbehaving listener.
	/// </summary>
	public static class PadlockTelemetrySuites
	{
		/// <summary>
		/// The telemetry suite.
		/// </summary>
		/// <returns>Suite descriptor.</returns>
		public static TestSuiteDescriptor TelemetrySuite()
		{
			const string suiteId = "Telemetry";

			return new TestSuiteDescriptor(
				suiteId,
				"Telemetry emission",
				new List<TestCaseDescriptor>
				{
					Case(suiteId, "StableNames", "Exposes the documented meter and activity source names", StableNamesAsync),
					Case(suiteId, "NoListenerDoesNotThrow", "Locks normally when nothing subscribes", NoListenerDoesNotThrowAsync),
					Case(suiteId, "ThrowingListenerDoesNotBreakLocking", "Locks normally when a metric listener throws", ThrowingListenerDoesNotBreakLockingAsync),
					Case(suiteId, "NameValidation", "Rejects null and blank names and defaults to 'default'", NameValidationAsync),
					Case(suiteId, "SyncAcquireMetrics", "Records wait, hold, holders, and pending for a sync lock", SyncAcquireMetricsAsync),
					Case(suiteId, "AsyncAcquireSpan", "Opens an acquire span with status Ok for an async lock", AsyncAcquireSpanAsync),
					Case(suiteId, "SpanNestsUnderCaller", "Nests the acquire span under the caller's current activity", SpanNestsUnderCallerAsync),
					Case(suiteId, "ContendedAcquire", "Marks a waited acquisition as contended and reports it pending", ContendedAcquireAsync),
					Case(suiteId, "CancellationRecorded", "Records cancelled outcome, error.type, and an Error span", CancellationRecordedAsync),
					Case(suiteId, "ErrorRecorded", "Records error outcome and error.type for a null key", ErrorRecordedAsync),
					Case(suiteId, "PoolMetrics", "Counts pool hits, misses, and discards", PoolMetricsAsync),
					Case(suiteId, "ObservableGauges", "Reports active keys, max count, pool, instances, and build info", ObservableGaugesAsync),
					Case(suiteId, "MaxCountChange", "Counts SetMaxCount calls and adds a span event", MaxCountChangeAsync)
				});
		}

		private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
		{
			return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
		}

		private static string UniqueName()
		{
			return "test-" + Guid.NewGuid().ToString("N");
		}

		private static Task StableNamesAsync(CancellationToken cancellationToken)
		{
			AssertEqual("Padlock", PadlockTelemetry.MeterName, "Meter name changed.");
			AssertEqual("Padlock", PadlockTelemetry.ActivitySourceName, "Activity source name changed.");
			AssertEqual("padlock.lock.wait.duration", PadlockTelemetry.LockWaitDuration, "Wait histogram name changed.");
			AssertEqual("padlock.acquire", PadlockTelemetry.AcquireSpanName, "Span name changed.");
			return Task.CompletedTask;
		}

		private static async Task NoListenerDoesNotThrowAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 1, poolSize: 0) { Name = UniqueName() };
			using (padlock.Lock("a")) { }
			using (await padlock.LockAsync("a")) { }

			IDisposable holder = padlock.Lock("b");
			using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
			{
				await AssertThrowsAsync<OperationCanceledException>(async () => await padlock.LockAsync("b", cts.Token));
			}
			holder.Dispose();
			padlock.SetMaxCount(2);
			AssertFalse(padlock.IsLocked("a") || padlock.IsLocked("b"), "Keys should be unlocked.");
		}

		private static async Task ThrowingListenerDoesNotBreakLockingAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string> { Name = UniqueName() };
			using (MeterListener listener = new MeterListener())
			{
				listener.InstrumentPublished = (instrument, l) =>
				{
					if (instrument.Meter.Name == PadlockTelemetry.MeterName) l.EnableMeasurementEvents(instrument);
				};
				listener.SetMeasurementEventCallback<long>((i, v, t, s) => throw new InvalidOperationException("listener failure"));
				listener.SetMeasurementEventCallback<double>((i, v, t, s) => throw new InvalidOperationException("listener failure"));
				listener.Start();

				using (padlock.Lock("k")) { }
				using (await padlock.LockAsync("k")) { }
				padlock.SetMaxCount(3);
			}
			AssertFalse(padlock.IsLocked("k"), "Key should be unlocked.");
		}

		private static Task NameValidationAsync(CancellationToken cancellationToken)
		{
			Padlock<int> padlock = new Padlock<int>();
			AssertEqual(PadlockTelemetry.DefaultName, padlock.Name, "Unexpected default name.");
			AssertThrows<ArgumentNullException>(() => padlock.Name = null!);
			AssertThrows<ArgumentException>(() => padlock.Name = "  ");
			padlock.Name = "orders";
			AssertEqual("orders", padlock.Name, "Name not applied.");
			return Task.CompletedTask;
		}

		private static Task SyncAcquireMetricsAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> padlock = new Padlock<string> { Name = name };
				using (padlock.Lock("k"))
				{
					AssertEqual(1.0, capture.For(PadlockTelemetry.LockHolders, name).Sum(m => m.Value), "Holder not counted while held.");
					Thread.Sleep(20);
				}

				List<CapturedMeasurement> waits = capture.For(PadlockTelemetry.LockWaitDuration, name);
				AssertEqual(1, waits.Count, "Expected one wait measurement.");
				AssertEqual(PadlockTelemetry.ModeSync, waits[0].Tag(PadlockTelemetry.AttributeMode), "Wrong mode.");
				AssertEqual(PadlockTelemetry.OutcomeAcquired, waits[0].Tag(PadlockTelemetry.AttributeOutcome), "Wrong outcome.");
				AssertEqual("false", waits[0].Tag(PadlockTelemetry.AttributeContended), "Uncontended lock reported contended.");
				AssertEqual(null, waits[0].Tag(PadlockTelemetry.AttributeErrorType), "error.type set on success.");

				List<CapturedMeasurement> holds = capture.For(PadlockTelemetry.LockHoldDuration, name);
				AssertEqual(1, holds.Count, "Expected one hold measurement.");
				AssertTrue(holds[0].Value >= 0.015, "Hold duration too short: " + holds[0].Value);

				AssertEqual(0.0, capture.For(PadlockTelemetry.LockHolders, name).Sum(m => m.Value), "Holders not balanced.");
				List<CapturedMeasurement> pending = capture.For(PadlockTelemetry.LockPending, name);
				AssertEqual(2, pending.Count, "Expected pending +1 and -1.");
				AssertEqual(0.0, pending.Sum(m => m.Value), "Pending not balanced.");

				List<Activity> spans = capture.SpansFor(name);
				AssertEqual(1, spans.Count, "Expected one acquire span.");
				AssertEqual(PadlockTelemetry.ModeSync, spans[0].GetTagItem(PadlockTelemetry.AttributeMode) as string, "Span mode wrong.");
			}
			return Task.CompletedTask;
		}

		private static async Task AsyncAcquireSpanAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> padlock = new Padlock<string>(maxCount: 2) { Name = name };
				using (await padlock.LockAsync("k")) { }

				List<Activity> spans = capture.SpansFor(name);
				AssertEqual(1, spans.Count, "Expected one acquire span.");
				Activity span = spans[0];
				AssertEqual(ActivityKind.Internal, span.Kind, "Wrong span kind.");
				AssertEqual(ActivityStatusCode.Ok, span.Status, "Wrong span status.");
				AssertEqual(PadlockTelemetry.ModeAsync, span.GetTagItem(PadlockTelemetry.AttributeMode) as string, "Span mode wrong.");
				AssertEqual(PadlockTelemetry.OutcomeAcquired, span.GetTagItem(PadlockTelemetry.AttributeOutcome) as string, "Span outcome wrong.");
				AssertEqual((object)2, span.GetTagItem(PadlockTelemetry.AttributeMaxCount), "Span max count wrong.");
				AssertEqual((object)false, span.GetTagItem(PadlockTelemetry.AttributeContended), "Span contended wrong.");

				List<CapturedMeasurement> waits = capture.For(PadlockTelemetry.LockWaitDuration, name);
				AssertEqual(1, waits.Count, "Expected one wait measurement.");
				AssertEqual(PadlockTelemetry.ModeAsync, waits[0].Tag(PadlockTelemetry.AttributeMode), "Wrong mode.");
			}
		}

		private static async Task SpanNestsUnderCallerAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			using (ActivitySource testSource = new ActivitySource(TelemetryCapture.TestSourceName))
			{
				Padlock<string> padlock = new Padlock<string> { Name = name };
				Activity? parent = testSource.StartActivity("request");
				AssertTrue(parent != null, "Parent activity not created.");
				using (await padlock.LockAsync("k")) { }
				AssertTrue(ReferenceEquals(parent, Activity.Current), "LockAsync changed Activity.Current for the caller.");
				using (padlock.Lock("k")) { }
				AssertTrue(ReferenceEquals(parent, Activity.Current), "Lock changed Activity.Current for the caller.");
				parent!.Stop();

				List<Activity> spans = capture.SpansFor(name);
				AssertEqual(2, spans.Count, "Expected two acquire spans.");
				foreach (Activity span in spans)
				{
					AssertEqual(parent.TraceId, span.TraceId, "Span not in caller trace.");
					AssertEqual(parent.SpanId, span.ParentSpanId, "Span not parented to caller.");
				}
			}
		}

		private static async Task ContendedAcquireAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> padlock = new Padlock<string> { Name = name };
				IDisposable holder = padlock.Lock("k");
				Task<IDisposable> waiter = padlock.LockAsync("k").AsTask();
				await Task.Delay(50);
				AssertFalse(waiter.IsCompleted, "Waiter should be blocked.");

				double pendingAsync = capture.For(PadlockTelemetry.LockPending, name)
					.Where(m => m.Tag(PadlockTelemetry.AttributeMode) == PadlockTelemetry.ModeAsync)
					.Sum(m => m.Value);
				AssertEqual(1.0, pendingAsync, "Waiter not reported pending.");

				capture.Observe();
				AssertEqual(1.0, capture.For(PadlockTelemetry.KeysActive, name).Last().Value, "Active key not reported.");

				holder.Dispose();
				using (await waiter) { }

				CapturedMeasurement wait = capture.For(PadlockTelemetry.LockWaitDuration, name)
					.Single(m => m.Tag(PadlockTelemetry.AttributeMode) == PadlockTelemetry.ModeAsync);
				AssertEqual("true", wait.Tag(PadlockTelemetry.AttributeContended), "Waited lock not reported contended.");
				AssertTrue(wait.Value >= 0.04, "Contended wait too short: " + wait.Value);
				AssertEqual(0.0, capture.For(PadlockTelemetry.LockPending, name).Sum(m => m.Value), "Pending not balanced.");

				Activity span = capture.SpansFor(name).Single(a => (a.GetTagItem(PadlockTelemetry.AttributeMode) as string) == PadlockTelemetry.ModeAsync);
				AssertEqual((object)true, span.GetTagItem(PadlockTelemetry.AttributeContended), "Span contended wrong.");
				AssertTrue(span.Duration >= TimeSpan.FromMilliseconds(40), "Span duration does not reflect wait.");
			}
		}

		private static async Task CancellationRecordedAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> padlock = new Padlock<string> { Name = name };
				using (padlock.Lock("k"))
				{
					using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30)))
					{
						await AssertThrowsAsync<OperationCanceledException>(async () => await padlock.LockAsync("k", cts.Token));
					}
				}

				CapturedMeasurement wait = capture.For(PadlockTelemetry.LockWaitDuration, name)
					.Single(m => m.Tag(PadlockTelemetry.AttributeOutcome) == PadlockTelemetry.OutcomeCancelled);
				AssertEqual(PadlockTelemetry.ModeAsync, wait.Tag(PadlockTelemetry.AttributeMode), "Wrong mode.");
				AssertTrue((wait.Tag(PadlockTelemetry.AttributeErrorType) ?? "").EndsWith("CanceledException", StringComparison.Ordinal), "error.type missing.");
				AssertEqual(0.0, capture.For(PadlockTelemetry.LockPending, name).Sum(m => m.Value), "Pending not balanced after cancel.");
				AssertEqual(0.0, capture.For(PadlockTelemetry.LockHolders, name).Sum(m => m.Value), "Holders not balanced after cancel.");

				Activity span = capture.SpansFor(name).Single(a => (a.GetTagItem(PadlockTelemetry.AttributeOutcome) as string) == PadlockTelemetry.OutcomeCancelled);
				AssertEqual(ActivityStatusCode.Error, span.Status, "Cancelled span not Error.");
				AssertTrue(span.Events.Any(e => e.Name == "exception"), "Exception event missing.");
			}
		}

		private static async Task ErrorRecordedAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> padlock = new Padlock<string> { Name = name };
				AssertThrows<ArgumentNullException>(() => padlock.Lock(null!));
				await AssertThrowsAsync<ArgumentNullException>(async () => await padlock.LockAsync(null!));

				List<CapturedMeasurement> waits = capture.For(PadlockTelemetry.LockWaitDuration, name);
				AssertEqual(2, waits.Count, "Expected two failed wait measurements.");
				foreach (CapturedMeasurement wait in waits)
				{
					AssertEqual(PadlockTelemetry.OutcomeError, wait.Tag(PadlockTelemetry.AttributeOutcome), "Wrong outcome.");
					AssertEqual(typeof(ArgumentNullException).FullName, wait.Tag(PadlockTelemetry.AttributeErrorType), "Wrong error.type.");
				}

				List<Activity> spans = capture.SpansFor(name);
				AssertEqual(2, spans.Count, "Expected two failed spans.");
				AssertTrue(spans.All(s => s.Status == ActivityStatusCode.Error), "Failed span not Error.");
			}
		}

		private static Task PoolMetricsAsync(CancellationToken cancellationToken)
		{
			string pooled = UniqueName();
			string unpooled = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> padlock = new Padlock<string>(poolSize: 1) { Name = pooled };
				using (padlock.Lock("a")) { }
				using (padlock.Lock("a")) { }

				List<CapturedMeasurement> requests = capture.For(PadlockTelemetry.PoolRequests, pooled);
				AssertEqual(1, requests.Count(m => m.Tag(PadlockTelemetry.AttributePoolResult) == PadlockTelemetry.PoolResultMiss), "Expected one miss.");
				AssertEqual(1, requests.Count(m => m.Tag(PadlockTelemetry.AttributePoolResult) == PadlockTelemetry.PoolResultHit), "Expected one hit.");

				Padlock<string> noPool = new Padlock<string>(poolSize: 0) { Name = unpooled };
				using (noPool.Lock("a")) { }
				AssertEqual(1, capture.For(PadlockTelemetry.PoolDiscards, unpooled).Count, "Expected one discard.");

				capture.Observe();
				AssertEqual(1.0, capture.For(PadlockTelemetry.PoolSize, pooled).Last().Value, "Pool size wrong.");
				AssertEqual(1.0, capture.For(PadlockTelemetry.PoolCapacity, pooled).Last().Value, "Pool capacity wrong.");
				AssertEqual(0.0, capture.For(PadlockTelemetry.PoolSize, unpooled).Last().Value, "Unpooled size wrong.");
				GC.KeepAlive(padlock);
				GC.KeepAlive(noPool);
			}
			return Task.CompletedTask;
		}

		private static Task ObservableGaugesAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			{
				Padlock<string> first = new Padlock<string>(maxCount: 3, poolSize: 7) { Name = name };
				Padlock<int> second = new Padlock<int>(maxCount: 5, poolSize: 2) { Name = name };
				IDisposable a = first.Lock("a");
				IDisposable b = first.Lock("b");
				IDisposable c = second.Lock(1);

				capture.Observe();
				AssertEqual(3.0, capture.For(PadlockTelemetry.KeysActive, name).Last().Value, "Active keys not summed across instances.");
				AssertEqual(5.0, capture.For(PadlockTelemetry.MaxCount, name).Last().Value, "Max count should report the largest value.");
				AssertEqual(9.0, capture.For(PadlockTelemetry.PoolCapacity, name).Last().Value, "Pool capacity not summed.");
				AssertEqual(2.0, capture.For(PadlockTelemetry.Instances, name).Last().Value, "Instances not counted.");

				CapturedMeasurement build = capture.Measurements.Last(m => m.Instrument == PadlockTelemetry.BuildInfo);
				AssertEqual(1.0, build.Value, "Build info value wrong.");
				AssertTrue(!String.IsNullOrEmpty(build.Tag(PadlockTelemetry.AttributeVersion)), "Build info version missing.");

				a.Dispose();
				b.Dispose();
				c.Dispose();
				capture.Observe();
				AssertEqual(0.0, capture.For(PadlockTelemetry.KeysActive, name).Last().Value, "Active keys not cleared.");
				GC.KeepAlive(first);
				GC.KeepAlive(second);
			}
			return Task.CompletedTask;
		}

		private static Task MaxCountChangeAsync(CancellationToken cancellationToken)
		{
			string name = UniqueName();
			using (TelemetryCapture capture = new TelemetryCapture())
			using (ActivitySource testSource = new ActivitySource(TelemetryCapture.TestSourceName))
			{
				Padlock<string> padlock = new Padlock<string>(maxCount: 1) { Name = name };
				using (Activity? parent = testSource.StartActivity("reconfigure"))
				{
					padlock.SetMaxCount(4);
					AssertTrue(parent != null, "Parent activity not created.");
					ActivityEvent evt = parent!.Events.Single(e => e.Name == PadlockTelemetry.MaxCountChangedEventName);
					AssertEqual((object)1, evt.Tags.First(t => t.Key == PadlockTelemetry.AttributePreviousMaxCount).Value, "Previous max count wrong.");
					AssertEqual((object)4, evt.Tags.First(t => t.Key == PadlockTelemetry.AttributeMaxCount).Value, "New max count wrong.");
				}

				AssertEqual(1, capture.For(PadlockTelemetry.MaxCountChanges, name).Count, "Change not counted.");
				capture.Observe();
				AssertEqual(4.0, capture.For(PadlockTelemetry.MaxCount, name).Last().Value, "Max count gauge not updated.");
				GC.KeepAlive(padlock);
			}
			return Task.CompletedTask;
		}

		private static void AssertEqual<T>(T expected, T actual, string message)
		{
			if (!EqualityComparer<T>.Default.Equals(expected, actual))
			{
				throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}.");
			}
		}

		private static void AssertTrue(bool condition, string message)
		{
			if (!condition) throw new InvalidOperationException(message);
		}

		private static void AssertFalse(bool condition, string message)
		{
			if (condition) throw new InvalidOperationException(message);
		}

		private static void AssertThrows<TException>(Action action) where TException : Exception
		{
			try
			{
				action();
			}
			catch (TException)
			{
				return;
			}
			throw new InvalidOperationException("Expected " + typeof(TException).Name + ".");
		}

		private static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
		{
			try
			{
				await action();
			}
			catch (TException)
			{
				return;
			}
			throw new InvalidOperationException("Expected " + typeof(TException).Name + ".");
		}
	}
}
