# Components: Infrastructure

## Overview

Infrastructure components support storage, state publication, diagnostics, and coordination.

## Motivation

Cross-cutting concerns must be explicit so that core logic stays simple and invariants remain enforceable.

## Design

### Key Components

- `CacheState<TRange, TData, TDomain>` (shared mutable state; mutated only by execution)
- `Cache<TRange, TData>` / storage strategy implementations
- `WindowCacheOptions` (public configuration)
- `ICacheDiagnostics` (optional instrumentation)
- `AsyncActivityCounter` (idle detection powering `WaitForIdleAsync`)

### Storage Strategies

Storage strategy trade-offs are documented in `docs/storage-strategies.md`. Component docs here only describe where storage plugs into the system.

### Diagnostics

Diagnostics are specified in `docs/diagnostics.md`. Component docs here only describe how diagnostics is wired and when events are emitted.

---

## Thread Safety Model

### Concurrency Philosophy

The Sliding Window Cache follows a **single consumer model** (see `docs/architecture.md`):

> A cache instance is designed for one logical consumer — one user, one access trajectory, one temporal sequence of requests. This is an ideological requirement, not merely a technical limitation.

### Key Principles

1. **Single Logical Consumer**: One cache instance = one user, one coherent access pattern
2. **Execution Serialization**: `SemaphoreSlim(1, 1)` in `RebalanceExecutor` for execution mutual exclusion; `Interlocked.Exchange` for atomic pending rebalance cancellation; no `lock` or `Monitor`
3. **Coordination Mechanism**: Single-writer architecture (User Path is read-only, only Rebalance Execution writes to `CacheState`); validation-driven cancellation (`DecisionEngine` confirms necessity then triggers cancellation); atomic updates via `Rematerialize()` (atomic array/List reference swap)

### Thread Contexts

| Component                                                                  | Thread Context | Notes                                                      |
|----------------------------------------------------------------------------|----------------|------------------------------------------------------------|
| `WindowCache`                                                              | Neutral        | Just delegates                                             |
| `UserRequestHandler`                                                       | ⚡ User Thread  | Synchronous, fast path                                     |
| `IntentController.PublishIntent()`                                         | ⚡ User Thread  | Atomic intent storage + semaphore signal (fire-and-forget) |
| `IntentController.ProcessIntentsAsync()`                                   | 🔄 Background  | Intent processing loop; invokes `DecisionEngine`           |
| `RebalanceDecisionEngine`                                                  | 🔄 Background  | CPU-only; runs in intent processing loop                   |
| `ProportionalRangePlanner`                                                 | 🔄 Background  | Invoked by `DecisionEngine`                                |
| `NoRebalanceRangePlanner`                                                  | 🔄 Background  | Invoked by `DecisionEngine`                                |
| `NoRebalanceSatisfactionPolicy`                                            | 🔄 Background  | Invoked by `DecisionEngine`                                |
| `IRebalanceExecutionController.PublishExecutionRequest()`                  | 🔄 Background  | Task-based: sync; channel-based: async await               |
| `TaskBasedRebalanceExecutionController.ChainExecutionAsync()`              | 🔄 Background  | Task chain execution (sequential)                          |
| `ChannelBasedRebalanceExecutionController.ProcessExecutionRequestsAsync()` | 🔄 Background  | Channel loop execution                                     |
| `RebalanceExecutor`                                                        | 🔄 Background  | ThreadPool, async, I/O                                     |
| `CacheDataExtensionService`                                                | Both ⚡🔄       | User Thread OR Background                                  |
| `CacheState`                                                               | Both ⚡🔄       | Shared mutable (no locks; single-writer)                   |
| Storage (`Snapshot`/`CopyOnRead`)                                          | Both ⚡🔄       | Owned by `CacheState`                                      |

