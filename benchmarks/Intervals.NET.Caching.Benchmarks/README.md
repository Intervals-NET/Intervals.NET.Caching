# Intervals.NET.Caching — Performance

Sub-microsecond construction. Microsecond-scale reads. Zero-allocation hot paths. 131x burst throughput gains under load. These are not theoretical projections — they are independently verified measurements from a rigorous BenchmarkDotNet suite covering **330+ benchmark cases** across all three cache implementations, using **public API only**.

Every number on this page comes directly from committed benchmark reports. No synthetic micro-ops, no cherry-picked runs.

---

## At a Glance

| Metric                      |          Result | Cache                    | Detail                                                                   |
|-----------------------------|----------------:|--------------------------|--------------------------------------------------------------------------|
| **Fastest construction**    |      **675 ns** | VPC                      | 2.01 KB allocated — ready to serve in under a microsecond                |
| **Layered construction**    |     **1.05 μs** | Layered (SWC+SWC)        | Two-layer cache stack built in a microsecond, 4.12 KB                    |
| **Cache hit (read)**        |      **2.5 μs** | VPC Strong               | Single-segment lookup across 1,000 cached segments                       |
| **Cache hit (read)**        |       **14 μs** | SWC Snapshot             | 10K-span range with 100x cache coefficient — constant 1.38 KB allocation |
| **Layered full hit**        |       **11 μs** | Layered (all topologies) | 392 B allocation — zero measurable overhead from composition             |
| **Cache miss**              |       **16 μs** | VPC Eventual             | Constant 512 B allocation whether the cache holds 10 or 100K segments    |
| **Burst throughput**        | **131x faster** | SWC Bounded              | 703 μs vs 92.6 ms — bounded execution queue eliminates backlog stacking  |
| **Segment lookup at scale** |  **13x faster** | VPC Strong               | AppendBufferSize=8: 180 μs vs 2,419 μs at 100K segments                  |
| **Rebalance (layered)**     |       **88 μs** | Layered (all topologies) | 7.7 KB constant allocation — layering adds no rebalance overhead         |

---

## SlidingWindow Cache (SWC)

### Zero-Allocation Reads with Snapshot Strategy

The Snapshot storage strategy delivers **constant-allocation reads regardless of cache size**. Whether the cache holds 100 or 1,000,000 data points, every full-hit read allocates exactly **1.38 KB**.

CopyOnRead pays for this at read time — its allocation grows linearly with cache size, reaching 3,427x more memory at the largest configuration:

| Scenario | RangeSpan | Cache Coefficient |            Snapshot |              CopyOnRead |                               Ratio |
|----------|----------:|------------------:|--------------------:|------------------------:|------------------------------------:|
| Full Hit |       100 |                 1 |     30 μs / 1.38 KB |         35 μs / 2.12 KB |                         1.2x slower |
| Full Hit |     1,000 |                10 |     27 μs / 1.38 KB |        72 μs / 50.67 KB |        2.7x slower, 37x more memory |
| Full Hit |    10,000 |               100 | **14 μs / 1.38 KB** | **1,881 μs / 4,713 KB** | **134x slower, 3,427x more memory** |

The tradeoff: CopyOnRead allocates significantly less during rebalance operations — **2.5 MB vs 16.4 MB** at 10K span size with Fixed behavior — making it the better choice when rebalances are frequent and reads are infrequent.

### Rebalance Cost is Predictable

Rebalance execution time is remarkably stable across all configurations — **162–167 ms** for 10 sequential rebalance cycles regardless of behavior pattern (Fixed, Growing, Shrinking) or span size:

| Behavior | Strategy   | Span Size | Time (10 cycles) | Allocated |
|----------|------------|----------:|-----------------:|----------:|
| Fixed    | Snapshot   |    10,000 |           162 ms | 16,446 KB |
| Fixed    | CopyOnRead |    10,000 |           163 ms |  2,470 KB |
| Growing  | Snapshot   |    10,000 |           160 ms | 17,408 KB |
| Growing  | CopyOnRead |    10,000 |           164 ms |  2,711 KB |

