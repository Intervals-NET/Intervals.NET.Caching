# Components: Public API

## Overview

This page documents the public surface area of `Intervals.NET.Caching.VisitedPlaces` and `Intervals.NET.Caching`: the cache facade, shared interfaces, configuration, eviction, diagnostics, and public DTOs.

## Packages

### Intervals.NET.Caching

Shared contracts and infrastructure for all cache implementations:

- `IRangeCache<TRange, TData, TDomain>` — shared cache interface: `GetDataAsync`, `WaitForIdleAsync`, `IAsyncDisposable`
- `IDataSource<TRange, TData>` — data source contract
- `RangeResult<TRange, TData>`, `RangeChunk<TRange, TData>`, `CacheInteraction` — shared DTOs
- `LayeredRangeCache<TRange, TData, TDomain>` — thin `IRangeCache` wrapper for layered stacks
- `RangeCacheDataSourceAdapter<TRange, TData, TDomain>` — adapts `IRangeCache` as `IDataSource`
- `LayeredRangeCacheBuilder<TRange, TData, TDomain>` — fluent builder for layered stacks
- `RangeCacheConsistencyExtensions` — `GetDataAndWaitForIdleAsync` (strong consistency) on `IRangeCache`

### Intervals.NET.Caching.VisitedPlaces

VisitedPlaces-specific implementation:

- `VisitedPlacesCache<TRange, TData, TDomain>` — primary entry point; implements `IVisitedPlacesCache`
- `IVisitedPlacesCache<TRange, TData, TDomain>` — marker interface extending `IRangeCache`; types eviction-aware implementations
- `VisitedPlacesCacheBuilder` / `VisitedPlacesCacheBuilder<TRange, TData, TDomain>` — builder for single-layer and layered caches
- `VisitedPlacesLayerExtensions` — `AddVisitedPlacesLayer` on `LayeredRangeCacheBuilder`
- `VisitedPlacesCacheOptions<TRange, TData>` / `VisitedPlacesCacheOptionsBuilder<TRange, TData>` — configuration
- `IVisitedPlacesCacheDiagnostics` / `NoOpDiagnostics` — instrumentation
- Eviction: `IEvictionPolicy<TRange, TData>`, `IEvictionSelector<TRange, TData>`, `EvictionConfigBuilder<TRange, TData>`

## Facade

- `VisitedPlacesCache<TRange, TData, TDomain>`: primary entry point and composition root.
  - **File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Cache/VisitedPlacesCache.cs`
  - Constructs and wires all internal components.
  - Delegates user requests to `UserRequestHandler`.
  - Exposes `WaitForIdleAsync()` for infrastructure/testing synchronization.
- `IVisitedPlacesCache<TRange, TData, TDomain>`: marker interface (for testing/mocking); extends `IRangeCache`. Adds no additional members — exists to constrain DI registrations to VisitedPlaces-compatible implementations.
  - **File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/IVisitedPlacesCache.cs`
- `IRangeCache<TRange, TData, TDomain>`: shared base interface.
  - **File**: `src/Intervals.NET.Caching/IRangeCache.cs`

## Configuration

### VisitedPlacesCacheOptions\<TRange, TData\>

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Configuration/VisitedPlacesCacheOptions.cs`

**Type**: `sealed class` (immutable; value equality via `IEquatable`)

| Parameter             | Description                                                                                      |
|-----------------------|--------------------------------------------------------------------------------------------------|
| `StorageStrategy`     | The internal segment collection strategy. Defaults to `SnapshotAppendBufferStorageOptions.Default` |
| `EventChannelCapacity`| Background event channel capacity, or `null` for unbounded task-chaining (default)              |
| `SegmentTtl`          | Time-to-live per cached segment, or `null` to disable TTL expiration (default)                  |

**Validation enforced at construction time:**
- `EventChannelCapacity >= 1` (when specified)
- `SegmentTtl > TimeSpan.Zero` (when specified)

**See**: `docs/visited-places/storage-strategies.md` for storage strategy selection guidance.

### VisitedPlacesCacheOptionsBuilder\<TRange, TData\>

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Configuration/VisitedPlacesCacheOptionsBuilder.cs`

Fluent builder for `VisitedPlacesCacheOptions`. Methods:

