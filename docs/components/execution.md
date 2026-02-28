# Components: Execution

## Overview

The execution subsystem performs debounced, cancellable background work and is the **only path allowed to mutate shared cache state** (single-writer invariant). It receives validated execution requests from `IntentController` and ensures single-flight, eventually-consistent cache updates.

## Key Components

| Component                                                          | File                                                                                          | Role                                                               |
|--------------------------------------------------------------------|-----------------------------------------------------------------------------------------------|--------------------------------------------------------------------|
| `IRebalanceExecutionController<TRange, TData, TDomain>`            | `src/SlidingWindowCache/Infrastructure/Execution/IRebalanceExecutionController.cs`            | Execution serialization contract                                   |
| `TaskBasedRebalanceExecutionController<TRange, TData, TDomain>`    | `src/SlidingWindowCache/Infrastructure/Execution/TaskBasedRebalanceExecutionController.cs`    | Default: Task.Run-based debounce + cancellation                    |
| `ChannelBasedRebalanceExecutionController<TRange, TData, TDomain>` | `src/SlidingWindowCache/Infrastructure/Execution/ChannelBasedRebalanceExecutionController.cs` | Optional: Channel-based bounded execution queue                    |
| `RebalanceExecutor<TRange, TData, TDomain>`                        | `src/SlidingWindowCache/Core/Rebalance/Execution/RebalanceExecutor.cs`                        | Sole writer; performs `Rematerialize`; the single-writer authority |
| `CacheDataExtensionService<TRange, TData, TDomain>`                | `src/SlidingWindowCache/Infrastructure/Services/CacheDataExtensionService.cs`                 | Incremental data fetching; range gap analysis                      |

## Execution Controllers

### TaskBasedRebalanceExecutionController (default)

- Uses `Task.Run` with debounce delay and `CancellationTokenSource`
- On each new execution request: cancels previous task, starts new task after debounce
- Selected when `WindowCacheOptions.RebalanceQueueCapacity` is `null`

### ChannelBasedRebalanceExecutionController (optional)

- Uses `System.Threading.Channels.Channel<T>` with bounded capacity
- Provides backpressure semantics; oldest unprocessed request may be dropped on overflow
- Selected when `WindowCacheOptions.RebalanceQueueCapacity` is set

**Strategy comparison:**

| Aspect       | TaskBased                  | ChannelBased           |
|--------------|----------------------------|------------------------|
| Debounce     | Per-request delay          | Channel draining       |
| Backpressure | None                       | Bounded capacity       |
| Cancellation | CancellationToken per task | Token per channel item |
| Default      | ✅ Yes                      | No                     |

## RebalanceExecutor — Single Writer

`RebalanceExecutor` is the **sole authority** for cache mutations. All other components are read-only with respect to `CacheState`.

**Execution flow:**

1. `ThrowIfCancellationRequested` — before any I/O (pre-I/O checkpoint)
2. Compute desired range gaps: `DesiredRange \ CurrentCacheRange`
3. Call `CacheDataExtensionService.ExtendCacheDataAsync` — fetches only missing subranges
4. `ThrowIfCancellationRequested` — after I/O, before mutations (pre-mutation checkpoint)
5. Call `CacheState.Rematerialize(newRangeData)` — atomic cache update
6. Update `CacheState.NoRebalanceRange` — new stability zone
7. Set `CacheState.IsInitialized = true` (if first execution)

**Cancellation checkpoints** (Invariant F.35):
- Before I/O: avoids unnecessary fetches
- After I/O: discards fetched data if superseded
- Before mutation: guarantees only latest validated execution applies changes

## CacheDataExtensionService — Incremental Fetching

**File**: `src/SlidingWindowCache/Infrastructure/Services/CacheDataExtensionService.cs`

- Computes missing ranges via range algebra: `DesiredRange \ CachedRange`
- Fetches only the gaps (not the full desired range)
- Merges new data with preserved existing data (union operation)
- Propagates `CancellationToken` to `IDataSource.FetchAsync`

**Invariants**: F.38 (incremental fetching), F.39 (data preservation during expansion).

## Responsibilities

- Debounce validated execution requests (burst resistance via delay or channel)
- Ensure single-flight rebalance execution (cancel obsolete work; serialize new work)
- Fetch missing data incrementally from `IDataSource` (gaps only)
- Apply atomic cache update (`Rematerialize`)
- Maintain cancellation checkpoints to preserve cache consistency

## Non-Responsibilities

- Does **not** decide whether to rebalance — decision is validated upstream by `RebalanceDecisionEngine` before this subsystem is invoked.
- Does **not** publish intents.
- Does **not** serve user requests.

## Exception Handling

Exceptions in `RebalanceExecutor` are caught by `IntentController.ProcessIntentsAsync` and reported via `ICacheDiagnostics.RebalanceExecutionFailed`. They are **never propagated to the user thread**.

> ⚠️ Always wire `RebalanceExecutionFailed` in production — it is the only signal for background execution failures. See `docs/diagnostics.md`.

## Invariants

| Invariant | Description                                                               |
|-----------|---------------------------------------------------------------------------|
| A.7       | Only `RebalanceExecutor` writes to `CacheState` (single-writer)           |
| A.8       | User path never blocks waiting for rebalance                              |
| B.12      | Cache updates are atomic (all-or-nothing via `Rematerialize`)             |
| B.13      | Consistency under cancellation: mutations discarded if cancelled          |
| B.15      | Cache contiguity maintained after every `Rematerialize`                   |
| B.16      | Obsolete results never applied (cancellation token identity check)        |
| C.21      | Serial execution: at most one active rebalance at a time                  |
| F.35      | Multiple cancellation checkpoints: before I/O, after I/O, before mutation |
| F.35a     | Cancellation-before-mutation guarantee                                    |
| F.37      | `Rematerialize` accepts arbitrary range and data (full replacement)       |
| F.38      | Incremental fetching: only missing subranges fetched                      |
| F.39      | Data preservation: existing cached data merged during expansion           |
| G.45      | I/O isolation: `IDataSource` only called on background thread             |
| H.47      | Activity counter incremented before channel write / Task.Run              |
| H.48      | Activity counter decremented in `finally` blocks                          |

See `docs/invariants.md` (Sections A, B, C, F, G, H) for full specification.

## See Also

- `docs/components/state-and-storage.md` — `CacheState` and storage strategy internals
- `docs/components/decision.md` — what validation happens before execution is enqueued
- `docs/invariants.md` — Sections B (state invariants) and F (execution invariants)
- `docs/diagnostics.md` — observing execution lifecycle events
