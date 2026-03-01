namespace Padlocks
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Padlock is a lightweight, high-performance library that provides key-based locking for multithreaded applications.
    /// It enables granular locking on specific resources identified by keys of any type, allowing for efficient concurrency control without unnecessary blocking.
    /// </summary>
    /// <typeparam name="T">Type of key.</typeparam>
    public class Padlock<T>
    {
        private readonly ConcurrentDictionary<T, LockEntry> _locks = new ConcurrentDictionary<T, LockEntry>();

        /// <summary>
        /// Padlock is a lightweight, high-performance library that provides key-based locking for multithreaded applications.
        /// It enables granular locking on specific resources identified by keys of any type, allowing for efficient concurrency control without unnecessary blocking.
        /// </summary>
        public Padlock()
        {
        }

        /// <summary>
        /// Acquires a lock for the specified key. Returns a disposable handle that will release the lock when disposed.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        public IDisposable Lock(T key)
        {
            var entry = AcquireEntry(key);
            entry.Semaphore.Wait();
            return new LockReleaser(entry, key, _locks);
        }

        /// <summary>
        /// Asynchronously acquires a lock for the specified key.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        public async Task<IDisposable> LockAsync(T key, CancellationToken cancellationToken = default)
        {
            var entry = AcquireEntry(key);
            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken);
            }
            catch
            {
                ReleaseEntry(entry, key, _locks);
                throw;
            }
            return new LockReleaser(entry, key, _locks);
        }

        /// <summary>
        /// Checks if a key is currently locked.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if locked, false otherwise.</returns>
        public bool IsLocked(T key)
        {
            if (_locks.TryGetValue(key, out var entry))
            {
                return entry.Semaphore.CurrentCount == 0;
            }
            return false; // No lock exists for this key
        }

        private LockEntry AcquireEntry(T key)
        {
            while (true)
            {
                var entry = _locks.GetOrAdd(key, _ => new LockEntry());
                int oldCount = Interlocked.Increment(ref entry.RefCount) - 1;
                if (oldCount >= 0)
                    return entry;

                // Entry is being removed (RefCount was negative sentinel), undo and retry
                Interlocked.Decrement(ref entry.RefCount);
            }
        }

        private static void ReleaseEntry(LockEntry entry, T key, ConcurrentDictionary<T, LockEntry> locks)
        {
            if (Interlocked.Decrement(ref entry.RefCount) == 0)
            {
                // Try to mark as removing by transitioning from 0 to MinValue
                if (Interlocked.CompareExchange(ref entry.RefCount, int.MinValue, 0) == 0)
                {
                    locks.TryRemove(key, out _);
                    entry.Semaphore.Dispose();
                }
            }
        }

        internal sealed class LockEntry
        {
            public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
            public int RefCount;
        }

        private sealed class LockReleaser : IDisposable
        {
            private readonly LockEntry _entry;
            private readonly T _key;
            private readonly ConcurrentDictionary<T, LockEntry> _lockDictionary;
            private int _disposed = 0;

            public LockReleaser(LockEntry entry, T key, ConcurrentDictionary<T, LockEntry> lockDictionary)
            {
                _entry = entry;
                _key = key;
                _lockDictionary = lockDictionary;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _entry.Semaphore.Release();
                    ReleaseEntry(_entry, _key, _lockDictionary);
                }
            }
        }
    }
}