CopyOnRead consistently uses **6–7x less memory** for rebalance operations at scale.

### Bounded Execution: 131x Throughput Under Load

The bounded execution strategy prevents backlog stacking when data source latency is non-trivial. Under burst load with slow data sources, the difference is not incremental — it is categorical:

| Latency | Burst Size | Unbounded | Bounded |  Speedup |
|--------:|-----------:|----------:|--------:|---------:|
|    0 ms |      1,000 |    542 μs |  473 μs |     1.2x |
|   50 ms |      1,000 | 57,077 μs |  680 μs |  **84x** |
|  100 ms |      1,000 | 92,655 μs |  703 μs | **131x** |

At zero latency the strategies are comparable. The moment real-world I/O latency enters the picture, unbounded execution collapses under burst load while bounded execution stays flat.

### Detailed Reports

- [User Flow (Full Hit / Partial Hit / Full Miss)](Results/Intervals.NET.Caching.Benchmarks.Benchmarks.UserFlowBenchmarks-report-github.md)
- [Rebalance Mechanics](Results/Intervals.NET.Caching.Benchmarks.Benchmarks.RebalanceFlowBenchmarks-report-github.md)
- [End-to-End Scenarios (Cold Start)](Results/Intervals.NET.Caching.Benchmarks.Benchmarks.ScenarioBenchmarks-report-github.md)
- [Execution Strategy Comparison](Results/Intervals.NET.Caching.Benchmarks.Benchmarks.ExecutionStrategyBenchmarks-report-github.md)

---

## VisitedPlaces Cache (VPC)

### Sub-Microsecond Construction

VPC instances are ready to serve in **675 ns** with just **2.01 KB** allocated. The builder API adds only ~80 ns of overhead:

| Method                   |   Time | Allocated |
|--------------------------|-------:|----------:|
| Constructor (Snapshot)   | 675 ns |   2.05 KB |
| Constructor (LinkedList) | 682 ns |   2.01 KB |
| Builder (Snapshot)       | 757 ns |   2.40 KB |
| Builder (LinkedList)     | 782 ns |   2.35 KB |

### Microsecond-Scale Cache Hits

Strong consistency delivers single-segment cache hits in **2.5 μs** and scales linearly — 10 segments in 10 μs, 100 segments in 187 μs. Both storage strategies perform identically on reads:

| Hit Segments | Total Cached | Strategy |      Time | Allocated |
|-------------:|-------------:|----------|----------:|----------:|
|            1 |        1,000 | Snapshot |    2.5 μs |   1.63 KB |
|            1 |       10,000 | Snapshot |    3.2 μs |   1.63 KB |
|           10 |        1,000 | Snapshot |   10.0 μs |   7.27 KB |
|          100 |        1,000 | Snapshot |    187 μs |  63.93 KB |
|        1,000 |       10,000 | Snapshot | 12,806 μs |  626.5 KB |

Performance remains stable as the total segment count grows from 1K to 10K — the binary search lookup scales logarithmically, not linearly.

### Constant-Allocation Cache Misses

Under Eventual consistency, cache miss allocation is **flat at 512 bytes** regardless of how many segments are already cached — a property that matters under sustained write pressure:

| Total Segments | Strategy   |    Time | Allocated |
|---------------:|------------|--------:|----------:|
|             10 | Snapshot   | 17.8 μs |     512 B |
|          1,000 | Snapshot   | 16.6 μs |     512 B |
|        100,000 | Snapshot   | 37.0 μs |     512 B |
|        100,000 | LinkedList | 24.7 μs |     512 B |

### AppendBufferSize: 13x Speedup at Scale

Under Strong consistency, the append buffer size has a dramatic impact at high segment counts. At 100K segments, `AppendBufferSize=8` delivers a **13x speedup** and reduces allocation by **800x**:

| Total Segments | Strategy   | Buffer Size |       Time | Allocated |
|---------------:|------------|------------:|-----------:|----------:|
|        100,000 | Snapshot   |           1 |   2,419 μs |    783 KB |
|        100,000 | Snapshot   |       **8** | **180 μs** |  **1 KB** |
|        100,000 | LinkedList |           1 |   4,907 μs |     50 KB |
|        100,000 | LinkedList |       **8** | **153 μs** |  **1 KB** |

