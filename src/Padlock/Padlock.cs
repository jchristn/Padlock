namespace Padlocks
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Padlock is a lightweight, high-performance library that provides key-based locking for multithreaded applications.
    /// It enables granular locking on specific resources identified by keys of any type, allowing for efficient concurrency control without unnecessary blocking.
    /// </summary>
    /// <remarks>
    /// Every instance emits metrics and spans through the <see cref="PadlockTelemetry.MeterName"/> meter and the
    /// <see cref="PadlockTelemetry.ActivitySourceName"/> activity source, labeled with <see cref="Name"/>. Emission is
    /// best-effort and costs a few flag checks when nothing subscribes. See TELEMETRY.md for the full catalog.
    /// </remarks>
    /// <typeparam name="T">Type of key.</typeparam>
    public class Padlock<T> : IPadlockObservable
    {
        private readonly ConcurrentDictionary<T, LockEntry> _Locks = new ConcurrentDictionary<T, LockEntry>();
        private readonly ConcurrentBag<LockEntry> _Pool = new ConcurrentBag<LockEntry>();
        private volatile int _MaxCount;
        private readonly int _PoolSize;
        private volatile string _Name = PadlockTelemetry.DefaultName;

        /// <summary>
        /// Initializes a new instance of the <see cref="Padlock{T}"/> class with configurable concurrency and pooling.
        /// </summary>
        /// <param name="maxCount">Maximum number of concurrent holders for each key. Default is 1 (exclusive lock).</param>
        /// <param name="poolSize">Maximum number of pooled lock entries for reuse. Default is 20.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxCount"/> is less than 1 or <paramref name="poolSize"/> is negative.</exception>
        public Padlock(int maxCount = 1, int poolSize = 20)
        {
            if (maxCount < 1) throw new ArgumentOutOfRangeException(nameof(maxCount), "Must be at least 1.");
            if (poolSize < 0) throw new ArgumentOutOfRangeException(nameof(poolSize), "Must be non-negative.");
            _MaxCount = maxCount;
            _PoolSize = poolSize;
            PadlockInstrumentation.Register(this);
        }

        /// <summary>
        /// Gets the maximum number of concurrent holders currently applied to newly created locks.
        /// </summary>
        public int MaxCount => _MaxCount;

        /// <summary>
        /// Gets or sets the name used as the <c>padlock.name</c> label on this instance's metrics and spans.
        /// Default is <c>"default"</c>. Use a small, fixed set of names (for example one per subsystem); never derive the
        /// name from a key, an id, or user input, because every distinct name creates new metric series. Instances that
        /// share a name are aggregated together. The name may be changed at any time and applies to subsequent operations.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the value is empty or whitespace.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Name), "Name cannot be null.");
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name cannot be empty or whitespace.", nameof(Name));
                _Name = value;
            }
        }

        string IPadlockObservable.TelemetryName => _Name;

        int IPadlockObservable.TelemetryMaxCount => _MaxCount;

        int IPadlockObservable.TelemetryActiveKeys => _Locks.Count;

        int IPadlockObservable.TelemetryPoolSize => _Pool.Count;

        int IPadlockObservable.TelemetryPoolCapacity => _PoolSize;

        /// <summary>
        /// Updates the maximum number of concurrent holders for this instance.
        /// </summary>
        /// <remarks>
        /// The new limit is applied lazily. It takes effect for any key acquired after this call, and for any
        /// currently-idle key the next time it is acquired (including lock entries recycled from the pool). A key
        /// that is active at the moment of the call keeps its existing limit until every holder and waiter releases
        /// and its entry becomes idle; the next acquisition then adopts the new limit. As a result, increases become
        /// visible as keys are acquired or recycled, while a decrease only takes effect for a given key once that key
        /// drains to zero holders at least once. Existing holders are never evicted.
        /// </remarks>
        /// <param name="maxCount">New maximum number of concurrent holders. Must be at least 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxCount"/> is less than 1.</exception>
        public void SetMaxCount(int maxCount)
        {
            if (maxCount < 1) throw new ArgumentOutOfRangeException(nameof(maxCount), "Must be at least 1.");
            int previous = _MaxCount;
            _MaxCount = maxCount;
            PadlockInstrumentation.RecordMaxCountChange(_Name, previous, maxCount);
        }

        /// <summary>
        /// Acquires a lock for the specified key. Returns a disposable handle that will release the lock when disposed.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is a null reference.</exception>
        public IDisposable Lock(T key)
        {
            string name = _Name;
            long waitStart = PadlockInstrumentation.GetWaitStartTimestamp();
            bool pendingCounted = PadlockInstrumentation.AddPending(name, PadlockTelemetry.ModeSync);
            Activity activity = PadlockInstrumentation.StartAcquire(name, PadlockTelemetry.ModeSync, _MaxCount);
            bool contended = false;
            try
            {
                LockEntry entry = AcquireEntry(key);
                contended = entry.Semaphore.CurrentCount == 0;
                entry.Semaphore.Wait();
                PadlockInstrumentation.AcquireSucceeded(activity, name, PadlockTelemetry.ModeSync, contended, waitStart);
                return new LockReleaser(this, entry, key, name, PadlockTelemetry.ModeSync);
            }
            catch (Exception e)
            {
                PadlockInstrumentation.AcquireFailed(activity, name, PadlockTelemetry.ModeSync, contended, waitStart, e);
                throw;
            }
            finally
            {
                PadlockInstrumentation.RemovePending(pendingCounted, name, PadlockTelemetry.ModeSync);
                PadlockInstrumentation.StopActivity(activity);
            }
        }

        /// <summary>
        /// Asynchronously acquires a lock for the specified key.
        /// </summary>
        /// <param name="key">The key on which to lock.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A disposable object that releases the lock when disposed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is a null reference.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled before the lock is acquired.</exception>
        public async ValueTask<IDisposable> LockAsync(T key, CancellationToken cancellationToken = default)
        {
            string name = _Name;
            long waitStart = PadlockInstrumentation.GetWaitStartTimestamp();
            bool pendingCounted = PadlockInstrumentation.AddPending(name, PadlockTelemetry.ModeAsync);
            Activity activity = PadlockInstrumentation.StartAcquire(name, PadlockTelemetry.ModeAsync, _MaxCount);
            bool contended = false;
            try
            {
                LockEntry entry = AcquireEntry(key);
                try
                {
                    Task wait = entry.Semaphore.WaitAsync(cancellationToken);
                    contended = !wait.IsCompleted;
                    await wait.ConfigureAwait(false);
                }
                catch
                {
                    ReleaseEntry(entry, key);
                    throw;
                }
                PadlockInstrumentation.AcquireSucceeded(activity, name, PadlockTelemetry.ModeAsync, contended, waitStart);
                return new LockReleaser(this, entry, key, name, PadlockTelemetry.ModeAsync);
            }
            catch (Exception e)
            {
                PadlockInstrumentation.AcquireFailed(activity, name, PadlockTelemetry.ModeAsync, contended, waitStart, e);
                throw;
            }
            finally
            {
                PadlockInstrumentation.RemovePending(pendingCounted, name, PadlockTelemetry.ModeAsync);
                PadlockInstrumentation.StopActivity(activity);
            }
        }

        /// <summary>
        /// Checks if a key is currently locked.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if locked, false otherwise.</returns>
        public bool IsLocked(T key)
        {
            if (_Locks.TryGetValue(key, out LockEntry entry))
            {
                return entry.Semaphore.CurrentCount == 0;
            }
            return false;
        }

        private LockEntry CreateOrTakeFromPool()
        {
            if (_Pool.TryTake(out LockEntry entry))
            {
                entry.Reset(_MaxCount);
                PadlockInstrumentation.RecordPoolRequest(_Name, true);
                return entry;
            }
            PadlockInstrumentation.RecordPoolRequest(_Name, false);
            return new LockEntry(_MaxCount);
        }

        private LockEntry AcquireEntry(T key)
        {
            while (true)
            {
                LockEntry entry = _Locks.GetOrAdd(key, _ => CreateOrTakeFromPool());
                Monitor.Enter(entry);
                if (!entry.IsRemoved
                    && _Locks.TryGetValue(key, out LockEntry stored)
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
                _Locks.TryRemove(key, out _);
                Monitor.Exit(entry);
                ReturnToPoolOrDispose(entry);
                return;
            }
            Monitor.Exit(entry);
        }

        private void ReturnToPoolOrDispose(LockEntry entry)
        {
            if (_Pool.Count < _PoolSize)
            {
                _Pool.Add(entry);
            }
            else
            {
                entry.Semaphore.Dispose();
                PadlockInstrumentation.RecordPoolDiscard(_Name);
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
            private readonly Padlock<T> _Padlock;
            private readonly LockEntry _Entry;
            private readonly T _Key;
            private readonly string _Name;
            private readonly string _Mode;
            private readonly long _HoldStart;
            private readonly bool _HolderCounted;
            private int _Disposed;

            /// <summary>
            /// Creates a new lock releaser.
            /// </summary>
            /// <param name="padlock">The owning padlock instance.</param>
            /// <param name="entry">The lock entry to release.</param>
            /// <param name="key">The key associated with this lock.</param>
            /// <param name="name">The instance name at acquisition time, used for telemetry.</param>
            /// <param name="mode">The acquisition mode, used for telemetry.</param>
            public LockReleaser(Padlock<T> padlock, LockEntry entry, T key, string name, string mode)
            {
                _Padlock = padlock;
                _Entry = entry;
                _Key = key;
                _Name = name;
                _Mode = mode;
                _HoldStart = PadlockInstrumentation.GetHoldStartTimestamp();
                _HolderCounted = PadlockInstrumentation.AddHolder(name);
                _Disposed = 0;
            }

            /// <summary>
            /// Releases the lock.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _Disposed, 1) == 0)
                {
                    _Entry.Semaphore.Release();
                    _Padlock.ReleaseEntry(_Entry, _Key);
                    PadlockInstrumentation.ReleaseHolder(_HolderCounted, _Name, _Mode, _HoldStart);
                }
            }
        }
    }
}
