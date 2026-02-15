# SlidingWindowCache Benchmarks

Comprehensive BenchmarkDotNet performance suite for SlidingWindowCache, measuring architectural performance characteristics using **public API only**.

**🎯 Methodologically Correct Benchmarks**: This suite follows rigorous benchmark methodology to ensure deterministic, reliable, and interpretable results.

---

## Overview

This benchmark project provides reliable, deterministic performance measurements organized around **two distinct execution flows** of SlidingWindowCache:

### Execution Flow Model

SlidingWindowCache has **two independent cost centers**:

1. **User Request Flow** → Measures latency/cost of user-facing API calls
   - Rebalance/background activity is **NOT** included in measured results
   - Focus: Direct `GetDataAsync` call overhead
   
2. **Rebalance/Maintenance Flow** → Measures cost of window maintenance operations
   - Explicitly waits for stabilization using `WaitForIdleAsync`
   - Focus: Background window management and cache mutation costs

### What We Measure

- **Snapshot vs CopyOnRead** storage modes across both flows
- **User Request Flow**: Full hit, partial hit, full miss scenarios
- **Rebalance Flow**: Maintenance costs after partial hit and full miss
- **Scenario Testing**: Cold start performance and sequential locality advantages
- **Scaling Behavior**: Performance across varying data volumes and cache sizes

---

## Parameterization Strategy

All benchmarks are **parameterized** to measure scaling behavior across different workload characteristics:

### Parameters

1. **`RangeSpan`** - Requested range size
   - Values: `[100, 1_000, 10_000, 100_000, 1_000_000]`
   - Purpose: Test how storage strategies scale with data volume
   - Critical thresholds:
     - **85KB (~21,000 integers)**: Large Object Heap (LOH) boundary
     - **100,000+ elements**: Memory pressure scenarios

2. **`CacheCoefficientSize`** - Left/right prefetch multipliers
   - Values: `[1, 10, 100, 1_000]`
   - Purpose: Test rebalance cost vs cache size tradeoff
   - Total cache size = `RangeSpan × (1 + leftCoeff + rightCoeff)`

### Parameter Matrix

- **5 range sizes** × **4 cache coefficients** = **20 parameter combinations**
- Each benchmark method runs across all 20 combinations
- Results grouped by category for easier comparison

### Expected Scaling Insights

**Snapshot Mode:**
- ✅ **Advantage at small-to-medium sizes** (RangeSpan < 10,000)
  - Zero-allocation reads dominate
  - Rebalance cost acceptable
- ⚠️ **LOH pressure at large sizes** (RangeSpan > 21,000)
  - Array allocations go to LOH (no compaction)
  - GC pressure increases
- ❌ **Disadvantage at very large sizes** (RangeSpan > 100,000)
  - Rebalance always allocates multi-MB arrays
  - Memory spikes during rebalance

**CopyOnRead Mode:**
- ❌ **Disadvantage at small sizes** (RangeSpan < 1,000)
  - Per-read allocation overhead visible
  - List overhead not amortized
- ✅ **Competitive at medium sizes** (RangeSpan 10,000-100,000)
  - List growth amortizes allocation cost
  - Reduced LOH pressure
- ✅ **Advantage at very large sizes** (RangeSpan > 100,000)
  - Incremental list operations cheaper than full array allocation
  - Stable memory usage

**Cache Coefficient Impact:**
- **Coefficient 1-10**: Minimal difference between modes
- **Coefficient 100-1000**: Rebalance cost dominates
  - CopyOnRead advantage becomes significant
  - Snapshot mode shows memory spikes

### Interpretation Guide

When analyzing results, look for:

1. **Crossover points**: Where CopyOnRead becomes faster than Snapshot
   - Expected around RangeSpan=10,000-100,000 depending on coefficient
   
2. **Allocation patterns**: 
   - Snapshot: Zero on read, large on rebalance
   - CopyOnRead: Constant on read, incremental on rebalance

3. **Memory usage trends**:
   - Watch for Gen2 collections (LOH pressure indicator)
   - Compare total allocated bytes across modes

4. **Latency stability**:
   - Snapshot should show consistent read latency
   - CopyOnRead should show linear growth with RangeSpan

---

## Design Principles

### 1. Public API Only
- ✅ No internal types
- ✅ No reflection
- ✅ Only uses public `WindowCache` API

### 2. Deterministic Behavior
- ✅ `FakeDataSource` with no randomness
- ✅ `SynchronousDataSource` for zero-latency isolation
- ✅ Stable, predictable data generation
- ✅ Configurable simulated latency
- ✅ No I/O operations