| Method                        | Sets                      |
|-------------------------------|---------------------------|
| `WithStorageStrategy(options)`| `StorageStrategy`         |
| `WithEventChannelCapacity(n)` | `EventChannelCapacity`    |
| `WithSegmentTtl(ttl)`         | `SegmentTtl`              |
| `Build()`                     | Returns configured options |

## Data Source

### IDataSource\<TRange, TData\>

**File**: `src/Intervals.NET.Caching/IDataSource.cs`

**Type**: Interface (user-implemented); lives in `Intervals.NET.Caching`

- Single-range fetch (required): `FetchAsync(Range<TRange>, CancellationToken)`
- Batch fetch (optional): default implementation uses parallel single-range fetches

**Called exclusively from User Path** (`UserRequestHandler`): on each `GetDataAsync` call for any gap not already covered by cached segments. VPC does **not** call `IDataSource` from the Background Path.

**See**: `docs/shared/boundary-handling.md` for the full `IDataSource` boundary contract and examples.

## DTOs

All DTOs live in `Intervals.NET.Caching`.

### RangeResult\<TRange, TData\>

**File**: `src/Intervals.NET.Caching/Dto/RangeResult.cs`

Returned by `GetDataAsync`. Contains three properties:

| Property           | Type                    | Description                                                                                                                 |
|--------------------|-------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| `Range`            | `Range<TRange>?`        | **Nullable**. The actual range returned. `null` indicates no data available (physical boundary miss).                       |
| `Data`             | `ReadOnlyMemory<TData>` | The materialized data. Empty when `Range` is `null`.                                                                        |
| `CacheInteraction` | `CacheInteraction`      | How the request was served: `FullHit` (all from cache), `PartialHit` (cache + fetch), or `FullMiss` (no cache coverage).   |

### CacheInteraction

**File**: `src/Intervals.NET.Caching/Dto/CacheInteraction.cs`

**Type**: `enum`

| Value        | Meaning (VPC context)                                                                           |
|--------------|-------------------------------------------------------------------------------------------------|
| `FullMiss`   | No cached segments covered any part of the requested range; full fetch from `IDataSource`.      |
| `FullHit`    | All of the requested range was already covered by cached segments; no `IDataSource` call made.  |
| `PartialHit` | Some sub-ranges were cached; remaining gaps were fetched from `IDataSource`.                    |

### RangeChunk\<TRange, TData\>

**File**: `src/Intervals.NET.Caching/Dto/RangeChunk.cs`

Returned by `IDataSource.FetchAsync`. Contains:
- `Range<TRange>? Range` — the range covered by this chunk (`null` = physical boundary miss)
- `IEnumerable<TData> Data` — the data for this range

## Eviction

**See**: `docs/visited-places/eviction.md` for the full eviction system design.

### IEvictionPolicy\<TRange, TData\>

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Core/Eviction/IEvictionPolicy.cs`

Determines whether eviction is needed based on a pressure metric. Eviction is triggered when **any** configured policy produces exceeded pressure (OR semantics).

### IEvictionSelector\<TRange, TData\>

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Core/Eviction/IEvictionSelector.cs`

Determines the order in which segments are considered for eviction (e.g., LRU, random).

### EvictionConfigBuilder\<TRange, TData\>

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Core/Eviction/EvictionConfigBuilder.cs`

Fluent builder for wiring policies and a selector together. Used inline in `WithEviction(Action<EvictionConfigBuilder<...>>)`.

## Diagnostics

### IVisitedPlacesCacheDiagnostics

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Instrumentation/IVisitedPlacesCacheDiagnostics.cs`

Optional observability interface covering:
- User request outcomes (full hit, partial hit, full miss)
- Data source access events
- Background event scheduling events (enqueued, executed, dropped)
- Segment lifecycle: stored, evicted, TTL-expired

**Implementation**: `NoOpDiagnostics` — zero-overhead default when no diagnostics are provided.

**See**: `docs/visited-places/diagnostics.md` for comprehensive usage documentation.

## Builder API