**Critical:** `PublishIntent()` is a synchronous user-thread operation (atomic ops only, no decision logic). Decision logic (`DecisionEngine`, planners, policy) executes in the **background intent processing loop**. Rebalance execution (I/O) happens in a **separate background execution loop**.

### Complete Flow Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│ PHASE 1: USER THREAD (Synchronous — Fast Path)                       │
├──────────────────────────────────────────────────────────────────────┤
│ WindowCache.GetDataAsync()  — entry point (user-facing API)          │
│           ↓                                                          │
│ UserRequestHandler.HandleRequestAsync()                              │
│   • Read cache state (read-only)                                     │
│   • Fetch missing data from IDataSource (if needed)                  │
│   • Assemble result data                                             │
│   • Call IntentController.PublishIntent()                            │
│           ↓                                                          │
│ IntentController.PublishIntent()                                     │
│   • Interlocked.Exchange(_pendingIntent, intent)  (O(1))             │
│   • _activityCounter.IncrementActivity()                             │
│   • _intentSignal.Release()  (signal background loop)                │
│   • Return immediately                                               │
│           ↓                                                          │
│ Return data to user  ← USER THREAD BOUNDARY ENDS HERE                │
└──────────────────────────────────────────────────────────────────────┘
                               ↓ (semaphore signal)
┌──────────────────────────────────────────────────────────────────────┐
│ PHASE 2: BACKGROUND THREAD #1 (Intent Processing Loop)               │
├──────────────────────────────────────────────────────────────────────┤
│ IntentController.ProcessIntentsAsync()  (infinite loop)              │
│   • await _intentSignal.WaitAsync()                                  │
│   • Interlocked.Exchange(_pendingIntent, null)  → read intent        │
│           ↓                                                          │
│ RebalanceDecisionEngine.Evaluate()                                   │
│   Stage 1: Current NoRebalanceRange check  (fast-path skip)          │
│   Stage 2: Pending NoRebalanceRange check  (thrashing prevention)    │
│   Stage 3: ProportionalRangePlanner.Plan()  + NoRebalanceRangePlanner│
│   Stage 4: DesiredCacheRange == CurrentCacheRange?  (no-op skip)     │
│   Stage 5: Return Schedule decision                                  │
│           ↓                                                          │
│ If skip: continue loop (work avoidance, diagnostics event)           │
│ If execute:                                                          │
│   • lastExecutionRequest?.Cancel()                                   │
│   • IRebalanceExecutionController.PublishExecutionRequest()          │
│     └─ Task-based: Volatile.Write (synchronous)                      │
│     └─ Channel-based: await WriteAsync()                             │
└──────────────────────────────────────────────────────────────────────┘
                               ↓ (strategy-specific)
