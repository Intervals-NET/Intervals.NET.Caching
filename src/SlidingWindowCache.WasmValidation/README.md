# SlidingWindowCache.WasmValidation

## Purpose

This project is a **WebAssembly compilation validation target** for the SlidingWindowCache library. It is **NOT** a demo application, test project, or runtime sample.

## Goal

The sole purpose of this project is to ensure that the SlidingWindowCache library successfully compiles for the `net8.0-browser` target framework, validating WebAssembly compatibility.

## What This Is NOT

- ❌ **Not a demo** - Does not demonstrate usage patterns or best practices
- ❌ **Not a test project** - Contains no assertions, test framework, or test execution logic
- ❌ **Not a runtime validation** - Code is not intended to be executed in CI/CD or production
- ❌ **Not a sample** - Does not showcase real-world scenarios or advanced features

## What This IS

- ✅ **Compile-only validation** - Successful build proves WebAssembly compatibility
- ✅ **CI/CD compatibility check** - Ensures library can target browser environments
- ✅ **Strategy coverage validation** - Validates all internal storage and serialization strategies
- ✅ **Minimal API usage** - Instantiates core types to validate no platform-incompatible APIs are used

## Implementation

The project validates all combinations of **strategy-determining configuration options** that affect internal implementation paths:

### Strategy Matrix (2×2 = 4 Configurations)

| Config | ReadMode   | RebalanceQueueCapacity | Storage Strategy    | Serialization Strategy  |
|--------|------------|------------------------|---------------------|-------------------------|
| **1**  | Snapshot   | null                   | SnapshotReadStorage | Task-based (unbounded)  |
| **2**  | CopyOnRead | null                   | CopyOnReadStorage   | Task-based (unbounded)  |
| **3**  | Snapshot   | 5                      | SnapshotReadStorage | Channel-based (bounded) |
| **4**  | CopyOnRead | 5                      | CopyOnReadStorage   | Channel-based (bounded) |

### Why These Configurations?

**ReadMode** determines the storage strategy:
- `Snapshot` → `SnapshotReadStorage` (contiguous array, zero-allocation reads)
- `CopyOnRead` → `CopyOnReadStorage` (growable List, copy-on-read)

**RebalanceQueueCapacity** determines the serialization strategy:
- `null` → Task-based serialization (unbounded queue, task chaining)
- `>= 1` → Channel-based serialization (System.Threading.Channels with bounded capacity)

Other configuration parameters (leftCacheSize, rightCacheSize, thresholds, debounceDelay) are numeric values that don't affect code path selection, so they don't require separate WASM validation.

### Validation Methods

Each configuration has a dedicated validation method:

1. `ValidateConfiguration1_SnapshotMode_UnboundedQueue()`
2. `ValidateConfiguration2_CopyOnReadMode_UnboundedQueue()`
3. `ValidateConfiguration3_SnapshotMode_BoundedQueue()`
4. `ValidateConfiguration4_CopyOnReadMode_BoundedQueue()`

All methods perform identical operations:
1. Implement a simple `IDataSource<int, int>`
2. Instantiate `WindowCache<int, int, IntegerFixedStepDomain>` with specific configuration
3. Call `GetDataAsync` with a `Range<int>`
4. Use `ReadOnlyMemory<int>` return type
5. Call `WaitForIdleAsync` for completeness

All code uses deterministic, synchronous-friendly patterns suitable for compile-time validation.

## Build Validation

To validate WebAssembly compatibility:

```bash
dotnet build src/SlidingWindowCache.WasmValidation/SlidingWindowCache.WasmValidation.csproj
```

A successful build confirms that:
- All SlidingWindowCache public APIs compile for `net8.0-browser`
- No platform-specific APIs incompatible with WebAssembly are used
- Intervals.NET dependencies are WebAssembly-compatible
- **All internal storage strategies** (SnapshotReadStorage, CopyOnReadStorage) are WASM-compatible
- **All serialization strategies** (task-based, channel-based) are WASM-compatible

## Target Framework

- **Framework**: `net8.0-browser`
- **SDK**: Microsoft.NET.Sdk
- **Output**: Class library (no entry point)

## Dependencies

Matches the main library dependencies:
- Intervals.NET.Data (0.0.1)
- Intervals.NET.Domain.Default (0.0.2)
- Intervals.NET.Domain.Extensions (0.0.3)
- SlidingWindowCache (project reference)

## Integration with CI/CD

This project should be included in CI build matrices to automatically validate WebAssembly compatibility on every build. Any compilation failure indicates a breaking change for browser-targeted applications.
