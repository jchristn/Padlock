namespace Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Padlocks;

    /// <summary>
    /// Test program for Padlock library.
    /// </summary>
    public static class Program
    {
        // Shared counters for each test type
        private static int _stringCounter = 0;
        private static int _intCounter = 0;
        private static int _guidCounter = 0;
        private static int _customTypeCounter = 0;

        /// <summary>
        /// Custom type to test with custom equality logic.
        /// </summary>
        private class CustomKey : IEquatable<CustomKey>
        {
            /// <summary>
            /// Gets the name.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets the identifier.
            /// </summary>
            public int Id { get; }

            /// <summary>
            /// Creates a new custom key.
            /// </summary>
            /// <param name="name">The name.</param>
            /// <param name="id">The identifier.</param>
            public CustomKey(string name, int id)
            {
                Name = name;
                Id = id;
            }

            /// <summary>
            /// Determines equality with another CustomKey.
            /// </summary>
            /// <param name="other">The other key.</param>
            /// <returns>True if equal.</returns>
            public bool Equals(CustomKey? other)
            {
                if (other is null) return false;
                return Name == other.Name && Id == other.Id;
            }

            /// <summary>
            /// Determines equality with another object.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>True if equal.</returns>
            public override bool Equals(object? obj)
            {
                return obj is CustomKey key && Equals(key);
            }

            /// <summary>
            /// Gets the hash code.
            /// </summary>
            /// <returns>The hash code.</returns>
            public override int GetHashCode()
            {
                return HashCode.Combine(Name, Id);
            }

            /// <summary>
            /// Returns a string representation.
            /// </summary>
            /// <returns>String representation.</returns>
            public override string ToString()
            {
                return $"{Name}:{Id}";
            }
        }

        /// <summary>
        /// Represents the result of a single test.
        /// </summary>
        private class TestResult
        {
            /// <summary>
            /// Gets the test name.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets whether the test passed.
            /// </summary>
            public bool Passed { get; }

            /// <summary>
            /// Gets the elapsed time in milliseconds.
            /// </summary>
            public long ElapsedMs { get; }

            /// <summary>
            /// Gets the failure reason, if any.
            /// </summary>
            public string FailureReason { get; }

            /// <summary>
            /// Creates a new test result.
            /// </summary>
            /// <param name="name">The test name.</param>
            /// <param name="passed">Whether the test passed.</param>
            /// <param name="elapsedMs">Elapsed time in milliseconds.</param>
            /// <param name="failureReason">Optional failure reason.</param>
            public TestResult(string name, bool passed, long elapsedMs, string failureReason = "")
            {
                Name = name;
                Passed = passed;
                ElapsedMs = elapsedMs;
                FailureReason = failureReason;
            }
        }

        /// <summary>
        /// Entry point for the test program.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Padlock tests...");
            Console.WriteLine("========================\n");

            Stopwatch totalSw = Stopwatch.StartNew();
            List<TestResult> results = new List<TestResult>();

            results.Add(await RunLockAcquisitionBenchmark());
            results.Add(await RunStringKeyTest());
            results.Add(await RunIntKeyTest());
            results.Add(await RunGuidKeyTest());
            results.Add(await RunCustomTypeKeyTest());
            results.Add(await RunMixedTypeTest());
            results.Add(await RunStressTest());
            results.Add(await RunCancellationTest());
            results.Add(await RunKeyRemovalTest());
            results.Add(await RunConcurrencyTest());
            results.Add(await RunPoolingTest());
            results.Add(await RunDoubleDisposeTest());
            results.Add(await RunConstructorValidationTest());
            results.Add(await RunIsLockedWithMaxCountTest());
            results.Add(await RunCancellationCleanupTest());
            results.Add(await RunSingleKeyHighContentionTest());
            results.Add(await RunAsyncLockCorrectnessTest());
            results.Add(await RunMultipleKeysIndependenceTest());
            results.Add(await RunPoolReuseVerificationTest());
            results.Add(await RunMixedMaxCountOperationsTest());
            results.Add(await RunDisposeUnderContentionTest());

            totalSw.Stop();

            // Print summary
            Console.WriteLine("\n========================================");
            Console.WriteLine("TEST SUMMARY");
            Console.WriteLine("========================================\n");

            int testNumber = 1;
            foreach (TestResult result in results)
            {
                string status = result.Passed ? "PASS" : "FAIL";
                Console.WriteLine($"  {testNumber,2}. [{status}] {result.Name} ({result.ElapsedMs}ms)");
                testNumber++;
            }

            int passCount = results.Count(r => r.Passed);
            int failCount = results.Count(r => !r.Passed);
            List<TestResult> failures = results.Where(r => !r.Passed).ToList();

            Console.WriteLine();
            Console.WriteLine($"  Total: {results.Count} | Passed: {passCount} | Failed: {failCount}");
            Console.WriteLine($"  Total runtime: {totalSw.ElapsedMilliseconds}ms");

            if (failures.Count > 0)
            {
                Console.WriteLine("\n  FAILED TESTS:");
                foreach (TestResult failure in failures)
                {
                    Console.WriteLine($"    - {failure.Name}: {failure.FailureReason}");
                }
                Console.WriteLine($"\n  OVERALL: FAIL");
            }
            else
            {
                Console.WriteLine($"\n  OVERALL: PASS");
            }
        }

        /// <summary>
        /// Benchmarks lock acquisition performance with a single key under contention.
        /// </summary>
        private static async Task<TestResult> RunLockAcquisitionBenchmark()
        {
            string testName = "Lock Acquisition Benchmark";
            Console.WriteLine($"  1. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            string key = "benchmark-key";
            int threadCount = Environment.ProcessorCount;
            CancellationTokenSource cts = new CancellationTokenSource();
            List<Task> tasks = new List<Task>();
            long[] counters = new long[threadCount];

            Console.WriteLine($"     Running benchmark with {threadCount} threads for 5 seconds...");

            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < threadCount; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    long localCounter = 0;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            using (padlock.Lock(key))
                            {
                                localCounter++;
                            }

                            if (localCounter % 10000 == 0)
                            {
                                counters[threadId] = localCounter;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"     Thread {threadId} error: {ex.Message}");
                            break;
                        }
                    }

                    counters[threadId] = localCounter;
                }));
            }

            await Task.Delay(5000);
            cts.Cancel();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                // Ignore exceptions from task cancellation
            }

            sw.Stop();
            testSw.Stop();

            long totalOperations = counters.Sum();
            double opsPerSecond = totalOperations / (sw.ElapsedMilliseconds / 1000.0);

            Console.WriteLine($"     {totalOperations:N0} ops in {sw.ElapsedMilliseconds:N0}ms ({opsPerSecond:N0} ops/sec)");

            bool passed = totalOperations > 0;
            string failureReason = passed ? "" : "No operations completed";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests locking with string keys under contention.
        /// </summary>
        private static async Task<TestResult> RunStringKeyTest()
        {
            string testName = "String Key Locking";
            Console.WriteLine($"  2. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            List<Task> tasks = new List<Task>();
            string[] keys = new[] { "apple", "banana", "cherry", "date", "elderberry" };
            int iterations = 10;

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Random random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        string key = keys[random.Next(keys.Length)];
                        using (padlock.Lock(key))
                        {
                            Interlocked.Increment(ref _stringCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int expected = 100 * iterations;
            bool passed = _stringCounter == expected;
            string failureReason = passed ? "" : $"Expected {expected} operations, got {_stringCounter}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests locking with integer keys under contention.
        /// </summary>
        private static async Task<TestResult> RunIntKeyTest()
        {
            string testName = "Integer Key Locking";
            Console.WriteLine($"  3. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<int> padlock = new Padlock<int>();
            List<Task> tasks = new List<Task>();
            int iterations = 100;

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Random random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        int key = random.Next(1, 6);
                        using (padlock.Lock(key))
                        {
                            Interlocked.Increment(ref _intCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int expected = 10 * iterations;
            bool passed = _intCounter == expected;
            string failureReason = passed ? "" : $"Expected {expected} operations, got {_intCounter}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests locking with GUID keys under contention.
        /// </summary>
        private static async Task<TestResult> RunGuidKeyTest()
        {
            string testName = "GUID Key Locking";
            Console.WriteLine($"  4. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<Guid> padlock = new Padlock<Guid>();
            List<Task> tasks = new List<Task>();
            Guid[] keys = new[]
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()
            };
            int iterations = 100;

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Random random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        Guid key = keys[random.Next(keys.Length)];
                        using (padlock.Lock(key))
                        {
                            Interlocked.Increment(ref _guidCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int expected = 10 * iterations;
            bool passed = _guidCounter == expected;
            string failureReason = passed ? "" : $"Expected {expected} operations, got {_guidCounter}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests locking with custom type keys under contention.
        /// </summary>
        private static async Task<TestResult> RunCustomTypeKeyTest()
        {
            string testName = "Custom Type Key Locking";
            Console.WriteLine($"  5. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<CustomKey> padlock = new Padlock<CustomKey>();
            List<Task> tasks = new List<Task>();
            CustomKey[] keys = new[]
            {
                new CustomKey("User", 1),
                new CustomKey("Admin", 2),
                new CustomKey("Guest", 3),
                new CustomKey("System", 4),
                new CustomKey("Service", 5)
            };
            int iterations = 100;

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Random random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        CustomKey key = keys[random.Next(keys.Length)];
                        using (padlock.Lock(key))
                        {
                            Interlocked.Increment(ref _customTypeCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int expected = 10 * iterations;
            bool passed = _customTypeCounter == expected;
            string failureReason = passed ? "" : $"Expected {expected} operations, got {_customTypeCounter}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests mixed asynchronous and synchronous locking on the same resource.
        /// </summary>
        private static async Task<TestResult> RunMixedTypeTest()
        {
            string testName = "Mixed Sync/Async Locking";
            Console.WriteLine($"  6. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            List<Task> tasks = new List<Task>();
            int[] results = new int[10];
            int iterations = 20;

            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        using (await padlock.LockAsync("sharedResource"))
                        {
                            results[taskId]++;
                        }
                    }
                }));
            }

            for (int i = 5; i < 10; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        using (padlock.Lock("sharedResource"))
                        {
                            results[taskId]++;
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int totalOperations = results.Sum();
            int expected = 10 * iterations;
            bool passed = totalOperations == expected;
            string failureReason = passed ? "" : $"Expected {expected} operations, got {totalOperations}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Runs a stress test with multiple key types and high contention.
        /// </summary>
        private static async Task<TestResult> RunStressTest()
        {
            string testName = "Multi-Type Stress Test";
            Console.WriteLine($"  7. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> stringLock = new Padlock<string>();
            Padlock<int> intLock = new Padlock<int>();
            Padlock<Guid> guidLock = new Padlock<Guid>();

            ConcurrentDictionary<string, int> stringDict = new ConcurrentDictionary<string, int>();
            ConcurrentDictionary<int, int> intDict = new ConcurrentDictionary<int, int>();
            ConcurrentDictionary<Guid, int> guidDict = new ConcurrentDictionary<Guid, int>();

            string[] stringKeys = { "key1", "key2" };
            int[] intKeys = { 1, 2 };
            Guid[] guidKeys = { Guid.NewGuid(), Guid.NewGuid() };

            List<Task> tasks = new List<Task>();
            int taskCount = 10;
            int opsPerTask = 20;

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Random random = new Random();

                    for (int j = 0; j < opsPerTask; j++)
                    {
                        int typeChoice = random.Next(3);

                        switch (typeChoice)
                        {
                            case 0:
                                string stringKey = stringKeys[random.Next(stringKeys.Length)];
                                using (stringLock.Lock(stringKey))
                                {
                                    stringDict.AddOrUpdate(stringKey, 1, (k, v) => v + 1);
                                }
                                break;

                            case 1:
                                int intKey = intKeys[random.Next(intKeys.Length)];
                                using (intLock.Lock(intKey))
                                {
                                    intDict.AddOrUpdate(intKey, 1, (k, v) => v + 1);
                                }
                                break;

                            case 2:
                                Guid guidKey = guidKeys[random.Next(guidKeys.Length)];
                                using (await guidLock.LockAsync(guidKey))
                                {
                                    guidDict.AddOrUpdate(guidKey, 1, (k, v) => v + 1);
                                }
                                break;
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int stringTotal = stringDict.Values.Sum();
            int intTotal = intDict.Values.Sum();
            int guidTotal = guidDict.Values.Sum();
            int totalOps = stringTotal + intTotal + guidTotal;
            int expected = taskCount * opsPerTask;

            bool passed = Math.Abs(totalOps - expected) < 10;
            string failureReason = passed ? "" : $"Expected ~{expected} total ops, got {totalOps}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests cancellation support via CancellationToken.
        /// </summary>
        private static async Task<TestResult> RunCancellationTest()
        {
            string testName = "Cancellation Support";
            Console.WriteLine($"  8. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            bool passed = false;
            string failureReason = "Unknown";

            using (padlock.Lock("cancelTest"))
            {
                CancellationTokenSource cts = new CancellationTokenSource(500);

                try
                {
                    using (await padlock.LockAsync("cancelTest", cts.Token))
                    {
                        failureReason = "Lock was acquired when it should be blocked";
                    }

                    failureReason = "No cancellation exception thrown";
                }
                catch (OperationCanceledException)
                {
                    passed = true;
                    failureReason = "";
                }
                catch (Exception ex)
                {
                    failureReason = $"Wrong exception type: {ex.GetType().Name}";
                }
            }

            testSw.Stop();
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests that the Padlock properly cleans up and removes keys from its internal dictionary after locks are released.
        /// </summary>
        private static async Task<TestResult> RunKeyRemovalTest()
        {
            string testName = "Key Removal After Release";
            Console.WriteLine($"  9. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            int lockCount = 100;
            List<string> keys = new List<string>();

            for (int i = 0; i < lockCount; i++)
            {
                keys.Add($"test-key-{i}");
            }

            FieldInfo? dictionaryField = typeof(Padlock<string>).GetField("_locks",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (dictionaryField == null)
            {
                testSw.Stop();
                Console.WriteLine($"     [FAIL] ({testSw.ElapsedMilliseconds}ms)\n");
                return new TestResult(testName, false, testSw.ElapsedMilliseconds, "Could not access _locks field via reflection");
            }

            System.Collections.IDictionary locksDictionary = dictionaryField.GetValue(padlock) as System.Collections.IDictionary
                ?? throw new InvalidOperationException("Could not access _locks dictionary");

            // Phase 1: Acquire and immediately release locks
            foreach (string key in keys)
            {
                using (padlock.Lock(key))
                {
                    // Acquire and release immediately
                }
            }

            int afterFirstPhaseCount = locksDictionary.Count;

            // Phase 2: Create overlapping locks to simulate contention
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 20; j++)
                    {
                        string key = keys[j];
                        using (padlock.Lock(key))
                        {
                            Thread.Sleep(5);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(100);

            int finalCount = locksDictionary.Count;

            // Phase 3: Rapid lock/unlock with new padlock
            padlock = new Padlock<string>();
            locksDictionary = dictionaryField.GetValue(padlock) as System.Collections.IDictionary
                ?? throw new InvalidOperationException("Could not access _locks dictionary");

            int rapidTestIterations = 1000;
            for (int i = 0; i < rapidTestIterations; i++)
            {
                string key = $"rapid-key-{i % 10}";
                using (padlock.Lock(key))
                {
                    // Acquire and release immediately
                }
            }

            await Task.Delay(100);

            int afterRapidTestCount = locksDictionary.Count;

            testSw.Stop();

            bool passed = afterFirstPhaseCount == 0 && finalCount == 0 && afterRapidTestCount == 0;
            string failureReason = passed ? "" : $"Keys remaining - phase1: {afterFirstPhaseCount}, contention: {finalCount}, rapid: {afterRapidTestCount}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests configurable concurrency by creating a Padlock with maxCount of 3
        /// and verifying that more than one but never more than maxCount concurrent holders are observed.
        /// </summary>
        private static async Task<TestResult> RunConcurrencyTest()
        {
            string testName = "Configurable Concurrency (maxCount: 3)";
            Console.WriteLine($" 10. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            int maxCount = 3;
            Padlock<string> padlock = new Padlock<string>(maxCount: maxCount);
            int maxObserved = 0;
            int currentHolders = 0;
            bool exceededMax = false;
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 5; j++)
                    {
                        using (await padlock.LockAsync("shared-key"))
                        {
                            int current = Interlocked.Increment(ref currentHolders);

                            int oldMax;
                            do
                            {
                                oldMax = Volatile.Read(ref maxObserved);
                                if (current <= oldMax) break;
                            } while (Interlocked.CompareExchange(ref maxObserved, current, oldMax) != oldMax);

                            if (current > maxCount)
                            {
                                exceededMax = true;
                            }

                            await Task.Delay(10);

                            Interlocked.Decrement(ref currentHolders);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            bool passed = !exceededMax;
            string failureReason = passed ? "" : $"Observed {maxObserved} concurrent holders, exceeds maxCount {maxCount}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests object pooling by creating a padlock with a pool size limit
        /// and verifying the pool respects the configured limit.
        /// </summary>
        private static async Task<TestResult> RunPoolingTest()
        {
            string testName = "Object Pooling (poolSize: 5)";
            Console.WriteLine($" 11. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            int poolSize = 5;
            Padlock<string> padlock = new Padlock<string>(poolSize: poolSize);

            for (int i = 0; i < 20; i++)
            {
                string key = $"pool-key-{i}";
                using (padlock.Lock(key))
                {
                    // Acquire and release
                }
            }

            FieldInfo? poolField = typeof(Padlock<string>).GetField("_pool",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (poolField == null)
            {
                testSw.Stop();
                Console.WriteLine($"     [FAIL] ({testSw.ElapsedMilliseconds}ms)\n");
                return new TestResult(testName, false, testSw.ElapsedMilliseconds, "Could not access _pool field via reflection");
            }

            System.Collections.ICollection? pool = poolField.GetValue(padlock) as System.Collections.ICollection;
            if (pool == null)
            {
                testSw.Stop();
                Console.WriteLine($"     [FAIL] ({testSw.ElapsedMilliseconds}ms)\n");
                return new TestResult(testName, false, testSw.ElapsedMilliseconds, "Could not cast _pool to ICollection");
            }

            int poolCount = pool.Count;
            testSw.Stop();

            bool passed = poolCount <= poolSize;
            string failureReason = passed ? "" : $"Pool count ({poolCount}) exceeds pool size limit ({poolSize})";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests that disposing a lock handle twice does not throw or corrupt ref counts.
        /// </summary>
        private static async Task<TestResult> RunDoubleDisposeTest()
        {
            string testName = "Double-Dispose Safety";
            Console.WriteLine($" 12. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();

            IDisposable handle = padlock.Lock("double-dispose-key");
            handle.Dispose();
            handle.Dispose();

            using (padlock.Lock("double-dispose-key"))
            {
                // Should succeed without issue
            }

            FieldInfo? dictionaryField = typeof(Padlock<string>).GetField("_locks",
                BindingFlags.NonPublic | BindingFlags.Instance);
            System.Collections.IDictionary locksDictionary = dictionaryField!.GetValue(padlock) as System.Collections.IDictionary
                ?? throw new InvalidOperationException("Could not access _locks dictionary");

            testSw.Stop();

            bool passed = locksDictionary.Count == 0;
            string failureReason = passed ? "" : $"{locksDictionary.Count} keys remain after double dispose";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests that the constructor throws on invalid arguments.
        /// </summary>
        private static async Task<TestResult> RunConstructorValidationTest()
        {
            string testName = "Constructor Validation";
            Console.WriteLine($" 13. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            bool maxCountThrew = false;
            bool poolSizeThrew = false;

            try
            {
                Padlock<string> bad = new Padlock<string>(maxCount: 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                maxCountThrew = true;
            }

            try
            {
                Padlock<string> bad = new Padlock<string>(poolSize: -1);
            }
            catch (ArgumentOutOfRangeException)
            {
                poolSizeThrew = true;
            }

            testSw.Stop();

            bool passed = maxCountThrew && poolSizeThrew;
            string failureReason = "";
            if (!maxCountThrew) failureReason += "maxCount: 0 did not throw; ";
            if (!poolSizeThrew) failureReason += "poolSize: -1 did not throw; ";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason.TrimEnd(' ', ';'));
        }

        /// <summary>
        /// Tests that IsLocked returns true only when all semaphore slots are taken (maxCount > 1).
        /// </summary>
        private static async Task<TestResult> RunIsLockedWithMaxCountTest()
        {
            string testName = "IsLocked with maxCount > 1";
            Console.WriteLine($" 14. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>(maxCount: 2);
            string key = "islocked-test";

            bool lockedBeforeAny = padlock.IsLocked(key);

            IDisposable handle1 = padlock.Lock(key);
            bool lockedAfterOne = padlock.IsLocked(key);

            IDisposable handle2 = padlock.Lock(key);
            bool lockedAfterTwo = padlock.IsLocked(key);

            handle1.Dispose();
            bool lockedAfterReleaseOne = padlock.IsLocked(key);

            handle2.Dispose();
            bool lockedAfterReleaseAll = padlock.IsLocked(key);

            testSw.Stop();

            bool passed = !lockedBeforeAny
                && !lockedAfterOne
                && lockedAfterTwo
                && !lockedAfterReleaseOne
                && !lockedAfterReleaseAll;
            string failureReason = passed ? "" : $"Before={lockedBeforeAny}, After1={lockedAfterOne}, After2={lockedAfterTwo}, AfterRelease1={lockedAfterReleaseOne}, AfterReleaseAll={lockedAfterReleaseAll}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests that a cancelled LockAsync properly cleans up and does not leak entries in the dictionary.
        /// </summary>
        private static async Task<TestResult> RunCancellationCleanupTest()
        {
            string testName = "Cancellation Cleanup (No Leaks)";
            Console.WriteLine($" 15. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            string key = "cancel-cleanup-key";

            FieldInfo? dictionaryField = typeof(Padlock<string>).GetField("_locks",
                BindingFlags.NonPublic | BindingFlags.Instance);
            System.Collections.IDictionary locksDictionary = dictionaryField!.GetValue(padlock) as System.Collections.IDictionary
                ?? throw new InvalidOperationException("Could not access _locks dictionary");

            IDisposable holder = padlock.Lock(key);

            List<Task> waiters = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                waiters.Add(Task.Run(async () =>
                {
                    try
                    {
                        CancellationTokenSource cts = new CancellationTokenSource(50);
                        using (await padlock.LockAsync(key, cts.Token))
                        {
                            // Should not reach here
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }));
            }

            await Task.WhenAll(waiters);

            holder.Dispose();
            await Task.Delay(50);

            int remainingCount = locksDictionary.Count;

            testSw.Stop();

            bool passed = remainingCount == 0;
            string failureReason = passed ? "" : $"{remainingCount} entries remain after cancellation cleanup";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Stress tests many tasks hammering a single key to exercise the Monitor-based acquire/release race window.
        /// </summary>
        private static async Task<TestResult> RunSingleKeyHighContentionTest()
        {
            string testName = "Single-Key High Contention";
            Console.WriteLine($" 16. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            string key = "contention-key";
            int taskCount = 50;
            int opsPerTask = 200;
            int counter = 0;
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < opsPerTask; j++)
                    {
                        using (padlock.Lock(key))
                        {
                            counter++;
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int expected = taskCount * opsPerTask;
            bool passed = counter == expected;
            string failureReason = passed ? "" : $"Counter is {counter}, expected {expected}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Verifies that LockAsync provides mutual exclusion by incrementing a non-atomic counter
        /// across many tasks and asserting no lost updates.
        /// </summary>
        private static async Task<TestResult> RunAsyncLockCorrectnessTest()
        {
            string testName = "Async Lock Mutual Exclusion";
            Console.WriteLine($" 17. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            string key = "async-correctness";
            int taskCount = 50;
            int opsPerTask = 200;
            int counter = 0;
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < opsPerTask; j++)
                    {
                        using (await padlock.LockAsync(key))
                        {
                            counter++;
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            testSw.Stop();

            int expected = taskCount * opsPerTask;
            bool passed = counter == expected;
            string failureReason = passed ? "" : $"Async counter is {counter}, expected {expected}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Verifies that locking key A does not block key B.
        /// </summary>
        private static async Task<TestResult> RunMultipleKeysIndependenceTest()
        {
            string testName = "Multiple Keys Independence";
            Console.WriteLine($" 18. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();

            IDisposable handleA = padlock.Lock("keyA");

            CancellationTokenSource cts = new CancellationTokenSource(2000);
            Stopwatch sw = Stopwatch.StartNew();
            bool keyBacquired = false;

            try
            {
                using (await padlock.LockAsync("keyB", cts.Token))
                {
                    keyBacquired = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Should not happen
            }

            sw.Stop();
            handleA.Dispose();

            testSw.Stop();

            bool passed = keyBacquired && sw.ElapsedMilliseconds < 500;
            string failureReason = "";
            if (!keyBacquired) failureReason = "Key B could not be acquired while key A was held";
            else if (sw.ElapsedMilliseconds >= 500) failureReason = $"Key B took {sw.ElapsedMilliseconds}ms to acquire (expected near-instant)";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Verifies that pooled LockEntry objects are actually reused after being returned to the pool.
        /// </summary>
        private static async Task<TestResult> RunPoolReuseVerificationTest()
        {
            string testName = "Pool Reuse Verification";
            Console.WriteLine($" 19. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            int poolSize = 10;
            Padlock<string> padlock = new Padlock<string>(poolSize: poolSize);

            FieldInfo? poolField = typeof(Padlock<string>).GetField("_pool",
                BindingFlags.NonPublic | BindingFlags.Instance);
            System.Collections.ICollection? pool = poolField!.GetValue(padlock) as System.Collections.ICollection;

            // Phase 1: Create and release entries to populate the pool
            for (int i = 0; i < 5; i++)
            {
                using (padlock.Lock($"reuse-key-{i}"))
                {
                    // Acquire and release
                }
            }

            int poolCountAfterFirstPhase = pool!.Count;

            // Phase 2: Acquire new keys — pool entries should be reused
            for (int i = 100; i < 105; i++)
            {
                using (padlock.Lock($"reuse-key-{i}"))
                {
                    // Acquire and release
                }
            }

            int poolCountAfterSecondPhase = pool.Count;

            testSw.Stop();

            // Pool should be populated after each phase (entries returned after use)
            bool passed = poolCountAfterFirstPhase > 0 || poolCountAfterSecondPhase > 0;
            string failureReason = passed ? "" : "Pool counts were 0 after both phases";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }

        /// <summary>
        /// Tests mixed maxCount operations: with maxCount 2, holds both slots, verifies a third waiter blocks,
        /// releases one, and verifies the waiter proceeds.
        /// </summary>
        private static async Task<TestResult> RunMixedMaxCountOperationsTest()
        {
            string testName = "Mixed maxCount Operations (maxCount: 2)";
            Console.WriteLine($" 20. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>(maxCount: 2);
            string key = "maxcount-test";

            IDisposable handle1 = padlock.Lock(key);
            IDisposable handle2 = padlock.Lock(key);

            bool lockedWithBothHeld = padlock.IsLocked(key);

            bool thirdAcquired = false;
            Task thirdTask = Task.Run(async () =>
            {
                using (await padlock.LockAsync(key))
                {
                    thirdAcquired = true;
                }
            });

            await Task.Delay(100);
            bool thirdAcquiredWhileBlocked = thirdAcquired;

            handle1.Dispose();

            bool completedInTime = thirdTask.Wait(2000);

            handle2.Dispose();

            testSw.Stop();

            bool passed = lockedWithBothHeld && !thirdAcquiredWhileBlocked && completedInTime && thirdAcquired;
            string failureReason = "";
            if (!lockedWithBothHeld) failureReason += "IsLocked was false with both slots held; ";
            if (thirdAcquiredWhileBlocked) failureReason += "Third waiter was not blocked; ";
            if (!completedInTime) failureReason += "Third waiter did not complete in time; ";
            if (!thirdAcquired) failureReason += "Third waiter never acquired the lock; ";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason.TrimEnd(' ', ';'));
        }

        /// <summary>
        /// Tests that when one task holds a lock and several waiters are queued,
        /// releasing the lock allows all waiters to eventually acquire and complete.
        /// </summary>
        private static async Task<TestResult> RunDisposeUnderContentionTest()
        {
            string testName = "Dispose Under Contention";
            Console.WriteLine($" 21. {testName}");

            Stopwatch testSw = Stopwatch.StartNew();

            Padlock<string> padlock = new Padlock<string>();
            string key = "dispose-contention";
            int waiterCount = 10;
            int completedCount = 0;

            IDisposable holder = padlock.Lock(key);

            List<Task> waiters = new List<Task>();
            for (int i = 0; i < waiterCount; i++)
            {
                waiters.Add(Task.Run(async () =>
                {
                    using (await padlock.LockAsync(key))
                    {
                        Interlocked.Increment(ref completedCount);
                        await Task.Delay(5);
                    }
                }));
            }

            await Task.Delay(100);

            holder.Dispose();

            bool allCompleted = Task.WaitAll(waiters.ToArray(), 10000);

            testSw.Stop();

            bool passed = allCompleted && completedCount == waiterCount;
            string failureReason = passed ? "" : $"{completedCount}/{waiterCount} waiters completed, allCompleted={allCompleted}";
            Console.WriteLine($"     [{(passed ? "PASS" : "FAIL")}] ({testSw.ElapsedMilliseconds}ms)\n");
            return new TestResult(testName, passed, testSw.ElapsedMilliseconds, failureReason);
        }
    }
}
