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
        private readonly ConcurrentDictionary<T, SemaphoreSlim> _locks = new ConcurrentDictionary<T, SemaphoreSlim>();

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
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            return new LockReleaser(semaphore, key, _locks);
        }

        /// <summary>
        /// Asynchronously acquires a lock for the specified key.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        public async Task<IDisposable> LockAsync(T key, CancellationToken cancellationToken = default)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            return new LockReleaser(semaphore, key, _locks);
        }

        /// <summary>
        /// Checks if a key is currently locked.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if locked, false otherwise.</returns>
        public bool IsLocked(T key)
        {
            if (_locks.TryGetValue(key, out var semaphore))
            {
                return semaphore.CurrentCount == 0;
            }
            return false; // No lock exists for this key
        }

        private sealed class LockReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private readonly T _key;
            private readonly ConcurrentDictionary<T, SemaphoreSlim> _lockDictionary;
            private int _disposed = 0;

            public LockReleaser(SemaphoreSlim semaphore, T key, ConcurrentDictionary<T, SemaphoreSlim> lockDictionary)
            {
                _semaphore = semaphore;
                _key = key;
                _lockDictionary = lockDictionary;
            }

            public void Dispose()
            {
                // Only dispose once
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _semaphore.Release();

                    // Optional cleanup: if no one is waiting on this semaphore, remove it
                    if (_semaphore.CurrentCount == 1 && _semaphore.Wait(0))
                    {
                        // If we could immediately acquire the lock again, no one else is waiting
                        // Try to remove from dictionary (only if the entry matches our semaphore)
                        _lockDictionary.TryRemove(_key, out _);
                        _semaphore.Release(); 
                    }
                }
            }
        }
    }
}