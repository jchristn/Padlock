# Change Log

## Current Version

v1.0.3

- Fixed race condition in pooling: `Reset()` no longer disposes the old semaphore while a stale reference may still read it; the old `SemaphoreSlim` is left for GC collection
- Fixed `AcquireEntry` to verify the entry is still the current one in the dictionary after entering the monitor, preventing cross-key contamination from pooled entry reuse
- Restructured test program with per-test PASS/FAIL reporting, per-test runtime, and overall summary with failed test listing

## Previous Versions

v1.0.2

- Fixed race condition in lock cleanup using `Monitor.Enter`/`Monitor.Exit` on `LockEntry` objects, replacing the CAS/sentinel approach (reported by @MarkCiliaVincenti)
- Added configurable concurrency via `maxCount` constructor parameter (default 1) to allow multiple concurrent holders per key
- Added object pooling via `ConcurrentBag<LockEntry>` with configurable `poolSize` constructor parameter (default 20) to reduce allocations
- Changed `LockAsync` return type from `Task<IDisposable>` to `ValueTask<IDisposable>` for reduced overhead
- Added conditional `System.Threading.Tasks.Extensions` package reference for netstandard2.0 ValueTask support
- `LockReleaser` now stores a `Padlock<T>` reference instead of a static method and dictionary reference
- Added XML documentation on all public members
- Replaced all `var` usage with explicit types

v1.0.1

- Fixed issue found by @MarkCiliaVincenti
- Added test to validate key removal after use

v1.0.0

- Initial release
- Key-based locking for any type (string, int, GUID, custom objects)
- Synchronous and asynchronous locking via `Lock` and `LockAsync`
- Cancellation support via `CancellationToken`
- `IsLocked` check for lock status
- Automatic resource cleanup when locks are released
- Targets netstandard2.0, netstandard2.1, net8.0, net10.0