### 3. Methodological Rigor
- ✅ **No state reuse**: Fresh cache per iteration via `[IterationSetup]`
- ✅ **Explicit rebalance handling**: `WaitForIdleAsync` in setup/cleanup, NOT in benchmark methods
- ✅ **Clear separation**: Read microbenchmarks vs partial-hit vs scenario-level
- ✅ **Isolation**: Each benchmark measures ONE thing
- ✅ **MemoryDiagnoser** for allocation tracking
- ✅ **MarkdownExporter** for report generation
- ✅ **Parameterization**: Comprehensive scaling analysis

---

## Benchmark Categories

Benchmarks are organized by **execution flow** to clearly separate user-facing costs from background maintenance costs.

### 📱 User Request Flow Benchmarks

**File**: `UserFlowBenchmarks.cs`

**Goal**: Measure ONLY user-facing request latency. Rebalance/background activity is EXCLUDED from measurements.

**Parameters**: `RangeSpan` × `CacheCoefficientSize` (20 combinations)

**Contract**:
- Benchmark methods measure ONLY `GetDataAsync` cost
- `WaitForIdleAsync` moved to `[IterationCleanup]`
- Fresh cache per iteration
- Deterministic overlap patterns (no randomness)

**Benchmark Methods** (grouped by category):

| Category | Method | Purpose |
|----------|--------|---------|
| **FullHit** | `User_FullHit_Snapshot` | Baseline: Full cache hit with Snapshot mode |
| **FullHit** | `User_FullHit_CopyOnRead` | Full cache hit with CopyOnRead mode |
| **PartialHit** | `User_PartialHit_ForwardShift_Snapshot` | Partial hit moving right (Snapshot) |
| **PartialHit** | `User_PartialHit_ForwardShift_CopyOnRead` | Partial hit moving right (CopyOnRead) |
| **PartialHit** | `User_PartialHit_BackwardShift_Snapshot` | Partial hit moving left (Snapshot) |
| **PartialHit** | `User_PartialHit_BackwardShift_CopyOnRead` | Partial hit moving left (CopyOnRead) |
| **FullMiss** | `User_FullMiss_Snapshot` | Full cache miss (Snapshot) |
| **FullMiss** | `User_FullMiss_CopyOnRead` | Full cache miss (CopyOnRead) |

**Expected Results**:
- Full hit: Snapshot ~0 allocations, CopyOnRead allocates proportional to RangeSpan
- Partial hit: Both modes serve request immediately, rebalance deferred to cleanup
- Full miss: Request served from data source, rebalance deferred to cleanup
- **Scaling**: Snapshot advantage increases with RangeSpan for full hits

---

### ⚙️ Rebalance/Maintenance Flow Benchmarks

**File**: `RebalanceFlowBenchmarks.cs`

**Goal**: Measure ONLY window maintenance and rebalance operation costs, isolated from I/O latency.

**Parameters**: `RangeSpan` × `CacheCoefficientSize` (20 combinations)

**Contract**:
- Uses `SynchronousDataSource` (zero latency) to isolate cache mechanics
- `WaitForIdleAsync` INSIDE benchmark methods (measuring rebalance)
- Trigger mutation → explicitly wait for stabilization
- Aggressive thresholds ensure rebalancing occurs

**Benchmark Methods** (grouped by category):

| Category | Method | Purpose |
|----------|--------|---------|
| **PartialHit** | `Rebalance_AfterPartialHit_Snapshot` | Baseline: Rebalance cost after partial hit (Snapshot) |
| **PartialHit** | `Rebalance_AfterPartialHit_CopyOnRead` | Rebalance cost after partial hit (CopyOnRead) |
| **FullMiss** | `Rebalance_AfterFullMiss_Snapshot` | Rebalance cost after full miss (Snapshot) |
| **FullMiss** | `Rebalance_AfterFullMiss_CopyOnRead` | Rebalance cost after full miss (CopyOnRead) |

**Expected Results**:
- Snapshot: Higher rebalance cost (full array allocation)
  - **Scaling**: Cost increases linearly with (RangeSpan × CacheCoefficientSize)
  - **LOH impact**: Significant slowdown above RangeSpan=21,000
- CopyOnRead: Lower rebalance cost (incremental list operations)
  - **Scaling**: Amortized cost, plateaus as capacity stabilizes
  - **Memory**: More predictable, less GC pressure
- **Crossover point**: CopyOnRead becomes faster around RangeSpan=10,000+

---

### 🌍 Scenario Benchmarks (End-to-End)

**File**: `ScenarioBenchmarks.cs`

**Goal**: End-to-end scenario testing including cold start and locality patterns. NOT microbenchmarks.

**Parameters**: `RangeSpan` × `CacheCoefficientSize` (20 combinations)

**Contract**:
- Fresh cache per iteration
- Cold start: Measures complete initialization including rebalance
- Locality: Simulates sequential access patterns (10 requests), cleanup handles stabilization

**Benchmark Methods** (grouped by category):

