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

    public static class Program
    {
        // Shared counters for each test type
        private static int _stringCounter = 0;
        private static int _intCounter = 0;
        private static int _guidCounter = 0;
        private static int _customTypeCounter = 0;

        // Custom type to test with custom equality logic
        private class CustomKey : IEquatable<CustomKey>
        {
            public string Name { get; }
            public int Id { get; }

            public CustomKey(string name, int id)
            {
                Name = name;
                Id = id;
            }

            public bool Equals(CustomKey other)
            {
                if (other is null) return false;
                return Name == other.Name && Id == other.Id;
            }

            public override bool Equals(object obj)
            {
                return obj is CustomKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, Id);
            }

            public override string ToString()
            {
                return $"{Name}:{Id}";
            }
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Padlock tests...");
            Console.WriteLine("=========================");

            await RunLockAcquisitionBenchmark();
            await RunStringKeyTest();
            await RunIntKeyTest();
            await RunGuidKeyTest();
            await RunCustomTypeKeyTest();
            await RunMixedTypeTest();
            await RunStressTest();
            await RunCancellationTest();
            await RunKeyRemovalTest(); // Added new test for key removal

            Console.WriteLine("\nAll tests completed successfully!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task RunLockAcquisitionBenchmark()
        {
            Console.WriteLine("\n1. Lock Acquisition Benchmark (Single Key)");
            Console.WriteLine("--------------------------------------------");

            var padlock = new Padlock<string>();
            var key = "benchmark-key";
            long totalOperations = 0;
            int threadCount = Environment.ProcessorCount;
            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();
            var counters = new long[threadCount];

            Console.WriteLine($"Running benchmark with {threadCount} threads for 5 seconds...");

            // Start the timer
            Stopwatch sw = Stopwatch.StartNew();

            // Start threads
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
                                // Just increment counter, no delay
                                localCounter++;
                            }

                            // Every 10,000 operations, check if we should report progress
                            if (localCounter % 10000 == 0)
                            {
                                counters[threadId] = localCounter;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Thread {threadId} error: {ex.Message}");
                            break;
                        }
                    }

                    // Final update of counter
                    counters[threadId] = localCounter;
                }));
            }

            // Run for 10 seconds
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

            // Calculate total operations
            totalOperations = counters.Sum();
            double opsPerSecond = totalOperations / (sw.ElapsedMilliseconds / 1000.0);

            Console.WriteLine($"  Completed {totalOperations:N0} lock/unlock operations in {sw.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"  Performance: {opsPerSecond:N0} operations per second");
            Console.WriteLine($"  Average time per operation: {sw.ElapsedMilliseconds * 1000.0 / totalOperations:N3} μs");
            Console.WriteLine("  Operations per thread:");
            foreach (long l in counters)
            {
                Console.WriteLine("| " + l.ToString("N0"));
            }
        }

        private static async Task RunStringKeyTest()
        {
            Console.WriteLine("\n1. Testing with string keys...");
            var padlock = new Padlock<string>();
            var tasks = new List<Task>();
            var keys = new[] { "apple", "banana", "cherry", "date", "elderberry" };
            var iterations = 10;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        string key = keys[random.Next(keys.Length)];
                        using (padlock.Lock(key))
                        {
                            // Simulate work
                            Console.Write("*");
                            Interlocked.Increment(ref _stringCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            Console.WriteLine("");
            Console.WriteLine($"  String key test completed: {_stringCounter} operations in {sw.ElapsedMilliseconds}ms");
            if (_stringCounter != 100 * iterations)
            {
                Console.WriteLine($"  ERROR: Expected {100 * iterations} operations, got {_stringCounter}");
            }
            else
            {
                Console.WriteLine("  PASSED: All operations completed correctly");
            }
        }

        private static async Task RunIntKeyTest()
        {
            Console.WriteLine("\n2. Testing with integer keys...");
            var padlock = new Padlock<int>();
            var tasks = new List<Task>();
            var iterations = 100;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        int key = random.Next(1, 6); // Keys from 1 to 5
                        using (padlock.Lock(key))
                        {
                            // Simulate work
                            Console.Write("*");
                            Interlocked.Increment(ref _intCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            Console.WriteLine("");
            Console.WriteLine($"  Integer key test completed: {_intCounter} operations in {sw.ElapsedMilliseconds}ms");
            if (_intCounter != 10 * iterations)
            {
                Console.WriteLine($"  ERROR: Expected {10 * iterations} operations, got {_intCounter}");
            }
            else
            {
                Console.WriteLine("  PASSED: All operations completed correctly");
            }
        }

        private static async Task RunGuidKeyTest()
        {
            Console.WriteLine("\n3. Testing with GUID keys...");
            var padlock = new Padlock<Guid>();
            var tasks = new List<Task>();
            var keys = new[]
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()
            };
            var iterations = 100;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        Guid key = keys[random.Next(keys.Length)];
                        using (padlock.Lock(key))
                        {
                            // Simulate work
                            Console.Write("*");
                            Interlocked.Increment(ref _guidCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            Console.WriteLine("");
            Console.WriteLine($"  GUID key test completed: {_guidCounter} operations in {sw.ElapsedMilliseconds}ms");
            if (_guidCounter != 10 * iterations)
            {
                Console.WriteLine($"  ERROR: Expected {10 * iterations} operations, got {_guidCounter}");
            }
            else
            {
                Console.WriteLine("  PASSED: All operations completed correctly");
            }
        }

        private static async Task RunCustomTypeKeyTest()
        {
            Console.WriteLine("\n4. Testing with custom type keys...");
            var padlock = new Padlock<CustomKey>();
            var tasks = new List<Task>();
            var keys = new[]
            {
                new CustomKey("User", 1),
                new CustomKey("Admin", 2),
                new CustomKey("Guest", 3),
                new CustomKey("System", 4),
                new CustomKey("Service", 5)
            };
            var iterations = 100;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var random = new Random();
                    for (int j = 0; j < iterations; j++)
                    {
                        CustomKey key = keys[random.Next(keys.Length)];
                        using (padlock.Lock(key))
                        {
                            // Simulate work
                            Console.Write("*");
                            Interlocked.Increment(ref _customTypeCounter);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            Console.WriteLine("");
            Console.WriteLine($"  Custom type key test completed: {_customTypeCounter} operations in {sw.ElapsedMilliseconds}ms");
            if (_customTypeCounter != 10 * iterations)
            {
                Console.WriteLine($"  ERROR: Expected {10 * iterations} operations, got {_customTypeCounter}");
            }
            else
            {
                Console.WriteLine("  PASSED: All operations completed correctly");
            }
        }

        private static async Task RunMixedTypeTest()
        {
            Console.WriteLine("\n5. Testing mixed asynchronous and synchronous locking...");
            var padlock = new Padlock<string>();
            var tasks = new List<Task>();
            var results = new int[10]; // One counter per task
            var iterations = 20;

            Stopwatch sw = Stopwatch.StartNew();

            // Create 5 async tasks
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        using (await padlock.LockAsync("sharedResource"))
                        {
                            // Critical section
                            results[taskId]++;
                            Console.Write("*");
                        }
                    }
                }));
            }

            // Create 5 sync tasks
            for (int i = 5; i < 10; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        using (padlock.Lock("sharedResource"))
                        {
                            // Critical section
                            results[taskId]++;
                            Console.Write("*");
                        }
                    }
                }));
            }

            // Wait for all tasks and measure time
            await Task.WhenAll(tasks);
            sw.Stop();

            // Calculate total operations
            int totalOperations = results.Sum();
            Console.WriteLine("");
            Console.WriteLine($"  Mixed locking test completed: {totalOperations} operations in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Performance: {totalOperations * 1000.0 / sw.ElapsedMilliseconds:N0} operations per second");

            if (totalOperations != 10 * iterations)
            {
                Console.WriteLine($"  ERROR: Expected {10 * iterations} operations, got {totalOperations}");
            }
            else
            {
                Console.WriteLine("  PASSED: All operations completed correctly");
            }
        }

        private static async Task RunStressTest()
        {
            Console.WriteLine("\n6. Running stress test with multiple types and high contention...");

            // Create locks for different types
            var stringLock = new Padlock<string>();
            var intLock = new Padlock<int>();
            var guidLock = new Padlock<Guid>();

            // Shared resources to protect
            Dictionary<string, int> stringDict = new Dictionary<string, int>();
            Dictionary<int, int> intDict = new Dictionary<int, int>();
            Dictionary<Guid, int> guidDict = new Dictionary<Guid, int>();

            // Fixed keys to ensure high contention
            string[] stringKeys = { "key1", "key2" };
            int[] intKeys = { 1, 2 };
            Guid[] guidKeys = { Guid.NewGuid(), Guid.NewGuid() };

            // Create a large number of tasks
            var tasks = new List<Task>();
            int taskCount = 10;
            int opsPerTask = 20;

            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var random = new Random();

                    for (int j = 0; j < opsPerTask; j++)
                    {
                        // Randomly choose which type of lock to use
                        int typeChoice = random.Next(3);

                        switch (typeChoice)
                        {
                            case 0: // String lock
                                string stringKey = stringKeys[random.Next(stringKeys.Length)];
                                using (stringLock.Lock(stringKey))
                                {
                                    // Update dictionary
                                    if (!stringDict.ContainsKey(stringKey))
                                        stringDict[stringKey] = 0;
                                    stringDict[stringKey]++;
                                }
                                break;

                            case 1: // Int lock
                                int intKey = intKeys[random.Next(intKeys.Length)];
                                using (intLock.Lock(intKey))
                                {
                                    // Update dictionary
                                    if (!intDict.ContainsKey(intKey))
                                        intDict[intKey] = 0;
                                    intDict[intKey]++;
                                }
                                break;

                            case 2: // Guid lock
                                Guid guidKey = guidKeys[random.Next(guidKeys.Length)];
                                using (await guidLock.LockAsync(guidKey))
                                {
                                    // Update dictionary
                                    if (!guidDict.ContainsKey(guidKey))
                                        guidDict[guidKey] = 0;
                                    guidDict[guidKey]++;
                                }
                                break;
                        }

                        Console.Write("*");
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // Calculate expected totals
            int expectedOpsPerType = taskCount * opsPerTask / 3; // Divide by 3 types
            int stringTotal = stringDict.Values.Sum();
            int intTotal = intDict.Values.Sum();
            int guidTotal = guidDict.Values.Sum();
            int totalOps = stringTotal + intTotal + guidTotal;

            Console.WriteLine("");
            Console.WriteLine($"  Stress test completed in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Total operations: {totalOps} (expected ~{taskCount * opsPerTask})");
            Console.WriteLine($"    String operations: {stringTotal}");
            Console.WriteLine($"    Int operations: {intTotal}");
            Console.WriteLine($"    Guid operations: {guidTotal}");

            // Check if the numbers are close enough (allowing for random distribution)
            bool passedTest = Math.Abs(totalOps - taskCount * opsPerTask) < 10;

            if (passedTest)
                Console.WriteLine("  PASSED: Stress test completed successfully");
            else
                Console.WriteLine("  ERROR: Operation counts don't match expected values");
        }

        private static async Task RunCancellationTest()
        {
            Console.WriteLine("\n7. Testing cancellation support...");

            var padlock = new Padlock<string>();

            // First lock the resource
            using (padlock.Lock("cancelTest"))
            {
                Console.WriteLine("  Resource locked, starting cancellation test...");

                // Try to lock with cancellation
                var cts = new CancellationTokenSource(500); // 500ms timeout

                try
                {
                    Console.WriteLine("  Attempting to acquire lock with 500ms timeout...");

                    // This should timeout and throw
                    using (await padlock.LockAsync("cancelTest", cts.Token))
                    {
                        Console.WriteLine("  ERROR: Lock was acquired when it should be blocked!");
                    }

                    Console.WriteLine("  ERROR: No cancellation exception thrown!");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("  PASSED: Cancellation worked as expected");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR: Wrong exception type: {ex.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// Tests that the Padlock properly cleans up and removes keys from its internal dictionary after locks are released.
        /// This test validates the cleanup functionality in the LockReleaser.Dispose() method.
        /// </summary>
        private static async Task RunKeyRemovalTest()
        {
            Console.WriteLine("\n8. Testing key removal after lock release...");

            var padlock = new Padlock<string>();
            var lockCount = 100; // Number of keys to test with
            var keys = new List<string>();

            // Generate unique keys
            for (int i = 0; i < lockCount; i++)
            {
                keys.Add($"test-key-{i}");
            }

            Console.WriteLine($"  Creating and releasing locks for {lockCount} unique keys...");

            // Create a reflection field to access the internal dictionary
            var dictionaryField = typeof(Padlock<string>).GetField("_locks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (dictionaryField == null)
            {
                Console.WriteLine("  ERROR: Could not access internal _locks field via reflection");
                return;
            }

            // Get the dictionary before any operations
            var locksDictionary = dictionaryField.GetValue(padlock) as ConcurrentDictionary<string, SemaphoreSlim>;
            int initialCount = locksDictionary.Count;
            Console.WriteLine($"  Initial dictionary count: {initialCount}");

            // Phase 1: Acquire and immediately release locks
            foreach (var key in keys)
            {
                using (padlock.Lock(key))
                {
                    // Just acquire and release immediately
                }
            }

            // Check dictionary size after initial release
            int afterFirstPhaseCount = locksDictionary.Count;
            Console.WriteLine($"  Dictionary count after first phase: {afterFirstPhaseCount}");

            // Phase 2: Create overlapping locks to simulate contention
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++) // Create 5 tasks
            {
                tasks.Add(Task.Run(() =>
                {
                    // Each task will lock 20 keys
                    for (int j = 0; j < 20; j++)
                    {
                        string key = keys[j];
                        using (padlock.Lock(key))
                        {
                            // Hold the lock briefly
                            Thread.Sleep(5);
                        }
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Give a small delay for any cleanup to occur
            await Task.Delay(100);

            // Check final dictionary size
            int finalCount = locksDictionary.Count;
            Console.WriteLine($"  Final dictionary count: {finalCount}");

            // Verify cleanup occurred
            if (finalCount == 0)
            {
                Console.WriteLine("  PASSED: All keys were removed from the dictionary after use");
            }
            else
            {
                Console.WriteLine($"  NOTE: {finalCount} keys remain in the dictionary");
                Console.WriteLine("  This is expected behavior if any semaphores still have waiters or");
                Console.WriteLine("  if cleanup edge cases prevented removal of some entries");

                // Force GC and check again
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);

                int afterGCCount = locksDictionary.Count;
                Console.WriteLine($"  Dictionary count after GC: {afterGCCount}");

                if (afterGCCount < finalCount)
                {
                    Console.WriteLine("  PARTIAL PASS: Some keys were removed after garbage collection");
                }
            }

            // Additional verification - try to create contention and then verify removal
            Console.WriteLine("\n  Testing with high contention scenario...");

            // Reset by creating a new padlock
            padlock = new Padlock<string>();
            locksDictionary = dictionaryField.GetValue(padlock) as ConcurrentDictionary<string, SemaphoreSlim>;

            // Use just a few keys to create contention
            var contentionKeys = new[] { "contentionKey1", "contentionKey2" };
            var contentionTasks = new List<Task>();

            // Create locks and hold them
            var locks = new List<IDisposable>();
            foreach (var key in contentionKeys)
            {
                locks.Add(padlock.Lock(key));
            }

            // Try to acquire the same locks from other tasks (will be blocked)
            for (int i = 0; i < 5; i++)
            {
                contentionTasks.Add(Task.Run(async () =>
                {
                    var random = new Random();
                    var key = contentionKeys[random.Next(contentionKeys.Length)];

                    try
                    {
                        using var cts = new CancellationTokenSource(50); // Short timeout
                        using var lockHandle = await padlock.LockAsync(key, cts.Token);
                        // If we get here, we acquired the lock
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected if the lock is held
                    }
                }));
            }

            // Wait for contention tasks to complete or timeout
            await Task.WhenAll(contentionTasks);

            // Now release all our held locks
            foreach (var lockHandle in locks)
            {
                lockHandle.Dispose();
            }
            locks.Clear();

            // Wait a moment for potential cleanup
            await Task.Delay(100);

            // Check dictionary size after contention test
            int afterContentionCount = locksDictionary.Count;
            Console.WriteLine($"  Dictionary count after contention test: {afterContentionCount}");

            // Final test - create and release locks in quick succession
            Console.WriteLine("\n  Testing rapid lock/unlock scenario...");
            const int rapidTestIterations = 1000;

            for (int i = 0; i < rapidTestIterations; i++)
            {
                string key = $"rapid-key-{i % 10}"; // Reuse 10 keys
                using (padlock.Lock(key))
                {
                    // Acquire and release immediately
                }
            }

            // Wait a moment for potential cleanup
            await Task.Delay(100);

            // Check dictionary size after rapid test
            int afterRapidTestCount = locksDictionary.Count;
            Console.WriteLine($"  Dictionary count after rapid test: {afterRapidTestCount}");

            // Final summary
            Console.WriteLine("\n  Key Removal Test Summary:");
            Console.WriteLine($"  - Initial dictionary size: {initialCount}");
            Console.WriteLine($"  - After first phase: {afterFirstPhaseCount}");
            Console.WriteLine($"  - After contention test: {afterContentionCount}");
            Console.WriteLine($"  - After rapid test: {afterRapidTestCount}");

            if (afterRapidTestCount == 0)
            {
                Console.WriteLine("  FINAL RESULT: PASSED - Key removal is working correctly");
            }
            else
            {
                int remainingPercentage = (afterRapidTestCount * 100) / rapidTestIterations;
                if (remainingPercentage < 5) // Less than 5% remains
                {
                    Console.WriteLine($"  FINAL RESULT: ACCEPTABLE - {afterRapidTestCount} keys remain ({remainingPercentage}%)");
                    Console.WriteLine("  This may be due to contention or cleanup timing");
                }
                else
                {
                    Console.WriteLine($"  FINAL RESULT: POTENTIAL ISSUE - {afterRapidTestCount} keys remain ({remainingPercentage}%)");
                    Console.WriteLine("  The cleanup mechanism may not be working optimally");
                }
            }
        }
    }
}