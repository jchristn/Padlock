namespace Test.Shared
{
	using System.Collections;
	using System.Collections.Concurrent;
	using System.Reflection;
	using Padlocks;
	using Touchstone.Core;

	public static class PadlockSuites
	{
		public static IReadOnlyList<TestSuiteDescriptor> All
		{
			get
			{
				return new List<TestSuiteDescriptor>
				{
					CoreBehaviorSuite(),
					ConcurrencySuite(),
					ResourceManagementSuite()
				};
			}
		}

		public static TestSuiteDescriptor CoreBehaviorSuite()
		{
			const string suiteId = "Core";

			return new TestSuiteDescriptor(
				suiteId,
				"Core Padlock behavior",
				new List<TestCaseDescriptor>
				{
					Case(suiteId, "StringKeys", "Locks with string keys under contention", SyncStringKeysUnderContentionAsync),
					Case(suiteId, "IntKeys", "Locks with integer keys under contention", SyncIntKeysUnderContentionAsync),
					Case(suiteId, "GuidKeys", "Locks with GUID keys under contention", SyncGuidKeysUnderContentionAsync),
					Case(suiteId, "CustomKeys", "Locks with custom value keys", SyncCustomKeysUnderContentionAsync),
					Case(suiteId, "MixedSyncAsync", "Mixes sync and async locks on the same key", MixedSyncAsyncSameKeyAsync),
					Case(suiteId, "EquivalentCustomKeys", "Treats equivalent custom key instances as the same key", EquivalentCustomKeysBlockEachOtherAsync),
					Case(suiteId, "ReferenceNullKeys", "Rejects null reference keys consistently", ReferenceNullKeysAsync),
					Case(suiteId, "ReusableAfterRelease", "Reuses a key after sync and async release", ReusableAfterReleaseAsync),
					Case(suiteId, "ConstructorValidation", "Rejects invalid constructor values", ConstructorValidationAsync),
					Case(suiteId, "UnknownKeyIsNotLocked", "Reports unknown keys as unlocked", UnknownKeyIsNotLockedAsync),
					Case(suiteId, "LockAcquisitionSmoke", "Acquires and releases a hot key repeatedly", LockAcquisitionSmokeAsync),
					Case(suiteId, "DefaultValueTypeKeys", "Treats default value-type keys as valid, distinct keys", DefaultValueTypeKeysAsync),
					Case(suiteId, "SeparateInstancesIndependent", "Isolates lock state between separate Padlock instances", SeparateInstancesIndependentAsync)
				});
		}

		public static TestSuiteDescriptor ConcurrencySuite()
		{
			const string suiteId = "Concurrency";

			return new TestSuiteDescriptor(
				suiteId,
				"Concurrency and cancellation",
				new List<TestCaseDescriptor>
				{
					Case(suiteId, "SingleKeySyncExclusion", "Serializes synchronous access for one key", SingleKeySyncExclusionAsync),
					Case(suiteId, "SingleKeyAsyncExclusion", "Serializes asynchronous access for one key", SingleKeyAsyncExclusionAsync),
					Case(suiteId, "SeparateKeysIndependent", "Does not block unrelated keys", SeparateKeysIndependentAsync),
					Case(suiteId, "DifferentKeysOverlap", "Allows unrelated keys to execute concurrently", DifferentKeysOverlapAsync),
					Case(suiteId, "SyncWaiterBlocksUntilRelease", "Blocks same-key sync waiter until release", SyncWaiterBlocksUntilReleaseAsync),
					Case(suiteId, "AsyncWaiterBlocksUntilRelease", "Blocks same-key async waiter until release", AsyncWaiterBlocksUntilReleaseAsync),
					Case(suiteId, "MaxCountAllowsConfiguredConcurrency", "Allows maxCount holders and blocks the next waiter", MaxCountAllowsConfiguredConcurrencyAsync),
					Case(suiteId, "MaxCountInvariantUnderLoad", "Never exceeds maxCount under heavy same-key load", MaxCountInvariantUnderLoadAsync),
					Case(suiteId, "MixedMaxCountInvariantUnderLoad", "Never exceeds maxCount with mixed sync and async callers", MixedMaxCountInvariantUnderLoadAsync),
					Case(suiteId, "IsLockedWithExclusiveLock", "Reports exclusive lock state", IsLockedWithExclusiveLockAsync),
					Case(suiteId, "IsLockedWithMaxCount", "Reports maxCount lock state only when all slots are held", IsLockedWithMaxCountAsync),
					Case(suiteId, "CancellationWhileWaiting", "Cancels an async waiter while the key is held", CancellationWhileWaitingAsync),
					Case(suiteId, "CancellationDoesNotReleaseHolder", "Does not release an active holder when a waiter cancels", CancellationDoesNotReleaseHolderAsync),
					Case(suiteId, "CanceledWaiterDoesNotConsumeSlot", "Does not consume capacity after waiter cancellation", CanceledWaiterDoesNotConsumeSlotAsync),
					Case(suiteId, "ManyCanceledWaitersThenSuccess", "Recovers after many canceled waiters", ManyCanceledWaitersThenSuccessAsync),
					Case(suiteId, "AlreadyCanceledTokenCleanup", "Cleans up immediately canceled async acquisition", AlreadyCanceledTokenCleanupAsync),
					Case(suiteId, "DisposeUnderContention", "Releases queued waiters when a holder is disposed", DisposeUnderContentionAsync),
					Case(suiteId, "AcquireReleaseStormNoLeaks", "Leaves no lock entries after a mixed acquire/release storm", AcquireReleaseStormNoLeaksAsync),
					Case(suiteId, "MultiTypeStress", "Handles mixed key types under load", MultiTypeStressAsync),
					Case(suiteId, "MaxCountSyncWaiterBlocks", "Blocks a synchronous maxCount waiter until a slot frees", MaxCountSyncWaiterBlocksAsync),
					Case(suiteId, "SetMaxCountValidation", "Rejects invalid runtime maxCount values", SetMaxCountValidationAsync),
					Case(suiteId, "MaxCountReflectsSetValue", "Reports the latest runtime maxCount value", MaxCountReflectsSetValueAsync),
					Case(suiteId, "SetMaxCountAppliesToNewKeys", "Applies a runtime maxCount change to newly acquired keys", SetMaxCountAppliesToNewKeysAsync),
					Case(suiteId, "SetMaxCountIncreaseVisibleAfterRecycle", "Applies a raised maxCount once a key is reacquired", SetMaxCountIncreaseVisibleAfterRecycleAsync),
					Case(suiteId, "SetMaxCountDecreaseAppliesAfterIdle", "Applies a lowered maxCount once a key drains and is reacquired", SetMaxCountDecreaseAppliesAfterIdleAsync),
					Case(suiteId, "SetMaxCountActiveKeyKeepsOldLimit", "Keeps the old limit for a key active at the time of the change", SetMaxCountActiveKeyKeepsOldLimitAsync),
					Case(suiteId, "RuntimeMaxCountStressWithChanges", "Never exceeds the configured ceiling while maxCount changes under load", RuntimeMaxCountStressWithChangesAsync)
				});
		}

		public static TestSuiteDescriptor ResourceManagementSuite()
		{
			const string suiteId = "Resources";

			return new TestSuiteDescriptor(
				suiteId,
				"Resource management",
				new List<TestCaseDescriptor>
				{
					Case(suiteId, "KeyRemovalAfterRelease", "Removes lock entries after release", KeyRemovalAfterReleaseAsync),
					Case(suiteId, "CancellationDoesNotLeakKeys", "Does not leak keys after canceled waiters", CancellationDoesNotLeakKeysAsync),
					Case(suiteId, "PoolSizeLimit", "Keeps the object pool within the configured size", PoolSizeLimitAsync),
					Case(suiteId, "PoolReusesEntries", "Returns entries to the pool for reuse", PoolReusesEntriesAsync),
					Case(suiteId, "PoolReuseResetsState", "Resets pooled entries before reuse", PoolReuseResetsStateAsync),
					Case(suiteId, "PoolSizeZero", "Does not pool entries when poolSize is zero", PoolSizeZeroAsync),
					Case(suiteId, "EntryRetainedUntilAllRefsReleased", "Retains entries until every holder and waiter releases", EntryRetainedUntilAllRefsReleasedAsync),
					Case(suiteId, "CanceledWaiterRefCountCleanup", "Decrements entry ref counts when async waiters cancel", CanceledWaiterRefCountCleanupAsync),
					Case(suiteId, "DoubleDispose", "Allows a lock handle to be disposed twice", DoubleDisposeAsync)
				});
		}

		private static TestCaseDescriptor Case(
			string suiteId,
			string caseId,
			string displayName,
			Func<CancellationToken, Task> executeAsync)
		{
			return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
		}

		private static async Task SyncStringKeysUnderContentionAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			string[] keys = { "apple", "banana", "cherry", "date", "elderberry" };
			int counter = 0;
			int taskCount = 100;
			int iterations = 10;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
			{
				Random random = new Random(taskIndex);
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					using (padlock.Lock(keys[random.Next(keys.Length)]))
					{
						Interlocked.Increment(ref counter);
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * iterations, counter, "Unexpected string-key operation count.");
		}

		private static async Task SyncIntKeysUnderContentionAsync(CancellationToken cancellationToken)
		{
			Padlock<int> padlock = new Padlock<int>();
			int counter = 0;
			int taskCount = 20;
			int iterations = 100;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
			{
				Random random = new Random(taskIndex);
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					using (padlock.Lock(random.Next(1, 6)))
					{
						Interlocked.Increment(ref counter);
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * iterations, counter, "Unexpected integer-key operation count.");
		}

		private static async Task SyncGuidKeysUnderContentionAsync(CancellationToken cancellationToken)
		{
			Padlock<Guid> padlock = new Padlock<Guid>();
			Guid[] keys = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
			int counter = 0;
			int taskCount = 20;
			int iterations = 100;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
			{
				Random random = new Random(taskIndex);
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					using (padlock.Lock(keys[random.Next(keys.Length)]))
					{
						Interlocked.Increment(ref counter);
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * iterations, counter, "Unexpected GUID-key operation count.");
		}

		private static async Task SyncCustomKeysUnderContentionAsync(CancellationToken cancellationToken)
		{
			Padlock<CustomKey> padlock = new Padlock<CustomKey>();
			CustomKey[] keys =
			{
				new CustomKey("User", 1),
				new CustomKey("Admin", 2),
				new CustomKey("Guest", 3),
				new CustomKey("System", 4),
				new CustomKey("Service", 5)
			};
			int counter = 0;
			int taskCount = 20;
			int iterations = 100;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
			{
				Random random = new Random(taskIndex);
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					using (padlock.Lock(keys[random.Next(keys.Length)]))
					{
						Interlocked.Increment(ref counter);
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * iterations, counter, "Unexpected custom-key operation count.");
		}

		private static async Task MixedSyncAsyncSameKeyAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			int counter = 0;
			int taskCount = 20;
			int iterations = 50;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(async () =>
			{
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (taskIndex % 2 == 0)
					{
						using (await padlock.LockAsync("shared", cancellationToken))
						{
							counter++;
						}
					}
					else
					{
						using (padlock.Lock("shared"))
						{
							counter++;
						}
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * iterations, counter, "Sync and async locking lost updates.");
		}

		private static async Task EquivalentCustomKeysBlockEachOtherAsync(CancellationToken cancellationToken)
		{
			Padlock<CustomKey> padlock = new Padlock<CustomKey>();
			CustomKey firstInstance = new CustomKey("Tenant", 42);
			CustomKey secondEquivalentInstance = new CustomKey("Tenant", 42);
			IDisposable holder = padlock.Lock(firstInstance);
			bool acquired = false;

			Task waiter = Task.Run(async () =>
			{
				using (await padlock.LockAsync(secondEquivalentInstance, cancellationToken))
				{
					acquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(acquired, "Equivalent custom key instance acquired before the first equivalent holder released.");
			AssertEqual(1, GetLockDictionaryCount(padlock), "Equivalent custom keys should share one dictionary entry.");

			holder.Dispose();
			await waiter.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(acquired, "Equivalent custom key instance did not acquire after release.");
			AssertEqual(0, GetLockDictionaryCount(padlock), "Equivalent custom key entry was not removed after all releases.");
		}

		private static async Task ReferenceNullKeysAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>();
			AssertThrows<ArgumentNullException>(() => padlock.Lock(null!), "Synchronous null key should be rejected.");
			AssertThrows<ArgumentNullException>(() => padlock.IsLocked(null!), "IsLocked null key should be rejected.");
			await AssertThrowsAsync<ArgumentNullException>(
				async () => { using (await padlock.LockAsync(null!, cancellationToken)) { } },
				"Asynchronous null key should be rejected.");
			AssertEqual(0, GetLockDictionaryCount(padlock), "Rejected null keys should not leave lock dictionary entries.");
		}

		private static async Task ReusableAfterReleaseAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();

			using (padlock.Lock("reusable"))
			{
				AssertTrue(padlock.IsLocked("reusable"), "Key should be locked while sync handle is held.");
			}

			AssertFalse(padlock.IsLocked("reusable"), "Key should be unlocked after sync handle disposal.");

			using (await padlock.LockAsync("reusable", cancellationToken))
			{
				AssertTrue(padlock.IsLocked("reusable"), "Key should be locked while async handle is held.");
			}

			AssertFalse(padlock.IsLocked("reusable"), "Key should be unlocked after async handle disposal.");
		}

		private static Task ConstructorValidationAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			AssertThrows<ArgumentOutOfRangeException>(() => new Padlock<string>(maxCount: 0), "maxCount below one should throw.");
			AssertThrows<ArgumentOutOfRangeException>(() => new Padlock<string>(poolSize: -1), "Negative poolSize should throw.");

			return Task.CompletedTask;
		}

		private static Task UnknownKeyIsNotLockedAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>();
			AssertFalse(padlock.IsLocked("missing"), "Unknown keys should not be reported as locked.");

			return Task.CompletedTask;
		}

		private static async Task LockAcquisitionSmokeAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			long operations = 0;
			int taskCount = Environment.ProcessorCount;
			int iterations = 2_000;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(() =>
			{
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					using (padlock.Lock("hot-key"))
					{
						operations++;
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual((long)taskCount * iterations, operations, "Hot-key acquisition smoke test lost updates.");
		}

		private static async Task DefaultValueTypeKeysAsync(CancellationToken cancellationToken)
		{
			// Value-type keys can never be null, so the default value (0, Guid.Empty)
			// must behave as an ordinary, valid key rather than being rejected.
			Padlock<int> intLock = new Padlock<int>();
			AssertFalse(intLock.IsLocked(0), "Default int key should start unlocked.");

			using (intLock.Lock(0))
			{
				AssertTrue(intLock.IsLocked(0), "Default int key should be locked while held.");
				AssertFalse(intLock.IsLocked(1), "Locking key 0 should not lock an unrelated key.");
			}

			AssertFalse(intLock.IsLocked(0), "Default int key should be unlocked after release.");
			AssertEqual(0, GetLockDictionaryCount(intLock), "Default int key entry should be removed after release.");

			Padlock<Guid> guidLock = new Padlock<Guid>();
			AssertFalse(guidLock.IsLocked(Guid.Empty), "Empty GUID key should start unlocked.");

			using (await guidLock.LockAsync(Guid.Empty, cancellationToken))
			{
				AssertTrue(guidLock.IsLocked(Guid.Empty), "Empty GUID key should be locked while held.");
			}

			AssertFalse(guidLock.IsLocked(Guid.Empty), "Empty GUID key should be unlocked after release.");
			AssertEqual(0, GetLockDictionaryCount(guidLock), "Empty GUID key entry should be removed after release.");
		}

		private static async Task SeparateInstancesIndependentAsync(CancellationToken cancellationToken)
		{
			Padlock<string> first = new Padlock<string>();
			Padlock<string> second = new Padlock<string>();

			using IDisposable firstHolder = first.Lock("shared-key");
			AssertTrue(first.IsLocked("shared-key"), "First instance should report its own key as locked.");
			AssertFalse(second.IsLocked("shared-key"), "Second instance should not observe the first instance's lock.");

			// The same key on a separate instance must acquire immediately; if instances
			// shared state this would block and the timeout below would fire.
			IDisposable secondHolder = await second.LockAsync("shared-key", cancellationToken)
				.AsTask()
				.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
			AssertTrue(second.IsLocked("shared-key"), "Second instance should acquire the same key independently.");

			secondHolder.Dispose();
			AssertTrue(first.IsLocked("shared-key"), "Releasing the second instance must not affect the first.");
			AssertFalse(second.IsLocked("shared-key"), "Second instance should be unlocked after its own release.");
		}

		private static async Task SingleKeySyncExclusionAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			int counter = 0;
			int taskCount = 50;
			int operationsPerTask = 200;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(() =>
			{
				for (int i = 0; i < operationsPerTask; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					using (padlock.Lock("exclusive"))
					{
						counter++;
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * operationsPerTask, counter, "Synchronous same-key lock did not preserve all increments.");
		}

		private static async Task SingleKeyAsyncExclusionAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			int counter = 0;
			int taskCount = 50;
			int operationsPerTask = 200;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () =>
			{
				for (int i = 0; i < operationsPerTask; i++)
				{
					using (await padlock.LockAsync("exclusive-async", cancellationToken))
					{
						counter++;
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(taskCount * operationsPerTask, counter, "Asynchronous same-key lock did not preserve all increments.");
		}

		private static async Task SeparateKeysIndependentAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			using IDisposable first = padlock.Lock("first");

			Task<IDisposable> secondTask = padlock.LockAsync("second", cancellationToken).AsTask();
			IDisposable second = await secondTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
			second.Dispose();
		}

		private static async Task DifferentKeysOverlapAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			TaskCompletionSource<object?> firstInside = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			TaskCompletionSource<object?> releaseFirst = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			int concurrentInside = 0;
			int maxConcurrentInside = 0;

			Task first = Task.Run(async () =>
			{
				using (padlock.Lock("first-overlap"))
				{
					RecordConcurrentEntry(ref concurrentInside, ref maxConcurrentInside);
					firstInside.SetResult(null);
					await releaseFirst.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
					Interlocked.Decrement(ref concurrentInside);
				}
			}, cancellationToken);

			await firstInside.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

			using (await padlock.LockAsync("second-overlap", cancellationToken))
			{
				RecordConcurrentEntry(ref concurrentInside, ref maxConcurrentInside);
				Interlocked.Decrement(ref concurrentInside);
			}

			releaseFirst.SetResult(null);
			await first.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(maxConcurrentInside >= 2, "Independent keys did not overlap while one key was held.");
		}

		private static async Task SyncWaiterBlocksUntilReleaseAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("blocked");
			TaskCompletionSource<object?> waiterStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			bool acquired = false;

			Task waiter = Task.Run(() =>
			{
				waiterStarted.SetResult(null);
				using (padlock.Lock("blocked"))
				{
					acquired = true;
				}
			}, cancellationToken);

			await waiterStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
			await Task.Delay(100, cancellationToken);
			AssertFalse(acquired, "Same-key waiter acquired the lock before the holder was released.");

			holder.Dispose();
			await waiter.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(acquired, "Same-key waiter did not acquire the lock after release.");
		}

		private static async Task AsyncWaiterBlocksUntilReleaseAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("async-blocked");
			bool acquired = false;

			Task waiter = Task.Run(async () =>
			{
				using (await padlock.LockAsync("async-blocked", cancellationToken))
				{
					acquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(acquired, "Async same-key waiter acquired before the holder was released.");

			holder.Dispose();
			await waiter.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(acquired, "Async same-key waiter did not acquire after release.");
		}

		private static async Task MaxCountAllowsConfiguredConcurrencyAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 2);
			IDisposable first = padlock.Lock("limited");
			IDisposable second = padlock.Lock("limited");
			bool thirdAcquired = false;

			Task third = Task.Run(async () =>
			{
				using (await padlock.LockAsync("limited", cancellationToken))
				{
					thirdAcquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(thirdAcquired, "Third waiter acquired a maxCount-limited key before a slot was released.");
			AssertTrue(padlock.IsLocked("limited"), "Key should be reported locked when all maxCount slots are held.");

			first.Dispose();
			await third.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			second.Dispose();
			AssertTrue(thirdAcquired, "Third waiter did not acquire after a maxCount slot was released.");
		}

		private static async Task MaxCountInvariantUnderLoadAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 3);
			int currentHolders = 0;
			int maxObservedHolders = 0;
			int taskCount = 40;
			int iterations = 40;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () =>
			{
				for (int i = 0; i < iterations; i++)
				{
					using (await padlock.LockAsync("limited-load", cancellationToken))
					{
						RecordConcurrentEntry(ref currentHolders, ref maxObservedHolders);
						await Task.Yield();
						Interlocked.Decrement(ref currentHolders);
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertTrue(maxObservedHolders <= 3, $"Observed {maxObservedHolders} concurrent holders with maxCount 3.");
			AssertEqual(0, GetLockDictionaryCount(padlock), "Max-count load test left lock entries behind.");
		}

		private static async Task MixedMaxCountInvariantUnderLoadAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 4);
			int currentHolders = 0;
			int maxObservedHolders = 0;
			int taskCount = 32;
			int iterations = 50;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(async () =>
			{
				for (int i = 0; i < iterations; i++)
				{
					if ((taskIndex + i) % 2 == 0)
					{
						using (padlock.Lock("mixed-limited-load"))
						{
							RecordConcurrentEntry(ref currentHolders, ref maxObservedHolders);
							Thread.Sleep(1);
							Interlocked.Decrement(ref currentHolders);
						}
					}
					else
					{
						using (await padlock.LockAsync("mixed-limited-load", cancellationToken))
						{
							RecordConcurrentEntry(ref currentHolders, ref maxObservedHolders);
							await Task.Yield();
							Interlocked.Decrement(ref currentHolders);
						}
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertTrue(maxObservedHolders <= 4, $"Observed {maxObservedHolders} concurrent mixed holders with maxCount 4.");
			AssertEqual(0, GetLockDictionaryCount(padlock), "Mixed max-count load test left lock entries behind.");
		}

		private static Task IsLockedWithExclusiveLockAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>();
			AssertFalse(padlock.IsLocked("key"), "Key should start unlocked.");

			IDisposable holder = padlock.Lock("key");
			AssertTrue(padlock.IsLocked("key"), "Exclusive key should be locked while held.");

			holder.Dispose();
			AssertFalse(padlock.IsLocked("key"), "Exclusive key should be unlocked after release.");

			return Task.CompletedTask;
		}

		private static Task IsLockedWithMaxCountAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>(maxCount: 2);
			AssertFalse(padlock.IsLocked("key"), "Key should start unlocked.");

			IDisposable first = padlock.Lock("key");
			AssertFalse(padlock.IsLocked("key"), "Key should not be fully locked while one maxCount slot remains.");

			IDisposable second = padlock.Lock("key");
			AssertTrue(padlock.IsLocked("key"), "Key should be locked when all maxCount slots are held.");

			first.Dispose();
			AssertFalse(padlock.IsLocked("key"), "Key should not be locked after one maxCount slot is released.");

			second.Dispose();
			AssertFalse(padlock.IsLocked("key"), "Key should be unlocked after all maxCount slots are released.");

			return Task.CompletedTask;
		}

		private static async Task CancellationWhileWaitingAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			using IDisposable holder = padlock.Lock("cancel");
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(100);

			await AssertThrowsAsync<OperationCanceledException>(
				async () => { using (await padlock.LockAsync("cancel", timeout.Token)) { } },
				"Async waiter should observe cancellation while waiting.");
		}

		private static async Task CancellationDoesNotReleaseHolderAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("cancel-held");
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(100);

			await AssertThrowsAsync<OperationCanceledException>(
				async () => { using (await padlock.LockAsync("cancel-held", timeout.Token)) { } },
				"Waiting acquisition should cancel.");

			AssertTrue(padlock.IsLocked("cancel-held"), "Canceling a waiter should not release the active holder.");
			bool secondAcquired = false;
			Task secondWaiter = Task.Run(async () =>
			{
				using (await padlock.LockAsync("cancel-held", cancellationToken))
				{
					secondAcquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(secondAcquired, "A new waiter acquired before the original holder was released.");

			holder.Dispose();
			await secondWaiter.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(secondAcquired, "New waiter did not acquire after original holder released.");
		}

		private static async Task CanceledWaiterDoesNotConsumeSlotAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 2);
			IDisposable first = padlock.Lock("cancel-slot");
			IDisposable second = padlock.Lock("cancel-slot");
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(100);

			await AssertThrowsAsync<OperationCanceledException>(
				async () => { using (await padlock.LockAsync("cancel-slot", timeout.Token)) { } },
				"Third waiter should cancel while both maxCount slots are held.");

			first.Dispose();
			IDisposable replacement = await padlock.LockAsync("cancel-slot", cancellationToken);
			replacement.Dispose();
			second.Dispose();

			AssertEqual(0, GetLockDictionaryCount(padlock), "Canceled waiter consumed capacity or leaked an entry.");
		}

		private static async Task ManyCanceledWaitersThenSuccessAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("many-cancel");

			Task[] canceledWaiters = Enumerable.Range(0, 25).Select(_ => Task.Run(async () =>
			{
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeout.CancelAfter(20);
				try
				{
					using (await padlock.LockAsync("many-cancel", timeout.Token))
					{
					}
				}
				catch (OperationCanceledException)
				{
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(canceledWaiters);
			AssertEqual(1, GetLockEntryRefCount(padlock, "many-cancel"), "Canceled waiters should leave only the active holder reference.");

			holder.Dispose();
			using (await padlock.LockAsync("many-cancel", cancellationToken))
			{
				AssertTrue(padlock.IsLocked("many-cancel"), "Key should remain usable after canceled waiters.");
			}

			AssertEqual(0, GetLockDictionaryCount(padlock), "Key leaked after recovery from canceled waiters.");
		}

		private static async Task AlreadyCanceledTokenCleanupAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			using CancellationTokenSource canceled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			canceled.Cancel();

			await AssertThrowsAsync<OperationCanceledException>(
				async () => { using (await padlock.LockAsync("already-canceled", canceled.Token)) { } },
				"Already canceled token should prevent acquisition.");

			AssertEqual(0, GetLockDictionaryCount(padlock), "Canceled acquisition should not leave a lock entry.");
		}

		private static async Task DisposeUnderContentionAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("dispose-contention");
			int waiterCount = 10;
			int completed = 0;

			Task[] waiters = Enumerable.Range(0, waiterCount).Select(_ => Task.Run(async () =>
			{
				using (await padlock.LockAsync("dispose-contention", cancellationToken))
				{
					Interlocked.Increment(ref completed);
					await Task.Delay(5, cancellationToken);
				}
			}, cancellationToken)).ToArray();

			await Task.Delay(100, cancellationToken);
			holder.Dispose();

			await Task.WhenAll(waiters).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
			AssertEqual(waiterCount, completed, "Not all waiters completed after the holder was disposed.");
		}

		private static async Task AcquireReleaseStormNoLeaksAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 3, poolSize: 8);
			string[] keys = Enumerable.Range(0, 12).Select(i => "storm-" + i).ToArray();
			int taskCount = 48;
			int iterations = 100;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(async () =>
			{
				Random random = new Random(taskIndex * 17);
				for (int i = 0; i < iterations; i++)
				{
					string key = keys[random.Next(keys.Length)];
					if (random.Next(2) == 0)
					{
						using (padlock.Lock(key))
						{
						}
					}
					else
					{
						using (await padlock.LockAsync(key, cancellationToken))
						{
						}
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);
			AssertEqual(0, GetLockDictionaryCount(padlock), "Acquire/release storm left lock dictionary entries behind.");
			AssertTrue(GetPoolCount(padlock) <= 8, "Acquire/release storm exceeded the configured pool limit.");
		}

		private static async Task MultiTypeStressAsync(CancellationToken cancellationToken)
		{
			Padlock<string> stringLock = new Padlock<string>();
			Padlock<int> intLock = new Padlock<int>();
			Padlock<Guid> guidLock = new Padlock<Guid>();
			ConcurrentDictionary<string, int> strings = new ConcurrentDictionary<string, int>();
			ConcurrentDictionary<int, int> ints = new ConcurrentDictionary<int, int>();
			ConcurrentDictionary<Guid, int> guids = new ConcurrentDictionary<Guid, int>();
			string[] stringKeys = { "a", "b", "c", "d", "e" };
			int[] intKeys = { 1, 2, 3, 4, 5 };
			Guid[] guidKeys = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
			int taskCount = 30;
			int iterations = 100;

			Task[] tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(async () =>
			{
				Random random = new Random(taskIndex);
				for (int i = 0; i < iterations; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					int operation = random.Next(3);
					if (operation == 0)
					{
						string key = stringKeys[random.Next(stringKeys.Length)];
						using (stringLock.Lock(key))
						{
							strings.AddOrUpdate(key, 1, (_, value) => value + 1);
						}
					}
					else if (operation == 1)
					{
						int key = intKeys[random.Next(intKeys.Length)];
						using (await intLock.LockAsync(key, cancellationToken))
						{
							ints.AddOrUpdate(key, 1, (_, value) => value + 1);
						}
					}
					else
					{
						Guid key = guidKeys[random.Next(guidKeys.Length)];
						using (guidLock.Lock(key))
						{
							guids.AddOrUpdate(key, 1, (_, value) => value + 1);
						}
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(tasks);

			int total = strings.Values.Sum() + ints.Values.Sum() + guids.Values.Sum();
			AssertEqual(taskCount * iterations, total, "Mixed key stress test lost operations.");
		}

		private static async Task MaxCountSyncWaiterBlocksAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 2);
			IDisposable first = padlock.Lock("mc-sync");
			IDisposable second = padlock.Lock("mc-sync");
			AssertTrue(padlock.IsLocked("mc-sync"), "Key should be locked when both maxCount slots are held.");

			TaskCompletionSource<object?> waiterStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			bool acquired = false;

			Task waiter = Task.Run(() =>
			{
				waiterStarted.SetResult(null);
				using (padlock.Lock("mc-sync"))
				{
					acquired = true;
				}
			}, cancellationToken);

			await waiterStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
			await Task.Delay(100, cancellationToken);
			AssertFalse(acquired, "Synchronous waiter acquired before a maxCount slot was released.");

			first.Dispose();
			await waiter.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(acquired, "Synchronous waiter did not acquire after a maxCount slot was released.");

			second.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Max-count sync waiter test left lock entries behind.");
		}

		private static Task SetMaxCountValidationAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>(maxCount: 3);
			AssertThrows<ArgumentOutOfRangeException>(() => padlock.SetMaxCount(0), "Runtime maxCount below one should throw.");
			AssertThrows<ArgumentOutOfRangeException>(() => padlock.SetMaxCount(-5), "Negative runtime maxCount should throw.");
			AssertEqual(3, padlock.MaxCount, "Rejected runtime maxCount changes should not alter the current value.");

			return Task.CompletedTask;
		}

		private static Task MaxCountReflectsSetValueAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>();
			AssertEqual(1, padlock.MaxCount, "Default maxCount should be one.");

			padlock.SetMaxCount(7);
			AssertEqual(7, padlock.MaxCount, "MaxCount should reflect the most recent SetMaxCount value.");

			padlock.SetMaxCount(2);
			AssertEqual(2, padlock.MaxCount, "MaxCount should reflect a subsequent SetMaxCount value.");

			return Task.CompletedTask;
		}

		private static async Task SetMaxCountAppliesToNewKeysAsync(CancellationToken cancellationToken)
		{
			// A key that has never been acquired must adopt the runtime maxCount value in effect
			// at the time of its first acquisition.
			Padlock<string> padlock = new Padlock<string>(maxCount: 1);
			padlock.SetMaxCount(3);

			IDisposable first = padlock.Lock("fresh");
			IDisposable second = padlock.Lock("fresh");
			AssertFalse(padlock.IsLocked("fresh"), "Two of three slots held should leave the key not fully locked.");

			IDisposable third = padlock.Lock("fresh");
			AssertTrue(padlock.IsLocked("fresh"), "All three slots held should report the key as locked.");

			bool fourthAcquired = false;
			Task fourth = Task.Run(async () =>
			{
				using (await padlock.LockAsync("fresh", cancellationToken))
				{
					fourthAcquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(fourthAcquired, "A fourth holder acquired before a slot on the new maxCount was released.");

			first.Dispose();
			await fourth.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(fourthAcquired, "Fourth holder did not acquire after a slot was released.");

			second.Dispose();
			third.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Runtime maxCount new-key test left lock entries behind.");
		}

		private static Task SetMaxCountIncreaseVisibleAfterRecycleAsync(CancellationToken cancellationToken)
		{
			// A key acquired under the old (lower) limit must adopt an increased limit once it
			// has drained and is acquired again.
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>(maxCount: 1);

			using (padlock.Lock("recycled"))
			{
				AssertTrue(padlock.IsLocked("recycled"), "Exclusive key should be locked while held.");
			}

			padlock.SetMaxCount(3);

			IDisposable first = padlock.Lock("recycled");
			IDisposable second = padlock.Lock("recycled");
			IDisposable third = padlock.Lock("recycled");
			AssertTrue(padlock.IsLocked("recycled"), "Reacquired key should expose the raised concurrency of three slots.");

			first.Dispose();
			second.Dispose();
			third.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Runtime maxCount increase test left lock entries behind.");

			return Task.CompletedTask;
		}

		private static async Task SetMaxCountDecreaseAppliesAfterIdleAsync(CancellationToken cancellationToken)
		{
			// A key acquired under the old (higher) limit must adopt a decreased limit once it
			// has drained and is acquired again.
			Padlock<string> padlock = new Padlock<string>(maxCount: 3);

			IDisposable a = padlock.Lock("shrink");
			IDisposable b = padlock.Lock("shrink");
			a.Dispose();
			b.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Key should drain before the decrease is validated.");

			padlock.SetMaxCount(1);

			IDisposable holder = padlock.Lock("shrink");
			AssertTrue(padlock.IsLocked("shrink"), "Reacquired key should now be exclusive under the lowered limit.");

			bool secondAcquired = false;
			Task second = Task.Run(async () =>
			{
				using (await padlock.LockAsync("shrink", cancellationToken))
				{
					secondAcquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(secondAcquired, "A second holder acquired despite the lowered exclusive limit.");

			holder.Dispose();
			await second.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(secondAcquired, "Second holder did not acquire after the exclusive holder released.");

			AssertEqual(0, GetLockDictionaryCount(padlock), "Runtime maxCount decrease test left lock entries behind.");
		}

		private static async Task SetMaxCountActiveKeyKeepsOldLimitAsync(CancellationToken cancellationToken)
		{
			// Documents the lazy boundary: a key active at the moment of the change keeps its old
			// limit until it fully drains, and only then adopts the new limit.
			Padlock<string> padlock = new Padlock<string>(maxCount: 3);

			IDisposable first = padlock.Lock("active");

			// Lower the limit while the key is still active.
			padlock.SetMaxCount(1);

			// The live entry retains its original three-slot capacity, so a second holder still gets in.
			// Acquire with a timeout so a regression (immediate application) fails cleanly instead of hanging.
			IDisposable second = await padlock.LockAsync("active", cancellationToken)
				.AsTask()
				.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

			first.Dispose();
			second.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Active key entry should be removed after all holders release.");

			// After draining, the next acquisition adopts the lowered limit.
			IDisposable exclusive = padlock.Lock("active");
			AssertTrue(padlock.IsLocked("active"), "Reacquired key should now honor the lowered exclusive limit.");

			bool blockedAcquired = false;
			Task blocked = Task.Run(async () =>
			{
				using (await padlock.LockAsync("active", cancellationToken))
				{
					blockedAcquired = true;
				}
			}, cancellationToken);

			await Task.Delay(100, cancellationToken);
			AssertFalse(blockedAcquired, "A second holder acquired after the key adopted the lowered exclusive limit.");

			exclusive.Dispose();
			await blocked.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(blockedAcquired, "Blocked holder did not acquire after the exclusive holder released.");

			AssertEqual(0, GetLockDictionaryCount(padlock), "Lazy-boundary test left lock entries behind.");
		}

		private static async Task RuntimeMaxCountStressWithChangesAsync(CancellationToken cancellationToken)
		{
			// Long-running, multi-threaded test that repeatedly changes maxCount while many workers
			// acquire and release a small set of keys. The core invariant is that the number of
			// concurrent holders for any key never exceeds the highest maxCount ever configured,
			// because each entry is sized from whichever maxCount was current when it was created.
			const int ceiling = 5;
			int[] allowedCounts = { 1, 2, 3, 4, 5 };
			string[] keys = { "stress-0", "stress-1", "stress-2", "stress-3" };

			Padlock<string> padlock = new Padlock<string>(maxCount: 1);

			int[] currentPerKey = new int[keys.Length];
			int[] maxObservedPerKey = new int[keys.Length];
			long totalOperations = 0;

			int workerCount = 32;
			int iterationsPerWorker = 500;
			int changerRunning = 1;

			// Background task that continuously churns the instance-wide maxCount.
			Task changer = Task.Run(async () =>
			{
				int index = 0;
				while (Volatile.Read(ref changerRunning) == 1)
				{
					index = (index + 1) % allowedCounts.Length;
					padlock.SetMaxCount(allowedCounts[index]);
					await Task.Yield();
				}
			}, cancellationToken);

			Task[] workers = Enumerable.Range(0, workerCount).Select(workerIndex => Task.Run(async () =>
			{
				Random random = new Random(workerIndex * 31 + 7);
				for (int i = 0; i < iterationsPerWorker; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					int keyIndex = random.Next(keys.Length);
					string key = keys[keyIndex];

					if ((workerIndex + i) % 2 == 0)
					{
						using (padlock.Lock(key))
						{
							RecordConcurrentEntry(ref currentPerKey[keyIndex], ref maxObservedPerKey[keyIndex]);
							Thread.SpinWait(50);
							Interlocked.Decrement(ref currentPerKey[keyIndex]);
							Interlocked.Increment(ref totalOperations);
						}
					}
					else
					{
						using (await padlock.LockAsync(key, cancellationToken))
						{
							RecordConcurrentEntry(ref currentPerKey[keyIndex], ref maxObservedPerKey[keyIndex]);
							await Task.Yield();
							Interlocked.Decrement(ref currentPerKey[keyIndex]);
							Interlocked.Increment(ref totalOperations);
						}
					}
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(workers);
			Volatile.Write(ref changerRunning, 0);
			await changer.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

			for (int i = 0; i < keys.Length; i++)
			{
				AssertTrue(
					maxObservedPerKey[i] <= ceiling,
					$"Key {keys[i]} observed {maxObservedPerKey[i]} concurrent holders, exceeding the ceiling of {ceiling}.");
			}

			int overallMaxObserved = maxObservedPerKey.Max();
			AssertTrue(
				overallMaxObserved >= 2,
				"Expected raised maxCount values to produce more than one concurrent holder at some point under load.");

			AssertEqual((long)workerCount * iterationsPerWorker, totalOperations, "Runtime maxCount stress test lost operations.");
			AssertEqual(0, GetLockDictionaryCount(padlock), "Runtime maxCount stress test left lock entries behind.");
		}

		private static Task KeyRemovalAfterReleaseAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>();
			using (padlock.Lock("remove-me"))
			{
				AssertEqual(1, GetLockDictionaryCount(padlock), "Held key should have an active lock entry.");
			}

			AssertEqual(0, GetLockDictionaryCount(padlock), "Released key should be removed from the lock dictionary.");
			return Task.CompletedTask;
		}

		private static async Task CancellationDoesNotLeakKeysAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("cancel-cleanup");

			Task[] waiters = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
			{
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeout.CancelAfter(50);

				try
				{
					using (await padlock.LockAsync("cancel-cleanup", timeout.Token))
					{
					}
				}
				catch (OperationCanceledException)
				{
				}
			}, cancellationToken)).ToArray();

			await Task.WhenAll(waiters);
			holder.Dispose();
			await Task.Delay(50, cancellationToken);

			AssertEqual(0, GetLockDictionaryCount(padlock), "Canceled waiters should not leave lock dictionary entries.");
		}

		private static Task PoolSizeLimitAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			int poolSize = 5;
			Padlock<string> padlock = new Padlock<string>(poolSize: poolSize);
			for (int i = 0; i < 20; i++)
			{
				using (padlock.Lock("pool-key-" + i))
				{
				}
			}

			int poolCount = GetPoolCount(padlock);
			AssertTrue(poolCount <= poolSize, $"Pool count {poolCount} exceeds configured size {poolSize}.");

			return Task.CompletedTask;
		}

		private static Task PoolReusesEntriesAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>(poolSize: 10);
			for (int i = 0; i < 5; i++)
			{
				using (padlock.Lock("reuse-key-" + i))
				{
				}
			}

			int firstPoolCount = GetPoolCount(padlock);

			for (int i = 100; i < 105; i++)
			{
				using (padlock.Lock("reuse-key-" + i))
				{
				}
			}

			int secondPoolCount = GetPoolCount(padlock);
			AssertTrue(firstPoolCount > 0 || secondPoolCount > 0, "Pool did not retain entries after released locks.");

			return Task.CompletedTask;
		}

		private static async Task PoolReuseResetsStateAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 2, poolSize: 1);

			using (padlock.Lock("pooled-once"))
			{
			}

			AssertEqual(1, GetPoolCount(padlock), "Released entry should be available in the pool.");

			IDisposable first = padlock.Lock("pooled-twice");
			IDisposable second = await padlock.LockAsync("pooled-twice", cancellationToken);
			AssertTrue(padlock.IsLocked("pooled-twice"), "Reused entry should reset with the original maxCount capacity.");

			first.Dispose();
			AssertFalse(padlock.IsLocked("pooled-twice"), "Reused entry did not expose a slot after one holder released.");

			second.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Reused pooled entry leaked after release.");
			AssertTrue(GetPoolCount(padlock) <= 1, "Reused pooled entry exceeded pool capacity.");
		}

		private static Task PoolSizeZeroAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>(poolSize: 0);
			using (padlock.Lock("no-pool"))
			{
			}

			AssertEqual(0, GetPoolCount(padlock), "poolSize zero should prevent returned entries from being pooled.");
			return Task.CompletedTask;
		}

		private static async Task EntryRetainedUntilAllRefsReleasedAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>(maxCount: 2, poolSize: 4);
			IDisposable first = padlock.Lock("retained");
			IDisposable second = padlock.Lock("retained");
			AssertEqual(2, GetLockEntryRefCount(padlock, "retained"), "Two holders should produce two entry references.");

			first.Dispose();
			AssertEqual(1, GetLockDictionaryCount(padlock), "Entry should remain while another holder exists.");
			AssertEqual(1, GetLockEntryRefCount(padlock, "retained"), "Disposing one holder should decrement the ref count once.");
			AssertEqual(0, GetPoolCount(padlock), "Entry should not return to the pool before all references release.");

			bool thirdAcquired = false;
			Task third = Task.Run(async () =>
			{
				using (await padlock.LockAsync("retained", cancellationToken))
				{
					thirdAcquired = true;
				}
			}, cancellationToken);

			await third.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertTrue(thirdAcquired, "Freed maxCount slot should allow another waiter to acquire.");
			AssertEqual(1, GetLockDictionaryCount(padlock), "Entry should remain while the second original holder exists.");

			second.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Entry should be removed after the final holder releases.");
			AssertTrue(GetPoolCount(padlock) > 0, "Entry should return to the pool after the final release.");
		}

		private static async Task CanceledWaiterRefCountCleanupAsync(CancellationToken cancellationToken)
		{
			Padlock<string> padlock = new Padlock<string>();
			IDisposable holder = padlock.Lock("ref-cleanup");
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(100);

			Task waiter = Task.Run(async () =>
			{
				await AssertThrowsAsync<OperationCanceledException>(
					async () => { using (await padlock.LockAsync("ref-cleanup", timeout.Token)) { } },
					"Blocked waiter should cancel.");
			}, cancellationToken);

			await waiter.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
			AssertEqual(1, GetLockEntryRefCount(padlock, "ref-cleanup"), "Canceled waiter should decrement its entry reference.");

			holder.Dispose();
			AssertEqual(0, GetLockDictionaryCount(padlock), "Canceled waiter ref-count cleanup should allow final removal.");
		}

		private static Task DoubleDisposeAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			Padlock<string> padlock = new Padlock<string>();
			IDisposable handle = padlock.Lock("double-dispose");
			handle.Dispose();
			handle.Dispose();

			using (padlock.Lock("double-dispose"))
			{
			}

			AssertEqual(0, GetLockDictionaryCount(padlock), "Double dispose should not corrupt lock dictionary cleanup.");
			return Task.CompletedTask;
		}

		private static int GetLockDictionaryCount<T>(Padlock<T> padlock)
			where T : notnull
		{
			FieldInfo? field = typeof(Padlock<T>).GetField("_locks", BindingFlags.NonPublic | BindingFlags.Instance);
			IDictionary dictionary = field?.GetValue(padlock) as IDictionary
				?? throw new InvalidOperationException("Could not read Padlock lock dictionary.");

			return dictionary.Count;
		}

		private static int GetPoolCount<T>(Padlock<T> padlock)
			where T : notnull
		{
			FieldInfo? field = typeof(Padlock<T>).GetField("_pool", BindingFlags.NonPublic | BindingFlags.Instance);
			ICollection pool = field?.GetValue(padlock) as ICollection
				?? throw new InvalidOperationException("Could not read Padlock pool.");

			return pool.Count;
		}

		private static int GetLockEntryRefCount<T>(Padlock<T> padlock, T key)
			where T : notnull
		{
			FieldInfo? locksField = typeof(Padlock<T>).GetField("_locks", BindingFlags.NonPublic | BindingFlags.Instance);
			IDictionary dictionary = locksField?.GetValue(padlock) as IDictionary
				?? throw new InvalidOperationException("Could not read Padlock lock dictionary.");

			object? entry = dictionary[key];
			if (entry == null)
			{
				throw new InvalidOperationException("No lock entry exists for key " + key + ".");
			}

			FieldInfo? refCountField = entry.GetType().GetField("RefCount", BindingFlags.Public | BindingFlags.Instance);
			return (int)(refCountField?.GetValue(entry) ?? throw new InvalidOperationException("Could not read lock entry ref count."));
		}

		private static void RecordConcurrentEntry(ref int current, ref int maxObserved)
		{
			int now = Interlocked.Increment(ref current);
			int observed;
			do
			{
				observed = Volatile.Read(ref maxObserved);
				if (now <= observed)
				{
					return;
				}
			}
			while (Interlocked.CompareExchange(ref maxObserved, now, observed) != observed);
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
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
		}

		private static void AssertFalse(bool condition, string message)
		{
			if (condition)
			{
				throw new InvalidOperationException(message);
			}
		}

		private static void AssertThrows<TException>(Action action, string message)
			where TException : Exception
		{
			try
			{
				action();
			}
			catch (TException)
			{
				return;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, got {ex.GetType().Name}.", ex);
			}

			throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, but no exception was thrown.");
		}

		private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
			where TException : Exception
		{
			try
			{
				await action();
			}
			catch (TException)
			{
				return;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, got {ex.GetType().Name}.", ex);
			}

			throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, but no exception was thrown.");
		}

		private sealed record CustomKey(string Name, int Id);
	}
}