| Category | Method | Purpose |
|----------|---------|---------|
| **ColdStart** | `ColdStart_Rebalance_Snapshot` | Baseline: Initial cache population (Snapshot) |
| **ColdStart** | `ColdStart_Rebalance_CopyOnRead` | Initial cache population (CopyOnRead) |
| **Locality** | `User_LocalityScenario_DirectDataSource` | Baseline: No caching (direct data source) |
| **Locality** | `User_LocalityScenario_Snapshot` | Sequential access with Snapshot mode |
| **Locality** | `User_LocalityScenario_CopyOnRead` | Sequential access with CopyOnRead mode |

**Expected Results**:
- Cold start: Allocation patterns differ between modes
  - Snapshot: Large upfront allocation
  - CopyOnRead: Incremental allocation, less memory spike
- Locality: 70-80% reduction in data source calls vs direct access
  - **Scaling**: Cache advantage increases with RangeSpan (amortizes prefetch cost)
  - **Coefficient impact**: Higher coefficients = better hit rate but higher memory

---

## Running Benchmarks

### Quick Start

```bash
# Run all benchmarks (WARNING: This will take 6-12 hours with parameterization)
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks

# Run specific benchmark class
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks --filter "*UserFlowBenchmarks*"

# Run specific parameter combination (e.g., RangeSpan=1000)
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks --filter "*" --job short -- --filter "*RangeSpan_1000*"
```

### Filtering Options

```bash
# Run only FullHit category across all parameters
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks --filter "*FullHit*"

# Run only Rebalance benchmarks
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks --filter "*RebalanceFlowBenchmarks*"

# Run specific method
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks --filter "*User_FullHit_Snapshot*"
```

### Managing Execution Time

With parameterization, total execution time can be significant:

**Default configuration:**
- 20 parameter combinations × 8 methods × 2 modes = 320+ individual benchmarks
- Estimated time: 6-12 hours

**Faster turnaround options:**

1. **Use SimpleJob for development:**
```csharp
[SimpleJob(warmupCount: 3, targetCount: 5)]  // Add to class attributes
```

2. **Run subset of parameters:**
```bash
# Comment out larger parameter values in code temporarily
[Params(100, 1_000)]  // Instead of all 5 values
```

3. **Run by category:**
```bash
# Focus on one flow at a time
dotnet run -c Release --project tests/SlidingWindowCache.Benchmarks --filter "*FullHit*"
```

---

## Data Sources

### SynchronousDataSource
Zero-latency synchronous data source for isolating cache mechanics:

```csharp
// Zero latency - isolates rebalance cost from I/O
var dataSource = new SynchronousDataSource(domain);
```

**Purpose**:
- Used in all benchmarks for deterministic, reproducible results
- Returns synchronous `IEnumerable<T>` wrapped in completed `Task`
- No `Task.Delay` or async overhead
- Measures pure cache mechanics without I/O interference

**Data Generation**:
- Deterministic: Position `i` produces value `i`
- No randomness
- Stable across runs
- Predictable memory footprint

---

## Running Benchmarks

### Run All Benchmarks
```bash
cd tests/SlidingWindowCache.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark Class
```bash
# User request flow benchmarks
dotnet run -c Release -- --filter *UserFlowBenchmarks*

# Rebalance/maintenance flow benchmarks
dotnet run -c Release -- --filter *RebalanceFlowBenchmarks*

# Scenario benchmarks (cold start + locality)
dotnet run -c Release -- --filter *ScenarioBenchmarks*
```

### Run Specific Method
```bash
# User flow examples
dotnet run -c Release -- --filter *User_FullHit*
dotnet run -c Release -- --filter *User_PartialHit*

# Rebalance flow examples
dotnet run -c Release -- --filter *Rebalance_AfterPartialHit*

