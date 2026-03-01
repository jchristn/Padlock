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
        private readonly ConcurrentBag<LockEntry> _pool = new ConcurrentBag<LockEntry>();
        private readonly int _maxCount;
        private readonly int _poolSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="Padlock{T}"/> class with configurable concurrency and pooling.
        /// </summary>
        /// <param name="maxCount">Maximum number of concurrent holders for each key. Default is 1 (exclusive lock).</param>
        /// <param name="poolSize">Maximum number of pooled lock entries for reuse. Default is 20.</param>
        public Padlock(int maxCount = 1, int poolSize = 20)
        {
            if (maxCount < 1) throw new ArgumentOutOfRangeException(nameof(maxCount), "Must be at least 1.");
            if (poolSize < 0) throw new ArgumentOutOfRangeException(nameof(poolSize), "Must be non-negative.");
            _maxCount = maxCount;
            _poolSize = poolSize;
        }

        /// <summary>
        /// Acquires a lock for the specified key. Returns a disposable handle that will release the lock when disposed.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        public IDisposable Lock(T key)
        {
            LockEntry entry = AcquireEntry(key);
            entry.Semaphore.Wait();
            return new LockReleaser(this, entry, key);
        }

        /// <summary>
        /// Asynchronously acquires a lock for the specified key.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        public async ValueTask<IDisposable> LockAsync(T key, CancellationToken cancellationToken = default)
        {
            LockEntry entry = AcquireEntry(key);
            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken);
            }
            catch
            {
                ReleaseEntry(entry, key);
                throw;
            }
            return new LockReleaser(this, entry, key);
        }

        /// <summary>
        /// Checks if a key is currently locked.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if locked, false otherwise.</returns>
        public bool IsLocked(T key)
        {
            if (_locks.TryGetValue(key, out LockEntry entry))
            {
                return entry.Semaphore.CurrentCount == 0;
            }
            return false;
        }

        private LockEntry CreateOrTakeFromPool()
        {
            if (_pool.TryTake(out LockEntry entry))
            {
                entry.Reset(_maxCount);
                return entry;
            }
            return new LockEntry(_maxCount);
        }

        private LockEntry AcquireEntry(T key)
        {
            while (true)
            {
                LockEntry entry = _locks.GetOrAdd(key, _ => CreateOrTakeFromPool());
                Monitor.Enter(entry);
                if (!entry.IsRemoved
                    && _locks.TryGetValue(key, out LockEntry stored)
                    && ReferenceEquals(stored, entry))
                {
                    entry.RefCount++;
                    Monitor.Exit(entry);
                    return entry;
                }
                Monitor.Exit(entry);
            }
        }

        private void ReleaseEntry(LockEntry entry, T key)
        {
            Monitor.Enter(entry);
            entry.RefCount--;
            if (entry.RefCount == 0)
            {
                entry.IsRemoved = true;
                _locks.TryRemove(key, out _);
                Monitor.Exit(entry);
                ReturnToPoolOrDispose(entry);
                return;
            }
            Monitor.Exit(entry);
        }

        private void ReturnToPoolOrDispose(LockEntry entry)
        {
            if (_pool.Count < _poolSize)
            {
                _pool.Add(entry);
            }
            else
            {
                entry.Semaphore.Dispose();
            }
        }

        internal sealed class LockEntry
        {
            /// <summary>
            /// The semaphore used to control concurrent access.
            /// </summary>
            public SemaphoreSlim Semaphore;

            /// <summary>
            /// The number of active references to this entry.
            /// </summary>
            public int RefCount;

            /// <summary>
            /// Indicates whether this entry has been removed from the dictionary.
            /// </summary>
            public bool IsRemoved;

            /// <summary>
            /// Creates a new lock entry with the specified max concurrency count.
            /// </summary>
            /// <param name="maxCount">Maximum number of concurrent holders.</param>
            public LockEntry(int maxCount)
            {
                Semaphore = new SemaphoreSlim(maxCount, maxCount);
                RefCount = 0;
                IsRemoved = false;
            }

            /// <summary>
            /// Resets this entry for reuse from the pool.
            /// </summary>
            /// <param name="maxCount">Maximum number of concurrent holders.</param>
            public void Reset(int maxCount)
            {
                RefCount = 0;
                IsRemoved = false;

                // Create a fresh semaphore; do not dispose the old one here
                // because a thread with a stale reference from GetOrAdd may
                // still read this field before retrying in AcquireEntry.
                // The old SemaphoreSlim will be collected by the GC.
                Semaphore = new SemaphoreSlim(maxCount, maxCount);
            }
        }

        private sealed class LockReleaser : IDisposable
        {
            private readonly Padlock<T> _padlock;
            private readonly LockEntry _entry;
            private readonly T _key;
            private int _disposed;

            /// <summary>
            /// Creates a new lock releaser.
            /// </summary>
            /// <param name="padlock">The owning padlock instance.</param>
            /// <param name="entry">The lock entry to release.</param>
            /// <param name="key">The key associated with this lock.</param>
            public LockReleaser(Padlock<T> padlock, LockEntry entry, T key)
            {
                _padlock = padlock;
                _entry = entry;
                _key = key;
                _disposed = 0;
            }

            /// <summary>
            /// Releases the lock.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _entry.Semaphore.Release();
                    _padlock.ReleaseEntry(_entry, _key);
                }
            }
        }
    }
}
