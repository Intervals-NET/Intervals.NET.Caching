# Sliding Window Cache - Complete Component Map

## Document Purpose

This document provides a comprehensive map of all components in the Sliding Window Cache, including:
- Component types (value/reference types)
- Ownership relationships
- Read/write patterns
- Data flow diagrams
- Thread safety model

**Last Updated**: February 8, 2026

---

## Table of Contents

1. [Component Statistics](#component-statistics)
2. [Component Type Legend](#component-type-legend)
3. [Component Hierarchy](#component-hierarchy)
4. [Detailed Component Catalog](#detailed-component-catalog)
5. [Ownership & Data Flow Diagram](#ownership--data-flow-diagram)
6. [Read/Write Patterns](#readwrite-patterns)
7. [Thread Safety Model](#thread-safety-model)
8. [Type Summary Tables](#type-summary-tables)

---

## Component Statistics

**Total Components**: 19 files in the codebase

**By Type**:
- 🟦 **Classes (Reference Types)**: 10
- 🟩 **Structs (Value Types)**: 3
- 🟧 **Interfaces**: 2
- 🟪 **Enums**: 1
- 🟨 **Records**: 2

**By Mutability**:
- **Immutable**: 12 components
- **Mutable**: 5 components (CacheState, IntentManager._currentIntentCts, Storage implementations)

**By Execution Context**:
- **User Thread**: 1 (UserRequestHandler)
- **Background / ThreadPool**: 4 (Scheduler, DecisionEngine, Executor, + async parts of IntentManager)
- **Both Contexts**: 1 (CacheDataFetcher)
- **Neutral**: 13 (configuration, data structures, interfaces)

**Shared Mutable State**:
- **CacheState** (shared by UserRequestHandler, RebalanceExecutor, DecisionEngine)
- No other shared mutable state

**External Dependencies**:
- **IDataSource** (user-provided implementation)
- **TDomain** (from Intervals.NET library)

---

## Component Type Legend

- **🟦 CLASS** = Reference type (heap-allocated, passed by reference)
- **🟩 STRUCT** = Value type (stack-allocated or inline, passed by value)
- **🟧 INTERFACE** = Contract definition
- **🟪 ENUM** = Value type enumeration
- **🟨 RECORD** = Reference type with value semantics

**Ownership Arrows**:
- `owns →` = Component owns/contains the other
- `reads ⊳` = Component reads from the other
- `writes ⊲` = Component writes to the other
- `uses ◇` = Component uses/depends on the other

**Mutability Indicators**:
- ✏️ = Mutable field/property
- 🔒 = Readonly/immutable
- ⚠️ = Mutable shared state (requires coordination)

---

## Component Hierarchy

### Public API Layer

```
🟦 WindowCache<TRange, TData, TDomain>                    [Public Facade]
│
├── owns → 🟦 UserRequestHandler<TRange, TData, TDomain>
│
└── composes (at construction):
    ├── 🟦 CacheState<TRange, TData, TDomain>              ⚠️ Shared Mutable
    ├── 🟦 IntentController<TRange, TData, TDomain>
    │   └── owns → 🟦 RebalanceScheduler<TRange, TData, TDomain>
    ├── 🟦 RebalanceDecisionEngine<TRange, TDomain>
    │   ├── owns → 🟩 ThresholdRebalancePolicy<TRange, TDomain>
    │   └── owns → 🟩 ProportionalRangePlanner<TRange, TDomain>
    ├── 🟦 RebalanceExecutor<TRange, TData, TDomain>
    └── 🟦 CacheDataFetcher<TRange, TData, TDomain>
        └── uses → 🟧 IDataSource<TRange, TData> (user-provided)
```

---

## Detailed Component Catalog

### 1. Configuration & Data Transfer Types

#### 🟨 WindowCacheOptions
```csharp
public record WindowCacheOptions
```

**File**: `src/SlidingWindowCache/Configuration/WindowCacheOptions.cs`

**Type**: Record (reference type with value semantics)

**Properties** (all readonly):
- `double LeftCacheSize` - Coefficient for left cache size (≥0)
- `double RightCacheSize` - Coefficient for right cache size (≥0)
- `double? LeftThreshold` - Left rebalance threshold percentage (optional, ≥0)
- `double? RightThreshold` - Right rebalance threshold percentage (optional, ≥0)
- `TimeSpan DebounceDelay` - Debounce delay for rebalance operations (default: 100ms)
- `UserCacheReadMode ReadMode` - Cache read strategy (Snapshot or CopyOnRead)

**Ownership**: Created by user, passed to WindowCache constructor

**Mutability**: Immutable (init-only properties)

**Lifetime**: Lives as long as cache instance

**Used by**: 
- WindowCache (constructor)
- ThresholdRebalancePolicy (threshold configuration)
- ProportionalRangePlanner (size configuration)

---

#### 🟪 UserCacheReadMode
```csharp
public enum UserCacheReadMode
```

**File**: `src/SlidingWindowCache/UserCacheReadMode.cs`

**Type**: Enum (value type)

**Values**:
- `Snapshot` - Zero-allocation reads, expensive rebalance (uses array)
- `CopyOnRead` - Allocation on reads, cheap rebalance (uses List)

**Ownership**: Part of WindowCacheOptions

**Mutability**: Immutable

**Used by**: 
- WindowCacheOptions
- ICacheStorage implementations (determines storage strategy)

**Trade-offs**:
- **Snapshot**: Fast reads, slow rebalance, LOH pressure for large caches
- **CopyOnRead**: Slow reads, fast rebalance, better memory pressure

---

#### 🟧 IDataSource<TRangeType, TDataType>
```csharp
public interface IDataSource<TRangeType, TDataType>
    where TRangeType : IComparable<TRangeType>
```

**File**: `src/SlidingWindowCache/IDataSource.cs`

**Type**: Interface (contract)

**Methods**:
- `Task<IEnumerable<TDataType>> FetchAsync(Range<TRangeType> range, CancellationToken ct)`
  - Required: Fetch data for a single range
- `Task<IEnumerable<RangeChunk<TRangeType, TDataType>>> FetchAsync(IEnumerable<Range<TRangeType>> ranges, CancellationToken ct)`
  - Optional override: Batch fetch optimization

**Ownership**: User provides implementation

**Used by**: CacheDataFetcher (calls to fetch external data)

**Operations**: Read-only (fetches external data)

**Characteristics**:
- User-implemented
- May perform I/O (network, disk, database)
- Should respect CancellationToken
- Default batch implementation uses parallel fetch

---

#### 🟨 RangeChunk<TRangeType, TDataType>
```csharp
public record RangeChunk<TRangeType, TDataType>(Range<TRangeType> Range, IEnumerable<TDataType> Data)
    where TRangeType : IComparable<TRangeType>
```

**File**: `src/SlidingWindowCache/DTO/RangeChunk.cs`

**Type**: Record (reference type, immutable)

**Properties**:
- `Range<TRangeType> Range` - The range covered by this chunk
- `IEnumerable<TDataType> Data` - The data for this range

**Ownership**: Created by IDataSource, consumed by CacheDataFetcher

**Mutability**: Immutable

**Lifetime**: Temporary (method return value)

**Purpose**: Encapsulates data fetched for a particular range (batch fetch result)

---

### 2. Storage Layer

#### 🟧 ICacheStorage<TRange, TData, TDomain>
```csharp
internal interface ICacheStorage<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
```

**File**: `src/SlidingWindowCache/Storage/ICacheStorage.cs`

**Type**: Interface (internal)

**Properties**:
- `UserCacheReadMode Mode { get; }` - The read mode this strategy implements
- `Range<TRange> Range { get; }` - Current range of cached data

**Methods**:
- `void Rematerialize(RangeData<TRange, TData, TDomain> rangeData)` ⊲ **WRITE**
  - Replaces internal storage with new range data
  - Called during cache initialization and rebalancing
- `ReadOnlyMemory<TData> Read(Range<TRange> range)` ⊳ **READ**
  - Returns data for the specified range
  - Behavior varies by implementation (zero-copy vs. copy)
- `RangeData<TRange, TData, TDomain> ToRangeData()` ⊳ **READ**
  - Converts current state to RangeData representation

**Implementations**:
- `SnapshotReadStorage<TRange, TData, TDomain>`
- `CopyOnReadStorage<TRange, TData, TDomain>`

**Owned by**: CacheState

**Writers**: UserRequestHandler, RebalanceExecutor (via CacheState)

**Readers**: UserRequestHandler, RebalanceExecutor

---

#### 🟦 SnapshotReadStorage<TRange, TData, TDomain>
```csharp
internal sealed class SnapshotReadStorage<TRange, TData, TDomain> : ICacheStorage<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/Storage/SnapshotReadStorage.cs`

**Type**: Class (sealed)

**Fields**:
- `TDomain _domain` (readonly) - Domain for range calculations
- ✏️ `TData[] _storage` - Mutable array holding cached data
- ✏️ `Range<TRange> Range` (property) - Current cache range

**Operations**:
- `Rematerialize()` ⊲ **WRITE**
  - Allocates new array
  - Replaces `_storage` completely
  - Updates `Range`
- `Read()` ⊳ **READ**
  - Returns `ReadOnlyMemory<TData>` view over internal array
  - **Zero allocation** (slice of existing array)
- `ToRangeData()` ⊳ **READ**
  - Creates RangeData from current array

**Characteristics**:
- ✅ Zero-allocation reads (fast)
- ❌ Expensive rebalance (always allocates new array)
- ⚠️ Large arrays may end up on LOH (≥85KB)

**Ownership**: Owned by CacheState (single instance)

**Internal State**: `TData[]` array (mutable, replaced atomically)

**Thread Safety**: Not thread-safe (single consumer model)

**Best for**: Read-heavy workloads, predictable memory patterns

---

#### 🟦 CopyOnReadStorage<TRange, TData, TDomain>
```csharp
internal sealed class CopyOnReadStorage<TRange, TData, TDomain> : ICacheStorage<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/Storage/CopyOnReadStorage.cs`

**Type**: Class (sealed)

**Fields**:
- `TDomain _domain` (readonly) - Domain for range calculations
- ✏️ `List<TData> _activeStorage` - Active storage (immutable during reads)
- ✏️ `List<TData> _stagingBuffer` - Staging buffer (write-only during rematerialization)
- ✏️ `Range<TRange> Range` (property) - Current cache range

**Staging Buffer Pattern**:
- Two internal buffers: active storage + staging buffer
- Active storage never mutated during enumeration
- Staging buffer cleared, filled, then swapped with active
- Buffers may grow but never shrink (capacity reuse)

**Operations**:
- `Rematerialize()` ⊲ **WRITE**
  - Clears staging buffer (preserves capacity)
  - Enumerates range data into staging (single-pass)
  - Atomically swaps staging ↔ active
  - Updates `Range`
- `Read()` ⊳ **READ**
  - Allocates new `TData[]` array
  - Copies from active storage
  - Returns as `ReadOnlyMemory<TData>`
- `ToRangeData()` ⊳ **READ**
  - Returns lazy enumerable over active storage
  - Safe because active storage is immutable during reads

**Characteristics**:
- ✅ Cheap rematerialization (amortized O(1) when capacity sufficient)
- ❌ Expensive reads (allocates + copies)
- ✅ Correct enumeration (staging buffer prevents corruption)
- ✅ No LOH pressure (List growth strategy)
- ✅ Satisfies Invariants A.3.8, A.3.9a, B.11-12

**Ownership**: Owned by CacheState (single instance)

**Internal State**: Two `List<TData>` (swapped atomically)

**Thread Safety**: Not thread-safe (single consumer model)

**Best for**: Rematerialization-heavy workloads, large sliding windows, background cache layers

**See**: [Storage Strategies Guide](STORAGE_STRATEGIES.md) for detailed comparison and usage scenarios

---

### 3. State Management

#### 🟦 CacheState<TRange, TData, TDomain>
```csharp
internal sealed class CacheState<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
```

**File**: `src/SlidingWindowCache/CacheState.cs`

**Type**: Class (sealed)

**Properties** (all mutable):
- ✏️ `ICacheStorage<TRange, TData, TDomain> Cache { get; }` - The actual cache storage
- ✏️ `Range<TRange>? LastRequested { get; set; }` - Last requested range by user
- ✏️ `Range<TRange>? NoRebalanceRange { get; set; }` - Range within which no rebalancing occurs
- 🔒 `TDomain Domain { get; }` - Domain for range calculations (readonly)

**Ownership**: 
- Created by WindowCache constructor
- **Shared by reference** across multiple components

**Shared with** (read/write):
- **UserRequestHandler** ⊲⊳
  - Reads: `Cache.Range`, `Cache.Read()`, `Cache.ToRangeData()`
  - Writes: `Cache.Rematerialize()`, `LastRequested`
- **RebalanceExecutor** ⊲⊳
  - Reads: `Cache.Range`, `Cache.ToRangeData()`
  - Writes: `Cache.Rematerialize()`, `NoRebalanceRange`
- **RebalanceScheduler** ⊳ (via DecisionEngine)
  - Reads: `NoRebalanceRange`

**Characteristics**:
- ⚠️ **Mutable shared state** (central coordination point)
- ❌ **No internal locking** (single consumer model by design)
- ✅ **Atomic operations** (Rematerialize replaces storage atomically)

**Thread Safety**: 
- Not thread-safe (intentional)
- Coordination via CancellationToken
- User Path cancels rebalance before mutations

**Role**: Central point for cache data and metadata

---

### 4. User Path (Fast Path)

#### 🟦 UserRequestHandler<TRange, TData, TDomain>
```csharp
internal sealed class UserRequestHandler<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/UserPath/UserRequestHandler.cs`

**Type**: Class (sealed)

**Fields** (all readonly):
- `CacheState<TRange, TData, TDomain> _state`
- `CacheDataFetcher<TRange, TData, TDomain> _cacheFetcher`
- `IntentController<TRange, TData, TDomain> _intentManager`

**Main Method**:
```csharp
public async ValueTask<ReadOnlyMemory<TData>> HandleRequestAsync(
    Range<TRange> requestedRange,
    CancellationToken cancellationToken)
```

**Operation Flow**:
1. **Cancel pending rebalance** - `_intentManager.CancelPendingRebalance()`
2. **Check cache coverage** - `_state.Cache.Range.Contains(requestedRange)`
3. **Extend if needed** - `_cacheFetcher.ExtendCacheAsync()` + `_state.Cache.Rematerialize()`
4. **Update metadata** - `_state.LastRequested = requestedRange`
5. **Trigger rebalance** - `_intentManager.PublishIntent(requestedRange)` (fire-and-forget)
6. **Return data** - `_state.Cache.Read(requestedRange)`

**Reads from**:
- ⊳ `_state.Cache` (Range, Read, ToRangeData)

**Writes to**:
- ⊲ `_state.Cache` (via Rematerialize - expands to cover requested range)
- ⊲ `_state.LastRequested`

**Uses**:
- ◇ `_cacheFetcher` (to fetch missing data)
- ◇ `_intentManager` (PublishIntent, CancelPendingRebalance)

**Characteristics**:
- ✅ Executes in **User Thread** (synchronous)
- ✅ Always serves user requests (never waits for rebalance)
- ✅ May expand cache to cover requested range
- ✅ Always triggers rebalance intent
- ❌ **Never** trims or normalizes cache
- ❌ **Never** invokes decision logic
- ❌ **Never** blocks on rebalance

**Ownership**: Owned by WindowCache

**Execution Context**: User Thread (synchronous)

**Responsibilities**: Serve user requests fast, trigger rebalance intents

**Invariants Enforced**:
- A.1-0a: Cancels rebalance before cache mutations
- 1: Always serves user requests
- 2: Never waits for rebalance execution
- 3: Sole source of rebalance intent
- 10: Always returns exactly RequestedRange

---

### 5. Rebalance System - Intent Management

#### 🟦 IntentController<TRange, TData, TDomain>
```csharp
internal sealed class IntentController<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/CacheRebalance/IntentController.cs`

**Type**: Class (sealed)

**Role**: Intent Controller (component 1 of 2 in Rebalance Intent Manager actor)

**Fields**:
- `RebalanceScheduler<TRange, TData, TDomain> _scheduler` (readonly)
- ✏️ `CancellationTokenSource? _currentIntentCts` - **Mutable**, tracks current intent

**Key Methods**:

**`PublishIntent(Range<TRange> requestedRange)`**:
```csharp
public void PublishIntent(Range<TRange> requestedRange)
{
    // 1. Invalidate previous intent
    _currentIntentCts?.Cancel();
    _currentIntentCts?.Dispose();
    
    // 2. Create new intent identity
    _currentIntentCts = new CancellationTokenSource();
    var intentToken = _currentIntentCts.Token;
    
    // 3. Delegate to scheduler
    _scheduler.ScheduleRebalance(requestedRange, intentToken);
}
```

**`CancelPendingRebalance()`**:
```csharp
public void CancelPendingRebalance()
{
    if (_currentIntentCts != null)
    {
        _currentIntentCts.Cancel();
        _currentIntentCts.Dispose();
        _currentIntentCts = null;
    }
}
```

**Characteristics**:
- ✅ Owns intent identity (CancellationTokenSource lifecycle)
- ✅ Single-flight enforcement (only one active intent)
- ✅ Exposes cancellation to User Path
- ⚠️ **Intent does not guarantee execution** - execution is opportunistic
- ❌ **Does NOT**: Timing, scheduling, execution logic

**Ownership**: 
- Owned by WindowCache
- Composes with RebalanceScheduler

**Execution Context**: 
- Synchronous methods (called from User Thread)
- Scheduled work executes in Background

**State**: 
- `_currentIntentCts` (mutable, nullable)
- Represents identity of latest intent

**Responsibilities**: 
- Intent lifecycle management
- Cancellation coordination
- Identity versioning

**Invariants Enforced**:
- C.17: At most one active intent
- C.18: Previous intents become obsolete
- C.24: Intent does not guarantee execution

---

#### 🟦 RebalanceScheduler<TRange, TData, TDomain>
```csharp
internal sealed class RebalanceScheduler<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/CacheRebalance/RebalanceScheduler.cs`

**Type**: Class (sealed)

**Role**: Execution Scheduler (component 2 of 2 in Rebalance Intent Manager actor)

**Fields** (all readonly):
- `CacheState<TRange, TData, TDomain> _state`
- `RebalanceDecisionEngine<TRange, TDomain> _decisionEngine`
- `RebalanceExecutor<TRange, TData, TDomain> _executor`
- `TimeSpan _debounceDelay`

**Key Methods**:

**`ScheduleRebalance(Range<TRange> requestedRange, CancellationToken intentToken)`**:
```csharp
public void ScheduleRebalance(Range<TRange> requestedRange, CancellationToken intentToken)
{
    // Fire-and-forget: schedule execution in background thread pool
    Task.Run(async () =>
    {
        try
        {
            // Debounce delay
            await Task.Delay(_debounceDelay, intentToken);
            
            // Intent validity check
            if (intentToken.IsCancellationRequested)
                return;
            
            // Execute pipeline
            await ExecutePipelineAsync(requestedRange, intentToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when intent is cancelled
        }
    }, intentToken);
}
```

**`ExecutePipelineAsync(Range<TRange> requestedRange, CancellationToken cancellationToken)`** (private):
```csharp
private async Task ExecutePipelineAsync(...)
{
    // Final cancellation check
    if (cancellationToken.IsCancellationRequested)
        return;
    
    // Step 1: Decision logic
    var decision = _decisionEngine.ShouldExecuteRebalance(
        requestedRange, _state.NoRebalanceRange);
    
    // Step 2: If skip, return early
    if (!decision.ShouldExecute)
        return;
    
    // Step 3: Execute if allowed
    await _executor.ExecuteAsync(decision.DesiredRange!.Value, cancellationToken);
}
```

**Characteristics**:
- ✅ Executes in **Background / ThreadPool**
- ✅ Handles debounce delay
- ✅ Orchestrates Decision → Execution pipeline
- ✅ Checks intent validity before execution
- ✅ Ensures single-flight through cancellation
- ❌ **Does NOT**: Intent identity, cancellation management

**Ownership**: Owned by IntentController

**Execution Context**: Background / ThreadPool

**State**: **Stateless** (only readonly fields)

**Important Design Note**: RebalanceScheduler is intentionally stateless and does not own intent identity.
All intent lifecycle, superseding, and cancellation semantics are delegated to the Intent Controller (IntentController).
The scheduler receives a CancellationToken for each execution and simply checks its validity.

**State**: Stateless (only readonly fields)

**Responsibilities**:
- Timing and debounce
- Pipeline orchestration
- Validity checking

**Invariants Enforced**:
- C.20: Obsolete intents don't start execution
- C.21: At most one execution active (via cancellation)

---

### 6. Rebalance System - Decision & Policy

#### 🟦 RebalanceDecisionEngine<TRange, TDomain>
```csharp
internal sealed class RebalanceDecisionEngine<TRange, TDomain>
```

**File**: `src/SlidingWindowCache/CacheRebalance/RebalanceDecisionEngine.cs`

**Type**: Class (sealed)

**Role**: Pure Decision Logic

**Fields** (all readonly, value types):
- `ThresholdRebalancePolicy<TRange, TDomain> _policy` (struct, copied)
- `ProportionalRangePlanner<TRange, TDomain> _planner` (struct, copied)

**Key Method**:
```csharp
public RebalanceDecision<TRange> ShouldExecuteRebalance(
    Range<TRange> requestedRange,
    Range<TRange>? noRebalanceRange)
{
    // Decision Path D1: Check NoRebalanceRange (fast path)
    if (noRebalanceRange.HasValue && 
        !_policy.ShouldRebalance(noRebalanceRange.Value, requestedRange))
    {
        return RebalanceDecision<TRange>.Skip();
    }
    
    // Decision Path D2/D3: Compute DesiredCacheRange
    var desiredRange = _planner.Plan(requestedRange);
    
    return RebalanceDecision<TRange>.Execute(desiredRange);
}
```

**Characteristics**:
- ✅ **Pure function** (no side effects)
- ✅ **Deterministic** (same inputs → same outputs)
- ✅ **Stateless** (composes value-type policies)
- ✅ Invoked only in background
- ❌ Not visible to User Path

**Uses**:
- ◇ `_policy.ShouldRebalance()` - check NoRebalanceRange containment
- ◇ `_planner.Plan()` - compute DesiredCacheRange

**Returns**: `RebalanceDecision<TRange>` (struct)

**Ownership**: Owned by WindowCache, used by RebalanceScheduler

**Execution Context**: Background / ThreadPool

**Responsibilities**: 
- Evaluate if rebalance is needed
- Check NoRebalanceRange
- Compute DesiredCacheRange

**Invariants Enforced**:
- 24: Decision path is purely analytical
- 25: Never mutates cache state
- 26: No rebalance if inside NoRebalanceRange
- 27: No rebalance if DesiredCacheRange == CurrentCacheRange

---

#### 🟩 ThresholdRebalancePolicy<TRange, TDomain>
```csharp
internal readonly struct ThresholdRebalancePolicy<TRange, TDomain>
```

**File**: `src/SlidingWindowCache/CacheRebalance/Policy/ThresholdRebalancePolicy.cs`

**Type**: Struct (readonly value type)

**Role**: Cache Geometry Policy - Threshold Rules (component 1 of 2)

**Fields** (all readonly):
- `WindowCacheOptions _options`
- `TDomain _domain`

**Key Methods**:

**`ShouldRebalance(Range<TRange> noRebalanceRange, Range<TRange> requested)`**:
```csharp
public bool ShouldRebalance(Range<TRange> noRebalanceRange, Range<TRange> requested)
    => !noRebalanceRange.Contains(requested);
```

**`GetNoRebalanceRange(Range<TRange> cacheRange)`**:
```csharp
public Range<TRange>? GetNoRebalanceRange(Range<TRange> cacheRange)
    => cacheRange.ExpandByRatio(
        domain: _domain,
        leftRatio: -(_options.LeftThreshold ?? 0),  // Negate to shrink
        rightRatio: -(_options.RightThreshold ?? 0)  // Negate to shrink
    );
```

**Characteristics**:
- ✅ **Value type** (struct, passed by value)
- ✅ **Pure functions** (no state mutation)
- ✅ **Configuration-driven** (uses WindowCacheOptions)
- ✅ **Stateless** (readonly fields)

**Ownership**: Value type, copied into RebalanceDecisionEngine and RebalanceExecutor

**Execution Context**: Background / ThreadPool

**Responsibilities**:
- Compute NoRebalanceRange (shrinks cache by threshold ratios)
- Check if requested range falls outside no-rebalance zone
- Answers: **"When to rebalance"**

**Invariants Enforced**:
- 26: No rebalance if inside NoRebalanceRange
- 33: NoRebalanceRange derived from CurrentCacheRange + config

---

#### 🟩 ProportionalRangePlanner<TRange, TDomain>
```csharp
internal readonly struct ProportionalRangePlanner<TRange, TDomain>
```

**File**: `src/SlidingWindowCache/DesiredRangePlanner/ProportionalRangePlanner.cs`

**Type**: Struct (readonly value type)

**Role**: Cache Geometry Policy - Shape Planning (component 2 of 2)

**Fields** (all readonly):
- `WindowCacheOptions _options`
- `TDomain _domain`

**Key Method**:
```csharp
public Range<TRange> Plan(Range<TRange> requested)
{
    var size = requested.Span(_domain);
    
    var left = size.Value * _options.LeftCacheSize;
    var right = size.Value * _options.RightCacheSize;
    
    return requested.Expand(
        domain: _domain,
        left: (long)left,
        right: (long)right
    );
}
```

**Characteristics**:
- ✅ **Value type** (struct, passed by value)
- ✅ **Pure function** (no state)
- ✅ **Configuration-driven** (uses WindowCacheOptions)
- ✅ **Independent of current cache contents**
- ✅ **Stateless** (readonly fields)

**Ownership**: Value type, copied into RebalanceDecisionEngine

**Execution Context**: Background / ThreadPool

**Responsibilities**:
- Compute DesiredCacheRange (expands requested by left/right coefficients)
- Define canonical cache geometry
- Answers: **"What shape to target"**

**Invariants Enforced**:
- 29: DesiredCacheRange computed from RequestedRange + config
- 30: Independent of current cache contents
- 31: Canonical target cache state
- 32: Sliding window geometry defined by configuration

---

#### 🟩 RebalanceDecision<TRange>
```csharp
internal readonly struct RebalanceDecision<TRange>
    where TRange : IComparable<TRange>
```

**File**: `src/SlidingWindowCache/CacheRebalance/RebalanceDecision.cs`

**Type**: Struct (readonly value type)

**Properties** (all readonly):
- `bool ShouldExecute` - Whether rebalance should proceed
- `Range<TRange>? DesiredRange` - Target cache range (if executing)

**Factory Methods**:
- `static Skip()` → Returns decision to skip rebalance
- `static Execute(Range<TRange> desiredRange)` → Returns decision to execute with target range

**Characteristics**:
- ✅ **Value type** (struct)
- ✅ **Immutable**
- ✅ Represents decision outcome

**Ownership**: Created by RebalanceDecisionEngine, consumed by RebalanceScheduler

**Mutability**: Immutable

**Lifetime**: Temporary (local variable in pipeline)

**Purpose**: Encapsulates decision result (skip or execute with target range)

---

### 7. Rebalance System - Execution

#### 🟦 RebalanceExecutor<TRange, TData, TDomain>
```csharp
internal sealed class RebalanceExecutor<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/CacheRebalance/Executor/RebalanceExecutor.cs`

**Type**: Class (sealed)

**Role**: Mutating Actor (sole component responsible for cache normalization)

**Fields** (all readonly):
- `CacheState<TRange, TData, TDomain> _state`
- `CacheDataFetcher<TRange, TData, TDomain> _cacheFetcher`
- `ThresholdRebalancePolicy<TRange, TDomain> _rebalancePolicy`

**Key Method**:
```csharp
public async Task ExecuteAsync(Range<TRange> desiredRange, CancellationToken cancellationToken)
{
    // Get current cache snapshot
    var rangeData = _state.Cache.ToRangeData();
    
    // Check if already at desired state (Decision Path D2)
    if (rangeData.Range == desiredRange)
        return;
    
    // Cancellation check before I/O
    cancellationToken.ThrowIfCancellationRequested();
    
    // Phase 1: Extend cache to cover desired range
    var extended = await _cacheFetcher.ExtendCacheAsync(rangeData, desiredRange, cancellationToken);
    
    // Cancellation check after I/O
    cancellationToken.ThrowIfCancellationRequested();
    
    // Phase 2: Trim to desired range
    var rebalanced = extended[desiredRange];
    
    // Cancellation check before mutation
    cancellationToken.ThrowIfCancellationRequested();
    
    // Phase 3: Update cache (atomic mutation)
    _state.Cache.Rematerialize(rebalanced);
    
    // Phase 4: Update no-rebalance range
    _state.NoRebalanceRange = _rebalancePolicy.GetNoRebalanceRange(_state.Cache.Range);
}
```

**Reads from**:
- ⊳ `_state.Cache` (ToRangeData, Range)

**Writes to**:
- ⊲ `_state.Cache` (via Rematerialize - normalizes to DesiredCacheRange)
- ⊲ `_state.NoRebalanceRange`

**Uses**:
- ◇ `_cacheFetcher.ExtendCacheAsync()` (fetch missing data)
- ◇ `_rebalancePolicy.GetNoRebalanceRange()` (compute new threshold zone)

**Characteristics**:
- ✅ Executes in **Background / ThreadPool**
- ✅ **Asynchronous** (performs I/O operations)
- ✅ **Cancellable** (checks token at multiple points)
- ✅ **Sole component** responsible for cache normalization
- ✅ Expands to DesiredCacheRange
- ✅ Trims excess data
- ✅ Updates NoRebalanceRange

**Ownership**: Owned by WindowCache, used by RebalanceScheduler

**Execution Context**: Background / ThreadPool

**Operations**: Mutates cache atomically (expand, trim, update metadata)

**Invariants Enforced**:
- 4: Rebalance is asynchronous
- 34: Supports cancellation at all stages
- 34a: Yields to User Path immediately upon cancellation
- 34b: Cancelled execution doesn't corrupt state
- 35: Only path responsible for cache normalization
- 35a: Mutates only for normalization (expand, trim, recompute NoRebalanceRange)
- 39-41: Upon completion, cache matches DesiredCacheRange

---

#### 🟦 CacheDataFetcher<TRange, TData, TDomain>
```csharp
internal sealed class CacheDataFetcher<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/CacheRebalance/Executor/CacheDataFetcher.cs`

**Type**: Class (sealed)

**Role**: Data Fetcher (used by both User Path and Rebalance Path)

**Fields** (all readonly):
- `IDataSource<TRange, TData> _dataSource` (user-provided)
- `TDomain _domain`

**Key Method**:
```csharp
public async Task<RangeData<TRange, TData, TDomain>> ExtendCacheAsync(
    RangeData<TRange, TData, TDomain> current,
    Range<TRange> requested,
    CancellationToken ct)
{
    // Step 1: Calculate missing ranges
    var missingRanges = CalculateMissingRanges(current.Range, requested);
    
    // Step 2: Fetch missing data from data source
    var fetchedResults = await _dataSource.FetchAsync(missingRanges, ct);
    
    // Step 3: Union fetched data with current cache
    return UnionAll(current, fetchedResults, _domain);
}
```

**Uses**:
- ◇ `_dataSource.FetchAsync()` - external I/O to fetch data

**Characteristics**:
- ✅ Calls external IDataSource
- ✅ Performs I/O operations
- ✅ Merges data **without trimming**
- ✅ Optimizes partial cache hits (only fetches missing ranges)
- ✅ **Shared by both paths**

**Ownership**: Owned by WindowCache, shared by UserRequestHandler and RebalanceExecutor

**Execution Context**: 
- User Thread (when called by UserRequestHandler)
- Background / ThreadPool (when called by RebalanceExecutor)

**External Dependencies**: IDataSource (user-provided)

**Operations**: 
- Fetches missing data
- Merges with existing cache
- **Never trims**

**Shared by**:
- UserRequestHandler (expand to cover requested range)
- RebalanceExecutor (expand to cover desired range)

---

### 8. Public Facade

#### 🟦 WindowCache<TRange, TData, TDomain>
```csharp
public sealed class WindowCache<TRange, TData, TDomain> : IWindowCache<TRange, TData, TDomain>
```

**File**: `src/SlidingWindowCache/WindowCache.cs`

**Type**: Class (sealed, public)

**Role**: Public Facade, Composition Root

**Fields**:
- `UserRequestHandler<TRange, TData, TDomain> _userRequestHandler` (readonly, private)

**Constructor**: Creates and wires all internal components:
```csharp
public WindowCache(
    IDataSource<TRange, TData> dataSource,
    TDomain domain,
    WindowCacheOptions options)
{
    var cacheStorage = CreateCacheStorage(domain, options);
    var state = new CacheState<TRange, TData, TDomain>(cacheStorage, domain);
    
    var rebalancePolicy = new ThresholdRebalancePolicy<TRange, TDomain>(options, domain);
    var rangePlanner = new ProportionalRangePlanner<TRange, TDomain>(options, domain);
    var cacheFetcher = new CacheDataFetcher<TRange, TData, TDomain>(dataSource, domain);
    
    var decisionEngine = new RebalanceDecisionEngine<TRange, TDomain>(rebalancePolicy, rangePlanner);
    var executor = new RebalanceExecutor<TRange, TData, TDomain>(state, cacheFetcher, rebalancePolicy);
    
    var intentManager = new IntentController<TRange, TData, TDomain>(
        state, decisionEngine, executor, options.DebounceDelay);
    
    _userRequestHandler = new UserRequestHandler<TRange, TData, TDomain>(
        state, cacheFetcher, intentManager);
}
```

**Public API**:
```csharp
public ValueTask<ReadOnlyMemory<TData>> GetDataAsync(
    Range<TRange> requestedRange,
    CancellationToken cancellationToken)
{
    return _userRequestHandler.HandleRequestAsync(requestedRange, cancellationToken);
}
```

**Characteristics**:
- ✅ **Pure facade** (no business logic)
- ✅ **Composition root** (wires all components)
- ✅ **Public API** (single entry point)
- ✅ **Delegates everything** to UserRequestHandler

**Ownership**: 
- Owns all internal components
- Created by user
- Lives for application lifetime

**Execution Context**: Neutral (just delegates)

**Responsibilities**:
- Expose public API
- Wire internal components together
- Own configuration and lifecycle

**Does NOT**:
- Implement business logic
- Directly access cache state
- Perform decision logic

---

## Ownership & Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│  USER (Consumer)                                                    │
└─────────────────────────────────────────────────────────────────────┘
                    │
                    │ GetDataAsync(range, ct)
                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│  WindowCache<TRange, TData, TDomain>  [Public Facade]              │
│  🟦 CLASS (sealed, public)                                          │
│                                                                      │
│  Constructor creates and wires:                                     │
│   ├─ 🟦 CacheState ──────────────────────────┐ (shared mutable)   │
│   ├─ 🟦 UserRequestHandler ──────────────────┼───┐                 │
│   ├─ 🟦 CacheDataFetcher ────────────────────┼───┼───┐             │
│   ├─ 🟦 RebalanceIntentManager ──────────────┼───┼───┼───┐         │
│   │   └─ 🟦 RebalanceScheduler ──────────────┼───┼───┼───┼───┐     │
│   ├─ 🟦 RebalanceDecisionEngine ─────────────┼───┼───┼───┼───┼───┐ │
│   │   ├─ 🟩 ThresholdRebalancePolicy         │   │   │   │   │   │ │
│   │   └─ 🟩 ProportionalRangePlanner          │   │   │   │   │   │ │
│   └─ 🟦 RebalanceExecutor ────────────────────┼───┼───┼───┼───┼───┤ │
│                                                │   │   │   │   │   │ │
│  GetDataAsync() → delegates to UserRequestHandler                   │
└────────────────────────────────────────────────┼───┼───┼───┼───┼───┼─┘
                                                 │   │   │   │   │   │
        ═════════════════════════════════════════╪═══╪═══╪═══╪═══╪═══╪═
        USER THREAD                              │   │   │   │   │   │
        ═════════════════════════════════════════╪═══╪═══╪═══╪═══╪═══╪═
                                                 │   │   │   │   │   │
┌────────────────────────────────────────────────▼───┼───┼───┼───┼───┤
│  UserRequestHandler  [Fast Path Actor]             │   │   │   │   │
│  🟦 CLASS (sealed)                                  │   │   │   │   │
│                                                     │   │   │   │   │
│  HandleRequestAsync(range, ct):                    │   │   │   │   │
│   1. _intentManager.CancelPendingRebalance() ──────┼───┼───┼───┼───┤
│   2. Check if cache covers range ──────────────────┼───┤   │   │   │
│   3. If not: _cacheFetcher.ExtendCacheAsync() ─────┼───┼───┤   │   │
│   4. If not: _state.Cache.Rematerialize() ─────────┼───┤   │   │   │
│   5. _state.LastRequested = range ─────────────────┼───┤   │   │   │
│   6. _intentManager.PublishIntent(range) ───────────┼───┼───┼───┼───┤
│   7. return _state.Cache.Read(range) ───────────────┼───┤   │   │   │
└─────────────────────────────────────────────────────┼───┼───┼───┼───┘
                                                      │   │   │   │
        ══════════════════════════════════════════════╪═══╪═══╪═══╪═══
        BACKGROUND / THREADPOOL                       │   │   │   │
        ══════════════════════════════════════════════╪═══╪═══╪═══╪═══
                                                      │   │   │   │
┌─────────────────────────────────────────────────────▼───┼───┼───┼───┐
│  RebalanceIntentManager  [Intent Controller]            │   │   │   │
│  🟦 CLASS (sealed)                                       │   │   │   │
│                                                          │   │   │   │
│  Fields:                                                 │   │   │   │
│   ├─ RebalanceScheduler _scheduler ─────────────────────▼───┼───┤   │
│   └─ CancellationTokenSource? _currentIntentCts ◄───────────┤   │   │
│                                                              │   │   │
│  PublishIntent(range):                                       │   │   │
│   1. Cancel & dispose old _currentIntentCts                  │   │   │
│   2. Create new CancellationTokenSource                      │   │   │
│   3. _scheduler.ScheduleRebalance(range, token) ─────────────┼───┤   │
│                                                              │   │   │
│  CancelPendingRebalance():                                   │   │   │
│   1. Cancel & dispose _currentIntentCts                      │   │   │
└──────────────────────────────────────────────────────────────┼───┼───┘
                                                               │   │
┌──────────────────────────────────────────────────────────────▼───┼───┐
│  RebalanceScheduler  [Execution Scheduler]                       │   │
│  🟦 CLASS (sealed)                                                │   │
│                                                                   │   │
│  ScheduleRebalance(range, intentToken):                          │   │
│   Task.Run(async () => {                                         │   │
│     await Task.Delay(_debounceDelay, intentToken);               │   │
│     if (!intentToken.IsCancellationRequested)                    │   │
│       await ExecutePipelineAsync(range, intentToken); ───────────┼───┤
│   });                                                             │   │
│                                                                   │   │
│  ExecutePipelineAsync(range, ct):                                │   │
│   1. Check cancellation                                          │   │
│   2. decision = _decisionEngine.ShouldExecuteRebalance() ────────┼───┤
│   3. if (decision.ShouldExecute)                                 │   │
│        await _executor.ExecuteAsync(desiredRange, ct); ──────────┼───┤
└───────────────────────────────────────────────────────────────────┼───┘
                                                                    │
┌───────────────────────────────────────────────────────────────────▼───┐
│  RebalanceDecisionEngine  [Pure Decision Logic]                       │
│  🟦 CLASS (sealed)                                                     │
│                                                                        │
│  Fields (value types):                                                │
│   ├─ 🟩 ThresholdRebalancePolicy _policy                              │
│   └─ 🟩 ProportionalRangePlanner _planner                             │
│                                                                        │
│  ShouldExecuteRebalance(requested, noRebalanceRange):                 │
│   1. Check if _policy.ShouldRebalance() → may skip                    │
│   2. desiredRange = _planner.Plan(requested)                          │
│   3. return Execute(desiredRange) or Skip()                           │
│                                                                        │
│  Returns: 🟩 RebalanceDecision<TRange>                                │
└────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────┐
│  RebalanceExecutor  [Mutating Actor]                                   │
│  🟦 CLASS (sealed)                                                      │
│                                                                         │
│  ExecuteAsync(desiredRange, ct):                                       │
│   1. rangeData = _state.Cache.ToRangeData() ──────────┐               │
│   2. if (rangeData.Range == desiredRange) return      │               │
│   3. ct.ThrowIfCancellationRequested()                 │               │
│   4. extended = await _cacheFetcher.ExtendCacheAsync() ┼───────────┐  │
│   5. ct.ThrowIfCancellationRequested()                 │           │  │
│   6. rebalanced = extended[desiredRange] (trim)        │           │  │
│   7. ct.ThrowIfCancellationRequested()                 │           │  │
│   8. _state.Cache.Rematerialize(rebalanced) ───────────┼───────┐   │  │
│   9. _state.NoRebalanceRange = ... ────────────────────┼───────┤   │  │
└────────────────────────────────────────────────────────┼───────┼───┼──┘
                                                         │       │   │
┌────────────────────────────────────────────────────────▼───────┼───┼──┐
│  CacheState  [Shared Mutable State]                            │   │  │
│  🟦 CLASS (sealed)  ⚠️ SHARED                                   │   │  │
│                                                                 │   │  │
│  Properties:                                                    │   │  │
│   ├─ ICacheStorage Cache ◄──────────────────────────────────────┼───┤  │
│   ├─ Range? LastRequested ◄─ UserRequestHandler                │   │  │
│   ├─ Range? NoRebalanceRange ◄─ RebalanceExecutor              │   │  │
│   └─ TDomain Domain (readonly)                                  │   │  │
│                                                                 │   │  │
│  Shared by:                                                     │   │  │
│   ├─ UserRequestHandler (R/W)                                   │   │  │
│   ├─ RebalanceExecutor (R/W)                                    │   │  │
│   └─ RebalanceScheduler → DecisionEngine (R)                    │   │  │
└─────────────────────────────────────────────────────────────────┼───┼──┘
                                                                  │   │
┌─────────────────────────────────────────────────────────────────▼───┼──┐
│  ICacheStorage<TRange, TData, TDomain>                              │  │
│  🟧 INTERFACE                                                        │  │
│                                                                      │  │
│  Implementations:                                                   │  │
│   ├─ 🟦 SnapshotReadStorage (TData[] array)                         │  │
│   │   • Read: zero allocation (memory view)                         │  │
│   │   • Write: expensive (allocates new array)                      │  │
│   │                                                                  │  │
│   └─ 🟦 CopyOnReadStorage (List<TData>)                             │  │
│       • Read: allocates (copies to new array)                       │  │
│       • Write: cheap (list operations)                              │  │
│                                                                      │  │
│  Methods:                                                            │  │
│   ├─ void Rematerialize(RangeData) ⊲ WRITE                          │  │
│   ├─ ReadOnlyMemory<TData> Read(Range) ⊳ READ                       │  │
│   └─ RangeData ToRangeData() ⊳ READ                                 │  │
└──────────────────────────────────────────────────────────────────────┼──┘
                                                                       │
┌──────────────────────────────────────────────────────────────────────▼──┐
│  CacheDataFetcher  [Data Fetcher]                                       │
│  🟦 CLASS (sealed)                                                       │
│                                                                          │
│  ExtendCacheAsync(current, requested, ct):                              │
│   1. missingRanges = CalculateMissingRanges()                           │
│   2. fetched = await _dataSource.FetchAsync(missingRanges, ct) ◄────┐  │
│   3. return UnionAll(current, fetched) (merge, no trim)             │  │
│                                                                       │  │
│  Shared by:                                                           │  │
│   ├─ UserRequestHandler (expand to requested)                        │  │
│   └─ RebalanceExecutor (expand to desired)                           │  │
└───────────────────────────────────────────────────────────────────────┼──┘
                                                                        │
┌───────────────────────────────────────────────────────────────────────▼──┐
│  IDataSource<TRangeType, TDataType>  [External Data Source]             │
│  🟧 INTERFACE (user-implemented)                                         │
│                                                                           │
│  Methods:                                                                 │
│   ├─ FetchAsync(Range, CT) → Task<IEnumerable<TData>>                    │
│   └─ FetchAsync(IEnumerable<Range>, CT) → Task<IEnumerable<RangeChunk>> │
│                                                                           │
│  Characteristics:                                                         │
│   ├─ User-provided implementation                                        │
│   ├─ May perform I/O (network, disk, database)                           │
│   ├─ Read-only (fetches data)                                            │
│   └─ Should respect CancellationToken                                    │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## Read/Write Patterns

### CacheState (⚠️ Shared Mutable State)

#### Writers

**UserRequestHandler**:
- ✏️ Writes `LastRequested` property
- ✏️ Writes `Cache` (via `Rematerialize()`)
  - **Purpose**: Expand cache to cover requested range
  - **When**: User request needs data not in cache
  - **Scope**: Expands only (never trims)

**RebalanceExecutor**:
- ✏️ Writes `Cache` (via `Rematerialize()`)
  - **Purpose**: Normalize cache to DesiredCacheRange
  - **When**: Rebalance execution completes
  - **Scope**: Expands AND trims
- ✏️ Writes `NoRebalanceRange` property
  - **Purpose**: Update threshold zone after normalization
  - **When**: After successful rebalance

#### Readers

**UserRequestHandler**:
- 👁️ Reads `Cache.Range` - Check if cache covers requested range
- 👁️ Reads `Cache.Read(range)` - Return data to user
- 👁️ Reads `Cache.ToRangeData()` - Get snapshot before extending

**RebalanceScheduler** (via DecisionEngine):
- 👁️ Reads `NoRebalanceRange` - Decision logic (check if rebalance needed)

**RebalanceExecutor**:
- 👁️ Reads `Cache.Range` - Check if already at desired range
- 👁️ Reads `Cache.ToRangeData()` - Get snapshot before normalizing

#### Coordination

**No locks** (by design):
- Single consumer model (one logical user per cache)
- Coordination via **CancellationToken**
- User Path **always cancels** rebalance before mutations
- Rebalance **always checks** cancellation before mutations

**Atomic operations**:
- `Rematerialize()` replaces storage atomically (array/list assignment)
- Property writes are atomic (reference assignment)

---

### CancellationTokenSource (Intent Identity)

#### Owner: IntentController

**Creates**:
- In `PublishIntent()` - new CTS for each intent

**Cancels**:
- In `PublishIntent()` - cancels previous CTS (supersede old intent)
- In `CancelPendingRebalance()` - cancels current CTS (user priority)

**Disposes**:
- Immediately after cancellation (prevent resource leaks)
- Sets to null after disposal (clean state)

#### Users

**RebalanceScheduler**:
- 👁️ Receives token from IntentManager
- 👁️ Checks `IsCancellationRequested` after debounce delay
- 👁️ Passes token to `ExecutePipelineAsync()`
- 👁️ Passes token to `Task.Delay()` (cancellable debounce)

**RebalanceExecutor**:
- 👁️ Receives token from Scheduler
- 👁️ Calls `ThrowIfCancellationRequested()` at three points:
  1. After range equality check, before I/O
  2. After `ExtendCacheAsync()`, before trim
  3. Before `Rematerialize()` (prevent applying obsolete results)

**CacheDataFetcher**:
- 👁️ Receives token from caller (UserRequestHandler or RebalanceExecutor)
- 👁️ Passes token to `IDataSource.FetchAsync()` (cancellable I/O)

---

## Thread Safety Model

### Concurrency Philosophy

The Sliding Window Cache follows a **single consumer model** as documented in `docs/concurrency-model.md`:

> "A cache instance is **not thread-safe**, is **not designed for concurrent access**, and assumes a single, coherent access pattern. This is an **ideological requirement**, not merely an architectural or technical limitation."

### Key Principles

1. **Single Logical Consumer**
   - One cache instance = one user
   - One access trajectory
   - One temporal sequence of requests

2. **No Synchronization Primitives**
   - ❌ No locks (`lock`, `Monitor`)
   - ❌ No semaphores (`SemaphoreSlim`)
   - ❌ No concurrent collections
   - ✅ Only `CancellationToken` for coordination

3. **Coordination Mechanism**
   - User Path cancels rebalance **before** any cache mutation
   - Rebalance checks cancellation **before and during** execution
   - Atomic array/list replacement in `Rematerialize()`

### Thread Contexts

| Component | Thread Context | Notes |
|-----------|----------------|-------|
| **WindowCache** | Neutral | Just delegates |
| **UserRequestHandler** | ⚡ **User Thread** | Synchronous, fast path |
| **RebalanceIntentManager** | User Thread | Synchronous methods (called from user) |
| **RebalanceScheduler** | 🔄 **Background** | ThreadPool, async |
| **RebalanceDecisionEngine** | 🔄 **Background** | ThreadPool, pure logic |
| **RebalanceExecutor** | 🔄 **Background** | ThreadPool, async, I/O |
| **CacheDataFetcher** | Both ⚡🔄 | User Thread OR Background |
| **CacheState** | Both ⚡🔄 | Shared mutable (no locks!) |
| **Storage (Snapshot/CopyOnRead)** | Both ⚡🔄 | Owned by CacheState |

### Concurrency Invariants (from `docs/invariants.md`)

**A.1 Concurrency & Priority**:
- **-1**: User Path **MUST NOT execute concurrently** with Rebalance Execution
- **0**: User Path **always has higher priority** than Rebalance Execution
- **0a**: Every User Request **MUST cancel** any ongoing/pending Rebalance before mutations

**C. Rebalance Intent & Temporal Invariants**:
- **17**: At most **one active rebalance intent**
- **18**: Previous intents are **obsolete** after new intent
- **21**: At most **one rebalance execution** active at any time

### How It Works

#### User Request Flow (User Thread)
```
1. UserRequestHandler.HandleRequestAsync() called
2. FIRST STEP: _intentManager.CancelPendingRebalance()
   └─> Cancels CancellationTokenSource
   └─> Background rebalance receives cancellation signal
3. Check cache, extend if needed
4. Mutate cache (Rematerialize) - safe, rebalance is cancelled
5. Publish new intent
6. Return data
```

#### Rebalance Flow (Background Thread)
```
1. RebalanceScheduler.ScheduleRebalance() in Task.Run()
2. await Task.Delay() - cancellable debounce
3. Check IsCancellationRequested - early exit if cancelled
4. DecisionEngine.ShouldExecuteRebalance() - pure logic
5. RebalanceExecutor.ExecuteAsync()
   ├─ ThrowIfCancellationRequested() before I/O
   ├─ await _dataSource.FetchAsync() - cancellable I/O
   ├─ ThrowIfCancellationRequested() after I/O
   ├─ Trim data
   ├─ ThrowIfCancellationRequested() before mutation
   └─ Rematerialize() - atomic cache update
```

### Multi-User Scenarios

**✅ Correct Approach**:
```csharp
// Create one cache instance per user
var userCache1 = new WindowCache<int, Data, IntDomain>(...);
var userCache2 = new WindowCache<int, Data, IntDomain>(...);
```

**❌ Incorrect Approach**:
```csharp
// DO NOT share cache across threads/users
var sharedCache = new WindowCache<int, Data, IntDomain>(...);
// Thread 1: sharedCache.GetDataAsync() - UNSAFE
// Thread 2: sharedCache.GetDataAsync() - UNSAFE
```

### Safety Guarantees

**Provided**:
- ✅ User Path never waits for rebalance
- ✅ User Path always has priority (cancels rebalance)
- ✅ At most one rebalance execution active
- ✅ Obsolete rebalance results are discarded
- ✅ Cache state remains consistent (atomic Rematerialize)

**Not Provided**:
- ❌ Thread-safe concurrent access (by design)
- ❌ Multiple consumers per cache (model violation)
- ❌ Cross-user sliding window arbitration (nonsensical)

---

## Type Summary Tables

### Reference Types (Classes)

| Component | Mutability | Shared State | Ownership | Lifetime |
|-----------|------------|--------------|-----------|----------|
| WindowCache | Immutable (after ctor) | No | User creates | App lifetime |
| UserRequestHandler | Immutable | No | WindowCache owns | Cache lifetime |
| CacheState | **Mutable** | **Yes** ⚠️ | WindowCache owns, shared | Cache lifetime |
| IntentController | Mutable (_currentIntentCts) | No | WindowCache owns | Cache lifetime |
| RebalanceScheduler | Immutable | No | IntentController owns | Cache lifetime |
| RebalanceDecisionEngine | Immutable | No | WindowCache owns | Cache lifetime |
| RebalanceExecutor | Immutable | No | WindowCache owns | Cache lifetime |
| CacheDataFetcher | Immutable | No | WindowCache owns | Cache lifetime |
| SnapshotReadStorage | **Mutable** (_storage array) | No | CacheState owns | Cache lifetime |
| CopyOnReadStorage | **Mutable** (_activeStorage, _stagingBuffer) | No | CacheState owns | Cache lifetime |

### Value Types (Structs)

| Component | Mutability | Ownership | Lifetime |
|-----------|------------|-----------|----------|
| ThresholdRebalancePolicy | Readonly | Copied into components | Component lifetime |
| ProportionalRangePlanner | Readonly | Copied into components | Component lifetime |
| RebalanceDecision | Readonly | Local variable | Method scope |

### Other Types

| Component | Type | Purpose | Mutability |
|-----------|------|---------|------------|
| WindowCacheOptions | 🟨 Record | Configuration | Immutable |
| RangeChunk | 🟨 Record | Data transfer | Immutable |
| UserCacheReadMode | 🟪 Enum | Configuration option | Immutable |
| ICacheStorage | 🟧 Interface | Storage abstraction | - |
| IDataSource | 🟧 Interface | External data contract | - |

---

## Component Responsibilities Summary

### By Execution Context

**User Thread (Synchronous, Fast)**:
- WindowCache - Facade, delegates
- UserRequestHandler - Serve requests, trigger intents

**Background / ThreadPool (Asynchronous, Heavy)**:
- RebalanceScheduler - Timing, debounce, orchestration
- RebalanceDecisionEngine - Pure decision logic
- RebalanceExecutor - Cache normalization, I/O

**Both Contexts**:
- CacheDataFetcher - Data fetching (called by both paths)
- CacheState - Shared mutable state (accessed by both)

### By Responsibility

**Data Serving**:
- WindowCache (facade)
- UserRequestHandler (implementation)
- CacheState (storage)
- ICacheStorage implementations (actual data)

**Intent Management**:
- IntentController (lifecycle)
- RebalanceScheduler (execution)

**Decision Making**:
- RebalanceDecisionEngine (orchestrator)
- ThresholdRebalancePolicy (thresholds)
- ProportionalRangePlanner (geometry)

**Mutation**:
- UserRequestHandler (expand only)
- RebalanceExecutor (normalize: expand + trim)

**Data Fetching**:
- CacheDataFetcher (internal)
- IDataSource (external, user-provided)

---

## Architectural Patterns Used

### 1. Facade Pattern
**WindowCache** acts as a facade that hides internal complexity and provides a simple public API.

### 2. Composition Root
**WindowCache** constructor wires all components together in one place.

### 3. Actor Model (Conceptual)
Components follow actor-like patterns with clear responsibilities and message passing (method calls).

### 4. Intent Controller Pattern
**IntentController** manages versioned, cancellable operations through CancellationTokenSource identity.

### 5. Strategy Pattern
**ICacheStorage** with two implementations (SnapshotReadStorage, CopyOnReadStorage) allows runtime selection of storage strategy.

### 6. Value Object Pattern
**ThresholdRebalancePolicy**, **ProportionalRangePlanner**, **RebalanceDecision** are immutable value types with pure behavior.

### 7. Shared Mutable State (Controlled)
**CacheState** is intentionally shared mutable state, coordinated via CancellationToken (not locks).

### 8. Single Consumer Model
Entire architecture assumes one logical consumer, avoiding traditional concurrency primitives.

---

## Related Documentation

- **Architecture Overview**: `docs/actors-to-components-mapping.md`
- **Responsibilities**: `docs/actors-and-responsibilities.md`
- **Invariants**: `docs/invariants.md`
- **Scenarios**: `docs/scenario-model.md`
- **State Machine**: `docs/cache-state-machine.md`
- **Concurrency Model**: `docs/concurrency-model.md`
- **Intent Manager Decomposition**: `docs/rebalance-intent-manager-decomposition.md`
- **Actor Decomposition Pattern**: `docs/actor-decomposition-pattern.md`

---

## Conclusion

The Sliding Window Cache is composed of **19 components** working together to provide fast, cache-aware data access with automatic rebalancing:

- **10 classes** (reference types) provide the runtime behavior
- **3 structs** (value types) provide pure, stateless logic
- **2 interfaces** define contracts for extensibility
- **2 records** provide immutable configuration and data transfer
- **1 enum** defines storage strategy options

The architecture follows a **single consumer model** with **no traditional synchronization primitives**, relying instead on **CancellationToken** for coordination between the fast User Path and the async Rebalance Path.

All components are designed with **clear ownership**, **explicit read/write patterns**, and **well-defined responsibilities**, making the system predictable, testable, and maintainable.

---

**Document Version**: 1.0  
**Last Updated**: February 8, 2026  
**Status**: Complete