### VisitedPlacesCacheBuilder (static entry point)

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Cache/VisitedPlacesCacheBuilder.cs`

Non-generic static class providing factory methods that enable full generic type inference:

```csharp
// Single-layer cache
await using var cache = VisitedPlacesCacheBuilder.For(dataSource, domain)
    .WithOptions(o => o.WithSegmentTtl(TimeSpan.FromMinutes(10)))
    .WithEviction(e => e
        .WithPolicy(new CountEvictionPolicy<int, MyData>(maxSegments: 100))
        .WithSelector(new LruEvictionSelector<int, MyData>()))
    .Build();

// Layered cache (VPC as inner layer, VPC as outer layer)
await using var layered = VisitedPlacesCacheBuilder.Layered(dataSource, domain)
    .AddVisitedPlacesLayer(/* inner layer config */)
    .AddVisitedPlacesLayer(/* outer layer config */)
    .BuildAsync();
```

### VisitedPlacesCacheBuilder\<TRange, TData, TDomain\>

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Cache/VisitedPlacesCacheBuilder.cs`

**Type**: `sealed class` — fluent builder; obtain via `VisitedPlacesCacheBuilder.For(dataSource, domain)`.

| Method                             | Description                                                    |
|------------------------------------|----------------------------------------------------------------|
| `WithOptions(options)`             | Supply a pre-built `VisitedPlacesCacheOptions` instance        |
| `WithOptions(configure)`           | Configure options inline via `VisitedPlacesCacheOptionsBuilder`|
| `WithDiagnostics(diagnostics)`     | Attach diagnostics; defaults to `NoOpDiagnostics`             |
| `WithEviction(policies, selector)` | Supply pre-built policies list and selector                    |
| `WithEviction(configure)`          | Configure eviction inline via `EvictionConfigBuilder`          |
| `Build()`                          | Construct and return the configured `IVisitedPlacesCache`     |

`Build()` throws `InvalidOperationException` if `WithOptions` or `WithEviction` was not called, or if called more than once on the same builder instance.

### VisitedPlacesLayerExtensions

**File**: `src/Intervals.NET.Caching.VisitedPlaces/Public/Extensions/VisitedPlacesLayerExtensions.cs`

**Type**: `static class` (extension methods on `LayeredRangeCacheBuilder<TRange, TData, TDomain>`)

Four overloads of `AddVisitedPlacesLayer`, covering all combinations of:
- Pre-built vs. inline options (`VisitedPlacesCacheOptions` vs. `Action<VisitedPlacesCacheOptionsBuilder>`)
- Pre-built vs. inline eviction (explicit `policies`/`selector` vs. `Action<EvictionConfigBuilder>`)

First call = innermost layer; last call = outermost (user-facing). Throws when policies are null/empty or selector is null.

## Strong Consistency

### RangeCacheConsistencyExtensions

**File**: `src/Intervals.NET.Caching/Extensions/RangeCacheConsistencyExtensions.cs`

**Type**: `static class` (extension methods on `IRangeCache<TRange, TData, TDomain>`)

#### GetDataAndWaitForIdleAsync

Composes `GetDataAsync` + unconditional `WaitForIdleAsync`. Always waits for the cache to reach idle after the request.

**When to use:**
- Asserting or inspecting cache state after a request (e.g., verifying a segment was stored)
- Cold start synchronization before subsequent operations
- Integration tests requiring deterministic cache state

**When NOT to use:**
- Hot paths — the idle wait adds latency equal to the full background processing cycle
- Parallel callers — serialized access required (Invariant S.H.3)

**Exception propagation**: If `GetDataAsync` throws, `WaitForIdleAsync` is never called. If `WaitForIdleAsync` throws `OperationCanceledException`, the already-obtained result is returned (graceful degradation to eventual consistency).

## Multi-Layer Cache

Three classes in `Intervals.NET.Caching` support layered stacks. `VisitedPlacesCacheBuilder.Layered` and `VisitedPlacesLayerExtensions.AddVisitedPlacesLayer` provide the VPC-specific entry points.

**See**: `docs/sliding-window/components/public-api.md` (Multi-Layer Cache section) for `LayeredRangeCache`, `RangeCacheDataSourceAdapter`, and `LayeredRangeCacheBuilder` documentation — these types are shared and behave identically for VPC.

## See Also

- `docs/shared/boundary-handling.md`
- `docs/visited-places/diagnostics.md`
- `docs/visited-places/invariants.md`
- `docs/visited-places/storage-strategies.md`
- `docs/visited-places/eviction.md`
