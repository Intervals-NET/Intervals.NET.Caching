# Components: State and Storage

## Overview

State and storage define how cached data is held, read, and published. `CacheState` is the central shared mutable state of the system — written exclusively by `RebalanceExecutor`, and read by `UserRequestHandler` and `RebalanceDecisionEngine`.

## Key Components

| Component                                     | File                                                                   | Role                                                |
|-----------------------------------------------|------------------------------------------------------------------------|-----------------------------------------------------|
| `CacheState<TRange, TData, TDomain>`          | `src/SlidingWindowCache/Core/State/CacheState.cs`                      | Shared mutable state; the single coordination point |
| `ICacheStorage<TRange, TData, TDomain>`       | `src/SlidingWindowCache/Infrastructure/Storage/ICacheStorage.cs`       | Internal storage contract                           |
| `SnapshotReadStorage<TRange, TData, TDomain>` | `src/SlidingWindowCache/Infrastructure/Storage/SnapshotReadStorage.cs` | Array-based; zero-allocation reads                  |
| `CopyOnReadStorage<TRange, TData, TDomain>`   | `src/SlidingWindowCache/Infrastructure/Storage/CopyOnReadStorage.cs`   | List-based; cheap rematerialization                 |

## CacheState

**File**: `src/SlidingWindowCache/Core/State/CacheState.cs`

`CacheState` is shared by reference across `UserRequestHandler`, `RebalanceDecisionEngine`, and `RebalanceExecutor`. It holds:

| Field              | Type            | Written by               | Read by                                |
|--------------------|-----------------|--------------------------|----------------------------------------|
| `Cache` (storage)  | `ICacheStorage` | `RebalanceExecutor` only | `UserRequestHandler`, `DecisionEngine` |
| `IsInitialized`    | `bool`          | `RebalanceExecutor` only | `UserRequestHandler`                   |
| `NoRebalanceRange` | `Range?`        | `RebalanceExecutor` only | `DecisionEngine`                       |

**Single-Writer Rule (Invariant A.7):** Only `RebalanceExecutor` writes any field of `CacheState`. User path components are read-only. This is enforced by internal visibility modifiers (setters are `internal`), not by locks.

**No internal locking:** The single-writer constraint makes locks unnecessary. `Volatile.Write` / `Volatile.Read` patterns ensure visibility across threads where needed.

**Atomic updates via `Rematerialize`:** The `Rematerialize` method replaces the storage contents in a single atomic operation. No intermediate states are visible to readers.

## Storage Strategies

### SnapshotReadStorage

**Type**: `internal sealed class`

**Strategy**: Array-based with atomic replacement on rematerialization.

| Operation       | Behavior                                                                      |
|-----------------|-------------------------------------------------------------------------------|
| `Rematerialize` | Allocates new `TData[]`, performs `Array.Copy`, atomically replaces reference |
| `Read`          | Returns zero-allocation `ReadOnlyMemory<TData>` view over internal array      |
| `ToRangeData`   | Creates snapshot from current array                                           |

**Characteristics**:
- ✅ Zero-allocation reads (fastest user path)
- ❌ Expensive rematerialization (always allocates new array)
- ⚠️ Large arrays (≥ 85 KB) may end up on the LOH
- Best for: read-heavy workloads, predictable memory patterns

### CopyOnReadStorage

**Type**: `internal sealed class`

**Strategy**: Dual-buffer pattern — active storage is never mutated during enumeration.

| Operation       | Behavior                                                                                |
|-----------------|-----------------------------------------------------------------------------------------|
| `Rematerialize` | Clears staging buffer, fills with new data, atomically swaps with active                |
| `Read`          | Acquires lock, allocates `TData[]`, copies from active buffer, returns `ReadOnlyMemory` |
| `ToRangeData`   | Returns lazy enumerable over active storage (unsynchronized; rebalance path only)       |

**Staging Buffer Pattern:**
```
Active buffer:   [existing data]  ← user reads here (immutable during enumeration)
Staging buffer:  [new data]       ← rematerialization builds here
                      ↓ swap (under lock)
Active buffer:   [new data]       ← now visible to reads
Staging buffer:  [old data]       ← reused next rematerialization (capacity preserved)
```

**Characteristics**:
- ✅ Cheap rematerialization (amortized O(1) when capacity sufficient)
- ✅ No LOH pressure (List growth strategy)
- ✅ Correct enumeration during LINQ-derived expansion
- ❌ Allocation on every read (lock + array copy)
- Best for: rematerialization-heavy workloads, large sliding windows

> **Note**: `ToRangeData()` is unsynchronized and must only be called from the rebalance path. See `docs/storage-strategies.md`.

### Strategy Selection

Controlled by `WindowCacheOptions.UserCacheReadMode`:
- `UserCacheReadMode.Snapshot` → `SnapshotReadStorage`
- `UserCacheReadMode.CopyOnRead` → `CopyOnReadStorage`

## Read/Write Pattern Summary

```
UserRequestHandler  ──reads───▶ CacheState.Cache.Read()
                                CacheState.Cache.ToRangeData()
                                CacheState.IsInitialized

DecisionEngine      ──reads───▶ CacheState.NoRebalanceRange
                                CacheState.Cache.Range

RebalanceExecutor   ──writes──▶ CacheState.Cache.Rematerialize()    ← SOLE WRITER
                                CacheState.NoRebalanceRange         ← SOLE WRITER
                                CacheState.IsInitialized            ← SOLE WRITER
```

## Invariants

| Invariant | Description                                                          |
|-----------|----------------------------------------------------------------------|
| A.7       | Only `RebalanceExecutor` writes `CacheState`                         |
| A.9       | `IsInitialized` only transitions false → true (monotonic)            |
| B.11      | Cache is always contiguous (no gaps in cached range)                 |
| B.12      | Cache updates are atomic via `Rematerialize`                         |
| B.13      | Consistency under cancellation: partial results discarded            |
| B.15      | Cache contiguity invariant maintained after every rematerialization  |
| E.34      | `NoRebalanceRange` is computed correctly from thresholds             |
| E.35      | `NoRebalanceRange` is always contained within `CacheRange`           |
| F.37      | `Rematerialize` accepts arbitrary range and replaces entire contents |

See `docs/invariants.md` (Sections A, B, E, F) for full specification.

## Notes

- "Single logical consumer" is a **usage model** constraint; internal concurrency (user thread + background loops) is fully supported by design.
- Multiple threads from the **same** logical consumer can call `GetDataAsync` safely — the user path is read-only.
- Multiple **independent** consumers should use separate cache instances; sharing violates the coherent access pattern assumption.

## See Also

- `docs/storage-strategies.md` — detailed strategy comparison, performance characteristics, and selection guide
- `docs/invariants.md` — Sections A (write authority), B (state invariants), E (range planning)
- `docs/components/execution.md` — how `RebalanceExecutor` performs writes
