# Change Log

## Current Version

v1.2.1

- Updated test dependencies: `Touchstone.Core`, `Touchstone.Cli`, `Touchstone.XunitAdapter`, and `Touchstone.NunitAdapter` 0.1.12 -> 0.2.0; `NUnit` 4.6.1 -> 5.0.0; `NUnit.Analyzers` 4.14.0 -> 4.15.0; `NUnit3TestAdapter` 6.2.0 -> 6.3.0; `Microsoft.NET.Test.Sdk` 18.9.0 -> 18.10.1; `coverlet.collector` 10.0.1 -> 10.1.0
- No library API or runtime dependency changes; all 62 shared Touchstone tests pass under xUnit, NUnit 5, and the console runner on net8.0 and net10.0

## Previous Versions

v1.2.0

- Added built-in telemetry through `System.Diagnostics`: a `Meter` and an `ActivitySource`, both named `Padlock`, with no SDK or exporter dependency and near-zero cost when unobserved
- Metrics: `padlock.lock.wait.duration` (by mode, outcome, contention, and `error.type`), `padlock.lock.hold.duration`, `padlock.lock.holders`, `padlock.lock.pending`, `padlock.keys.active`, `padlock.max_count`, `padlock.max_count.changes`, `padlock.pool.size`, `padlock.pool.capacity`, `padlock.pool.requests`, `padlock.pool.discards`, `padlock.instances`, `padlock.build.info`
- Traces: a `padlock.acquire` span per acquisition, nested under the caller's activity, with status `Error` and an exception event on cancellation or failure; `SetMaxCount` adds a `padlock.max_count.changed` event to the current activity
- Added the `Name` property, used as the `padlock.name` label (default `default`)
- Added the `PadlockTelemetry` class with every meter, source, instrument, attribute, and value name as public constants
- Added `TELEMETRY.md` documenting the full catalog, subscription, PromQL, and alerts
- `LockAsync` now awaits with `ConfigureAwait(false)`
- Added `System.Diagnostics.DiagnosticSource` 10.0.12 package reference for targets other than net10.0
- Added telemetry test suite using in-memory `MeterListener`/`ActivityListener`, covering success, contention, cancellation, error, pool, gauge, no-listener, and throwing-listener paths

v1.1.0

- Added `SetMaxCount(int)` to adjust the instance-wide maximum number of concurrent holders at runtime
- Added `MaxCount` property to read the current maximum
- Runtime changes are applied lazily: they take effect for keys acquired after the call and for idle keys on their next acquisition (including pooled entries reused via `Reset`); a key that is active at the time of the change keeps its existing limit until it drains, and existing holders are never evicted
- Added positive and negative test coverage for runtime `maxCount` changes, including a long-running multi-threaded stress test that churns `maxCount` under contention and validates the concurrency ceiling is never exceeded

v1.0.3

- Fixed race condition in pooling: `Reset()` no longer disposes the old semaphore while a stale reference may still read it; the old `SemaphoreSlim` is left for GC collection
- Fixed `AcquireEntry` to verify the entry is still the current one in the dictionary after entering the monitor, preventing cross-key contamination from pooled entry reuse
- Restructured test program with per-test PASS/FAIL reporting, per-test runtime, and overall summary with failed test listing

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