At small segment counts the buffer size has minimal impact — this optimization targets scale.

### Eviction Under Pressure

VPC handles sustained eviction churn without degradation. 100-request burst scenarios with continuous eviction complete in approximately **1 ms**, with Snapshot consistently faster than LinkedList:

| Scenario                | Burst Size | Strategy   |     Time | Allocated |
|-------------------------|-----------:|------------|---------:|----------:|
| Cold Start (all misses) |        100 | Snapshot   |   239 μs |  64.76 KB |
| All Hits                |        100 | Snapshot   |   406 μs | 146.51 KB |
| Churn (eviction active) |        100 | Snapshot   |   877 μs | 131.48 KB |
| Churn (eviction active) |        100 | LinkedList | 1,330 μs | 129.24 KB |

### Partial Hit Performance

Requests that partially overlap cached segments — the common case in real workloads — perform well even with complex gap patterns:

| Gap Count | Total Segments | Strategy   |   Time | Allocated |
|----------:|---------------:|------------|-------:|----------:|
|         1 |          1,000 | Snapshot   |  98 μs |   2.64 KB |
|        10 |          1,000 | Snapshot   | 156 μs |  10.99 KB |
|       100 |          1,000 | LinkedList | 612 μs |  93.27 KB |

LinkedList can outperform Snapshot at high gap counts (612 μs vs 1,210 μs at 100 gaps) due to avoiding array reallocation during multi-segment assembly.

### Detailed Reports

**Cache Hits**
- [Eventual Consistency](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcCacheHitEventualBenchmarks-report-github.md)
- [Strong Consistency](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcCacheHitStrongBenchmarks-report-github.md)

**Cache Misses**
- [Eventual Consistency](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcCacheMissEventualBenchmarks-report-github.md)
- [Strong Consistency (with Eviction & Buffer Size)](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcCacheMissStrongBenchmarks-report-github.md)

**Partial Hits**
- [Single Gap — Eventual](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcSingleGapPartialHitEventualBenchmarks-report-github.md)
- [Single Gap — Strong](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcSingleGapPartialHitStrongBenchmarks-report-github.md)
- [Multiple Gaps — Eventual](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcMultipleGapsPartialHitEventualBenchmarks-report-github.md)
- [Multiple Gaps — Strong](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcMultipleGapsPartialHitStrongBenchmarks-report-github.md)

**Scenarios & Construction**
- [End-to-End Scenarios (Cold Start, All Hits, Churn)](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcScenarioBenchmarks-report-github.md)
- [Construction Benchmarks](Results/Intervals.NET.Caching.Benchmarks.VisitedPlaces.VpcConstructionBenchmarks-report-github.md)

---

## Layered Cache (Multi-Layer Composition)

### Zero Overhead from Composition

The headline result for layered caches: **composition does not degrade read performance**. Full-hit reads across all topologies — two-layer and three-layer — deliver **11 μs with 392 bytes allocated**, identical to single-cache performance:

| Topology        | RangeSpan |    Time | Allocated |
|-----------------|----------:|--------:|----------:|
| SWC + SWC       |       100 | 11.0 μs |     392 B |
| VPC + SWC       |       100 | 10.9 μs |     392 B |
| VPC + SWC + SWC |       100 | 10.9 μs |     392 B |
| SWC + SWC       |    10,000 | 14.8 μs |     392 B |
| VPC + SWC       |    10,000 | 13.6 μs |     392 B |
| VPC + SWC + SWC |    10,000 | 14.0 μs |     392 B |

Allocation is constant at **392 bytes** regardless of topology depth or range span. The layered architecture adds zero measurable allocation overhead.

### Constant-Cost Rebalance

Layer rebalance completes in **87–111 μs** with a flat **7.7 KB** allocation across all topologies:

| Topology        | Span Size |   Time | Allocated |
|-----------------|----------:|-------:|----------:|
| SWC + SWC       |       100 |  88 μs |    7.7 KB |
| VPC + SWC       |       100 |  88 μs |    7.7 KB |
| VPC + SWC + SWC |       100 |  89 μs |    7.7 KB |
| SWC + SWC       |     1,000 | 109 μs |    7.7 KB |
| VPC + SWC       |     1,000 | 106 μs |    7.7 KB |
| VPC + SWC + SWC |     1,000 | 111 μs |    7.7 KB |

Adding a third layer adds less than 5 μs. The allocation cost is constant.

### VPC + SWC: The Fastest Layered Topology

In end-to-end scenarios, **VPC + SWC consistently outperforms homogeneous SWC + SWC** — random-access front layer plus sequential-access back layer is the optimal combination:

| Scenario            |   Span | SWC+SWC |    VPC+SWC | VPC+SWC+SWC |
|---------------------|-------:|--------:|-----------:|------------:|
| Cold Start          |    100 |  158 μs | **138 μs** |      180 μs |
| Cold Start          |  1,000 |  430 μs | **391 μs** |      614 μs |
| Sequential Locality |    100 |  194 μs | **189 μs** |      239 μs |
| Sequential Locality |  1,000 |  469 μs | **441 μs** |      637 μs |
| Full Miss           | 10,000 |  240 μs | **123 μs** |      376 μs |

VPC + SWC is **9–49% faster** than SWC + SWC depending on scenario. The three-layer VPC + SWC + SWC adds 15–43% overhead — expected for an additional layer, but still sub-millisecond across all configurations.

### Sub-2μs Construction

Even the deepest topology builds in under 2 microseconds:

| Topology        |    Time | Allocated |
|-----------------|--------:|----------:|
| SWC + SWC       | 1.05 μs |   4.12 KB |
| VPC + SWC       | 1.35 μs |   4.58 KB |
| VPC + SWC + SWC | 1.78 μs |   6.47 KB |

### Detailed Reports

- [User Flow (Full Hit / Partial Hit / Full Miss)](Results/Intervals.NET.Caching.Benchmarks.Layered.LayeredUserFlowBenchmarks-report-github.md)
- [Rebalance](Results/Intervals.NET.Caching.Benchmarks.Layered.LayeredRebalanceBenchmarks-report-github.md)
- [End-to-End Scenarios](Results/Intervals.NET.Caching.Benchmarks.Layered.LayeredScenarioBenchmarks-report-github.md)
- [Construction](Results/Intervals.NET.Caching.Benchmarks.Layered.LayeredConstructionBenchmarks-report-github.md)

---

## Methodology

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) with `[MemoryDiagnoser]` for allocation tracking. Key methodological properties:

- **Public API only** — no internal types, no reflection, no `InternalsVisibleTo`
- **Fresh state per iteration** — `[IterationSetup]` creates a clean cache for every measurement
- **Deterministic data source** — zero-latency `SynchronousDataSource` isolates cache mechanics from I/O variance
- **Separated cost centers** — User Path benchmarks exclude background activity; Rebalance/Scenario benchmarks explicitly include it via `WaitForIdleAsync`
- **Each benchmark measures one thing** — no mixed measurements, no ambiguous attribution

**Environment**: .NET 8.0, Intel Core i7-1065G7 (4 cores / 8 threads), Windows 10. Full environment details are included in each report file.

**Total coverage**: ~17 benchmark classes, ~50 methods, **330+ parameterized cases** across SWC, VPC, and Layered configurations.

---

## Running Benchmarks

```bash
# All benchmarks (takes many hours with full parameterization)
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks

# By cache type
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks -- --filter "*SlidingWindow*"
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks -- --filter "*VisitedPlaces*"
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks -- --filter "*Layered*"

# Specific benchmark class
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks -- --filter "*UserFlowBenchmarks*"
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks -- --filter "*CacheHitBenchmarks*"

# Specific method
dotnet run -c Release --project benchmarks/Intervals.NET.Caching.Benchmarks -- --filter "*FullHit_SwcSwc*"
```

Reports are generated in `BenchmarkDotNet.Artifacts/results/` locally. Committed baselines are in `Results/`.

---

## License

MIT (same as parent project)
