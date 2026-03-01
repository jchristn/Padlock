<img src="https://github.com/jchristn/padlock/blob/main/assets/icon.png?raw=true" alt="Padlock" width="128px" height="128px" />

# Padlock

[![NuGet Version](https://img.shields.io/nuget/v/Padlock.svg?style=flat)](https://www.nuget.org/packages/Padlock/) [![NuGet](https://img.shields.io/nuget/dt/Padlock.svg)](https://www.nuget.org/packages/Padlock)

## Description

Padlock is a lightweight, high-performance library that provides key-based locking for multithreaded applications. It enables granular locking on specific resources identified by keys of any type, allowing for efficient concurrency control without unnecessary blocking.

Core features:

- Create locks based on any key type (string, int, GUID, custom objects, etc.)
- Support for both synchronous and asynchronous locking patterns
- Configurable concurrency per key (exclusive or shared locks)
- Object pooling for reduced allocations under high throughput
- ValueTask-based async API for reduced overhead
- Efficient memory usage with automatic resource cleanup
- Cancellation support via standard CancellationToken
- Simple, intuitive API with IDisposable pattern for lock release

## Special Thanks

Special thanks to @MarkCiliaVincenti for his help on this repo.  His open source contributions have helped make this library better.  He has a similar yet more robust and battle-tested library in [AsyncKeyedLock](https://github.com/MarkCiliaVincenti/AsyncKeyedLock) which also offers striped locking, "lock-or-null" patterns, conditional locking, and more.

## Constructor Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxCount` | `int` | `1` | Maximum number of concurrent holders for each key. Use `1` for exclusive locking, or a higher value for shared/throttled access. |
| `poolSize` | `int` | `20` | Maximum number of pooled lock entries kept for reuse. Reduces allocations in high-throughput scenarios. |

## Simple Example

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Padlocks;

Padlock<string> padlock = new Padlock<string>(); // using string keys

using (padlock.Lock("resource1")) // synchronous
{
    Console.WriteLine("Resource 1 is locked and being accessed");
}

await Task.Run(async () => // asynchronous
{
    using (await padlock.LockAsync("resource2"))
    {
        Console.WriteLine("Resource 2 is locked and being accessed");
    }
});

CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    using (await padlock.LockAsync("resource3", cts.Token))
    {
        Console.WriteLine("Resource 3 is locked and being accessed");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Lock acquisition was cancelled");
}

bool isLocked = padlock.IsLocked("resource1");
Console.WriteLine($"Resource 1 is {(isLocked ? "locked" : "available")}");
```

Padlock supports mixing both synchronous and asynchronous lock operations on the same resource.

## Configurable Concurrency

Use `maxCount` to allow multiple concurrent holders per key:

```csharp
// Allow up to 3 concurrent holders per key
Padlock<string> padlock = new Padlock<string>(maxCount: 3);

// All three of these can proceed concurrently for the same key
Task task1 = Task.Run(async () =>
{
    using (await padlock.LockAsync("resource"))
    {
        // Work with resource concurrently (up to 3 at a time)
        await Task.Delay(1000);
    }
});

Task task2 = Task.Run(async () =>
{
    using (await padlock.LockAsync("resource"))
    {
        await Task.Delay(1000);
    }
});

Task task3 = Task.Run(async () =>
{
    using (await padlock.LockAsync("resource"))
    {
        await Task.Delay(1000);
    }
});

await Task.WhenAll(task1, task2, task3);
```

## Lock With Custom Types

Padlock works with any type that properly implements equality comparison:

```csharp
// Custom type with custom equality logic
public class ResourceKey : IEquatable<ResourceKey>
{
    public string Name { get; }
    public int Id { get; }

    public ResourceKey(string name, int id)
    {
        Name = name;
        Id = id;
    }

    public bool Equals(ResourceKey other)
    {
        if (other is null) return false;
        return Name == other.Name && Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is ResourceKey key && Equals(key);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Id);
    }
}

Padlock<ResourceKey> padlock = new Padlock<ResourceKey>();
ResourceKey resourceKey = new ResourceKey("UserProfile", 42);

using (padlock.Lock(resourceKey))
{
    Console.WriteLine("Resource is locked and being accessed");
}
```

## Performance

Padlock is designed for high performance with minimal overhead:

- Uses `SemaphoreSlim` for efficient lock management
- Object pooling reduces allocations in high-throughput scenarios
- Monitor-based coordination for race-free cleanup
- Automatically cleans up unused lock objects to reduce memory usage
- Avoids unnecessary blocking when possible
- Handles high-contention scenarios gracefully

## Installation

```
Install-Package Padlock
```

Or via the .NET CLI:

```
dotnet add package Padlock
```

## Version History

Refer to CHANGELOG.md.

## Icon

Many thanks to [Vector Stall](https://www.flaticon.com/free-icon/padlock_5272442) for creating the icon.