┌──────────────────────────────────────────────────────────────────────┐
│ PHASE 3: BACKGROUND EXECUTION (Strategy-Specific)                    │
├──────────────────────────────────────────────────────────────────────┤
│ TASK-BASED: ChainExecutionAsync()  (chained async method)            │
│   • await previousTask  (serial ordering)                            │
│   • await ExecuteRequestAsync()                                      │
│ OR CHANNEL-BASED: ProcessExecutionRequestsAsync()  (infinite loop)   │
│   • await foreach (channel read)  (sequential processing)            │
│           ↓                                                          │
│ ExecuteRequestAsync()  (both strategies)                             │
│   • await Task.Delay(debounce)  (cancellable)                        │
│   • Cancellation check                                               │
│           ↓                                                          │
│ RebalanceExecutor.ExecuteAsync()                                     │
│   • ct.ThrowIfCancellationRequested()  (before I/O)                  │
│   • Extend cache data via IDataSource  (async I/O)                   │
│   • ct.ThrowIfCancellationRequested()  (after I/O)                   │
│   • Trim to desired range                                            │
│   • ct.ThrowIfCancellationRequested()  (before mutation)             │
│   ┌──────────────────────────────────────┐                           │
│   │ CACHE MUTATION (SINGLE WRITER)       │                           │
│   │ • Cache.Rematerialize()              │                           │
│   │ • IsInitialized = true               │                           │
│   │ • NoRebalanceRange = desiredNRR      │                           │
│   └──────────────────────────────────────┘                           │
└──────────────────────────────────────────────────────────────────────┘
```

**Threading boundaries:**

- **User Thread Boundary**: Ends at `PublishIntent()` return. Everything before: synchronous, blocking user request. `PublishIntent()`: atomic ops only (microseconds), returns immediately.
- **Background Thread #1**: Intent processing loop. Single dedicated thread via semaphore wait. Processes intents sequentially (one at a time). CPU-only decision logic (microseconds). No I/O.
- **Background Execution**: Strategy-specific serialization. Task-based: chained async methods on ThreadPool. Channel-based: single dedicated loop via channel reader. Both: sequential (one at a time). I/O operations. SOLE writer to cache state.

### User Request Flow (step-by-step)

```
1. UserRequestHandler.HandleRequestAsync() called
2. Read from cache or fetch missing data via IDataSource  (READ-ONLY — no mutation)
3. Assemble data to return to user
4. IntentController.PublishIntent(intent)  [user thread]
   ├─ Interlocked.Exchange(_pendingIntent, intent)  — atomic, O(1)
   ├─ _activityCounter.IncrementActivity()
   └─ _intentSignal.Release()  → wakes background loop; returns immediately
5. Return assembled data to user

--- BACKGROUND (ProcessIntentsAsync) ---

6.  _intentSignal.WaitAsync() unblocks
7.  Interlocked.Exchange(_pendingIntent, null)  → reads latest intent
8.  RebalanceDecisionEngine.Evaluate()  [CPU-only, side-effect free]
    Stage 1: CurrentNoRebalanceRange check
    Stage 2: PendingNoRebalanceRange check
    Stage 3: Compute DesiredRange + DesiredNoRebalanceRange
    Stage 4: DesiredRange == CurrentRange check
    Stage 5: Schedule
9.  If validation rejects: continue loop  (work avoidance)
10. If schedule: lastRequest?.Cancel() + PublishExecutionRequest()

--- BACKGROUND EXECUTION ---

11. Debounce delay (Task.Delay)
12. RebalanceExecutor.ExecuteAsync()
    └─ I/O operations + atomic cache mutations
```

Key: Decision evaluation happens in the **background loop**, not in the user thread. The user thread only does atomic store + semaphore signal then returns immediately. This means user request bursts are handled gracefully: latest intent wins via `Interlocked.Exchange`; the decision loop processes serially with no concurrent thrashing.

### Concurrency Guarantees

- ✅ User requests NEVER block on decision evaluation
- ✅ User requests NEVER block on rebalance execution
- ✅ At most ONE decision evaluation active at a time (sequential loop)
- ✅ At most ONE rebalance execution active at a time (sequential loop + `SemaphoreSlim`)
- ✅ Cache mutations are SERIALIZED (single-writer via sequential execution)
- ✅ No race conditions on cache state (read-only User Path + single writer)
- ✅ No locks in hot path (Volatile/Interlocked only)

---

## Invariants

- Atomic cache mutation and state consistency: `docs/invariants.md` (Cache state and execution invariants).
- Activity tracking and "was idle" semantics: `docs/invariants.md` (Activity tracking invariants).

## Usage

For contributors:

- If you touch cache state publication, re-check single-writer and atomicity invariants.
- If you touch idle detection, re-check activity tracking invariants and tests.
- If you touch the intent loop or execution controllers, re-check the threading boundary described above.

## Examples

See `docs/diagnostics.md` for production instrumentation patterns.

## Edge Cases

- Storage strategy may use short critical sections internally; see `docs/storage-strategies.md`.

## Limitations

- Diagnostics should remain optional and low-overhead.
- Thread safety is guaranteed for the single-consumer model only; see `docs/architecture.md`.
