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

## Execution Context & Threading

### Where hooks execute

Diagnostic hooks are invoked **synchronously** on the library's internal threads. The calling thread depends on the event:

| Thread                | Description                                                           | Which events                                                                                     |
|-----------------------|-----------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| **User Thread**       | The thread calling `GetDataAsync` / `GetDataAndWaitForIdleAsync` etc. | `UserRequest*`, `DataSourceFetch*`, `CacheExpanded`, `CacheReplaced`, `RebalanceIntentPublished` |
| **Background Thread** | Internal background loops (rebalance execution, normalization, TTL)   | All other events                                                                                 |

> Each event's XML doc (and the package-specific diagnostics docs) includes a `Context:` annotation with the exact thread.

### Rules for implementations

> ⚠️ **Warning:** Diagnostic hooks execute synchronously inside library threads. Any long-running or blocking code inside a hook will stall that thread and directly slow down the cache.

**Lightweight operations are fine:**
- Logging calls (e.g., `_logger.LogInformation(...)`)
- Incrementing atomic counters (`Interlocked.Increment`)
- Updating metrics/telemetry spans

**For heavy work, dispatch yourself:**
```csharp
void ISlidingWindowCacheDiagnostics.RebalanceExecutionCompleted()
{
    // Don't do heavy work here — dispatch to ThreadPool instead
    _ = Task.Run(() => NotifyExternalSystem());
}
```

**Never throw from a hook.** An exception propagates directly into a library thread and will crash background loops or corrupt user request handling. Wrap the entire implementation body in try/catch:
```csharp
void ICacheDiagnostics.BackgroundOperationFailed(Exception ex)
{
    try
    {
        _logger.LogError(ex, "Cache background operation failed.");
    }
    catch { /* silently ignore — never let diagnostics crash the cache */ }
}
```

### ExecutionContext flows correctly

Hooks execute with the `ExecutionContext` captured from the thread that triggered the event. This means:

- `AsyncLocal<T>` values (e.g., request IDs, tenant IDs) are available
- `Activity` / OpenTelemetry tracing context is propagated
- `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` are preserved

You do not need to manually capture or restore context — it flows automatically into every hook invocation.

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