# Scenario examples
dotnet run -c Release -- --filter *ColdStart_Rebalance*
dotnet run -c Release -- --filter *User_LocalityScenario*
```

---

## Interpreting Results

### Mean Execution Time
- Lower is better
- Compare Snapshot vs CopyOnRead for same scenario
- Look for order-of-magnitude differences

### Allocations
- **Snapshot mode**: Watch for large array allocations during rebalance
- **CopyOnRead mode**: Watch for per-read allocations
- **Gen 0/1/2**: Track garbage collection pressure

### Memory Diagnostics
- **Allocated**: Total bytes allocated
- **Gen 0/1/2 Collections**: GC pressure indicator
- **LOH**: Large Object Heap allocations (arrays ≥85KB)

---

## Methodological Guarantees

### ✅ No State Drift
Every iteration starts from a clean, deterministic cache state via `[IterationSetup]`.

### ✅ Explicit Rebalance Handling
- Benchmarks that trigger rebalance use `[IterationCleanup]` to wait for completion
- NO `WaitForIdleAsync` inside benchmark methods (would contaminate measurements)
- Setup phases use `WaitForIdleAsync` to ensure deterministic starting state

### ✅ Clear Separation
- **Read microbenchmarks**: Rebalance disabled, measure read path only
- **Partial hit benchmarks**: Rebalance enabled, deterministic overlap, cleanup handles rebalance
- **Scenario benchmarks**: Full sequential patterns, cleanup handles stabilization

### ✅ Isolation
- `RebalanceCostBenchmarks` uses `SynchronousDataSource` to isolate cache mechanics from I/O
- Each benchmark measures ONE architectural characteristic

---

## Expected Performance Characteristics

### Snapshot Mode
- ✅ **Best for**: Read-heavy workloads (high read:rebalance ratio)
- ✅ **Strengths**: Zero-allocation reads, fastest read performance
- ❌ **Weaknesses**: Expensive rebalancing, LOH pressure

### CopyOnRead Mode
- ✅ **Best for**: Write-heavy workloads (frequent rebalancing)
- ✅ **Strengths**: Cheap rebalancing, reduced LOH pressure
- ❌ **Weaknesses**: Allocates on every read, slower read performance

### Sequential Locality
- ✅ **Cache advantage**: Reduces data source calls by 70-80%
- ✅ **Prefetching benefit**: Most requests served from cache
- ✅ **Latency hiding**: Background rebalancing doesn't block reads

---

## Deprecated Benchmarks

### ⚠️ Old Benchmark Files (DEPRECATED - REPLACED BY EXECUTION FLOW MODEL)

The following benchmark files have been replaced by the new execution flow model:

**Issues with Old Organization**:
- Mixed user-facing costs with maintenance costs
- Unclear separation between execution flows
- Difficult to interpret which costs are user-visible
- Inconsistent handling of WaitForIdleAsync

**Old Files → New Files Mapping**:

| Old File | Replaced By | New Method Names |
|----------|-------------|------------------|
| `FullHitBenchmarks.cs` | `UserFlowBenchmarks.cs` | `User_FullHit_Snapshot`, `User_FullHit_CopyOnRead` |
| `PartialHitBenchmarks.cs` | `UserFlowBenchmarks.cs` | `User_PartialHit_ForwardShift_*`, `User_PartialHit_BackwardShift_*` |
| `FullMissBenchmarks.cs` | `UserFlowBenchmarks.cs` | `User_FullMiss_Snapshot`, `User_FullMiss_CopyOnRead` |
| `RebalanceCostBenchmarks.cs` | `RebalanceFlowBenchmarks.cs` | `Rebalance_AfterPartialHit_*`, `Rebalance_AfterFullMiss_*` |
| `LocalityAdvantageBenchmarks.cs` | `ScenarioBenchmarks.cs` | `User_LocalityScenario_*` |
| `ColdStartBenchmarks.cs` | `ScenarioBenchmarks.cs` | `ColdStart_Rebalance_*` |

**Action**: The old files can be safely deleted. All functionality is preserved in the new execution flow model with improved clarity and semantic naming.

---

## Architecture Goals

These benchmarks validate:
1. **User request flow isolation** (measured without rebalance contamination in `UserFlowBenchmarks`)
2. **Rebalance cost tradeoffs** (Snapshot vs CopyOnRead, isolated in `RebalanceFlowBenchmarks`)
3. **Sequential locality optimization** (vs direct data source, validated in `ScenarioBenchmarks`)
4. **Memory pressure characteristics** (allocations, GC, LOH across all flows)
5. **Deterministic partial-hit behavior** (`UserFlowBenchmarks` with guaranteed overlap)
6. **Cold start performance** (end-to-end initialization in `ScenarioBenchmarks`)

---

## Output Files

After running benchmarks, find results in:
```
BenchmarkDotNet.Artifacts/
├── results/
│   ├── SlidingWindowCache.Benchmarks.UserFlowBenchmarks-report.html
│   ├── SlidingWindowCache.Benchmarks.UserFlowBenchmarks-report.md
│   ├── SlidingWindowCache.Benchmarks.RebalanceFlowBenchmarks-report.html
│   ├── SlidingWindowCache.Benchmarks.RebalanceFlowBenchmarks-report.md
│   ├── SlidingWindowCache.Benchmarks.ScenarioBenchmarks-report.html
│   └── SlidingWindowCache.Benchmarks.ScenarioBenchmarks-report.md
└── logs/
    └── ... (detailed logs)
```

---

## CI/CD Integration

These benchmarks can be integrated into CI/CD for:
- **Performance regression detection**
- **Release performance validation**
- **Architectural decision documentation**
- **Historical performance tracking**

Example: Run on every release and commit results to repository.

---

## License

MIT (same as parent project)
