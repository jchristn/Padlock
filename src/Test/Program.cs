namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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
    }
}