# Diagnostics — Shared Pattern

This document covers the diagnostics pattern that applies across all cache implementations. Implementation-specific diagnostics (specific callbacks, event meanings) are documented in each implementation's docs.

---

## Design Philosophy

Diagnostics are an optional observability layer with **zero cost when not used**. The default implementation (`NoOpDiagnostics`) has no-op methods that the JIT eliminates entirely — no branching, no allocation, no overhead.

When diagnostics are wired, each event is a simple method call. Implementations are user-provided and may fan out to counters, metrics systems, loggers, or test assertions.

---

## Interface Hierarchy

The diagnostics system uses a two-level interface hierarchy:

### Shared base: `ICacheDiagnostics` (in `Intervals.NET.Caching`)

Contains events common to all cache implementations:

| Method                                 | Description                                               |
|----------------------------------------|-----------------------------------------------------------|
| `UserRequestServed()`                  | A user request was successfully served                    |
| `UserRequestFullCacheHit()`            | All requested data was found in cache                     |
| `UserRequestPartialCacheHit()`         | Requested data was partially found in cache               |
| `UserRequestFullCacheMiss()`           | No requested data was found in cache                      |
| `BackgroundOperationFailed(Exception)` | A background operation failed with an unhandled exception |

### Package-specific interfaces

Each package defines its own interface that inherits from `ICacheDiagnostics`:

- **`ISlidingWindowCacheDiagnostics`** (in `Intervals.NET.Caching.SlidingWindow`) — adds rebalance lifecycle events
- **`IVisitedPlacesCacheDiagnostics`** (in `Intervals.NET.Caching.VisitedPlaces`) — adds normalization and eviction events

---

## Two-Tier Pattern

Every cache implementation exposes a diagnostics interface with two default implementations:

### NoOpDiagnostics (default)

Empty implementation. Methods are empty and get inlined/eliminated by the JIT.

- **Zero overhead** — no performance impact whatsoever
- **No memory allocations**
- Used automatically when no diagnostics instance is provided

### EventCounterCacheDiagnostics (built-in counter)

Thread-safe atomic counter implementation using `Interlocked.Increment`.

- ~1–5 nanoseconds per event
- No locks, no allocations
- `Reset()` method for test isolation
- Use for testing, development, and production monitoring

---

## Critical: BackgroundOperationFailed

Every cache implementation exposes `BackgroundOperationFailed(Exception ex)` via the shared `ICacheDiagnostics` base interface. This is the **only signal** for silent background failures.

Background operations run fire-and-forget. When they fail:
1. The exception is caught
2. `BackgroundOperationFailed(ex)` is called
3. The exception is **swallowed** to prevent application crashes
4. The cache continues serving user requests (but background operations stop)

**Without handling this event, failures are completely silent.**

Minimum production implementation:

```csharp
void ICacheDiagnostics.BackgroundOperationFailed(Exception ex)
{
    _logger.LogError(ex,
        "Cache background operation failed. Cache will continue serving user requests " +
        "but background processing has stopped. Investigate data source health and cache configuration.");
}
```

---

## Custom Implementations

Implement the package-specific diagnostics interface for custom observability:

```csharp
// SlidingWindow example
public class PrometheusMetricsDiagnostics : ISlidingWindowCacheDiagnostics
{
    private readonly Counter _requestsServed;
    private readonly Counter _cacheHits;

    void ICacheDiagnostics.UserRequestServed() => _requestsServed.Inc();
    void ICacheDiagnostics.UserRequestFullCacheHit() => _cacheHits.Inc();

    // Shared base method — always implement this in production
    void ICacheDiagnostics.BackgroundOperationFailed(Exception ex) =>
        _logger.LogError(ex, "Cache background operation failed.");

    // SlidingWindow-specific methods
    public void RebalanceExecutionCompleted() => _rebalances.Inc();
    // ...
}
```

---

## See Also

- `docs/sliding-window/diagnostics.md` — full `ISlidingWindowCacheDiagnostics` event reference (18 events, test patterns, layered cache diagnostics)
