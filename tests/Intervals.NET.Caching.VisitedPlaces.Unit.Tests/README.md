# Unit Tests — VisitedPlaces Cache

Isolated component tests for internal VPC actors. Each test class targets a single class, uses mocks or simple fakes where dependencies are needed, and follows the Arrange-Act-Assert pattern with `Record.Exception` / `Record.ExceptionAsync` for exception assertions.

## Run

```bash
dotnet test tests/Intervals.NET.Caching.VisitedPlaces.Unit.Tests/Intervals.NET.Caching.VisitedPlaces.Unit.Tests.csproj
```

## Structure

```
Core/
  CacheNormalizationExecutorTests.cs   — Background Path four-step sequence

Eviction/
  EvictionEngineTests.cs               — Engine facade: metadata delegation, segment init, evaluate-and-execute
  EvictionExecutorTests.cs             — Constraint satisfaction loop, immune set, candidate selection
  EvictionPolicyEvaluatorTests.cs      — Policy evaluation: single policy, multiple policies, composite pressure
  EvictionConfigBuilderTests.cs        — Builder validation and wiring
  Policies/
    MaxSegmentCountPolicyTests.cs      — ShouldEvict threshold, pressure object
    MaxSegmentCountPolicyFactoryTests.cs
    MaxTotalSpanPolicyTests.cs         — Span accumulation, ShouldEvict threshold
    MaxTotalSpanPolicyFactoryTests.cs
  Selectors/
    LruEvictionSelectorTests.cs        — Metadata init/update, TrySelectCandidate (LRU order, immunity)
    LruEvictionSelectorFactoryTests.cs
    FifoEvictionSelectorTests.cs       — Metadata init (no-op update), TrySelectCandidate (FIFO order, immunity)
    FifoEvictionSelectorFactoryTests.cs
    SmallestFirstEvictionSelectorTests.cs   — Metadata init, TrySelectCandidate (span order, immunity)
    SmallestFirstEvictionSelectorFactoryTests.cs
  Pressure/
    SegmentCountPressureTests.cs       — IsExceeded, Reduce, constraint tracking
    TotalSpanPressureTests.cs          — IsExceeded, Reduce
    CompositePressureTests.cs          — IsExceeded when any pressure fires, Reduce propagation
    NoPressureTests.cs                 — IsExceeded always false

Storage/
  SnapshotAppendBufferStorageTests.cs  — Append buffer flush, sorted snapshot, FindIntersecting
  LinkedListStrideIndexStorageTests.cs — Stride index lookup, tail normalization, FindIntersecting
```

## Key Dependencies

- `EventCounterCacheDiagnostics` — thread-safe diagnostics spy from `Tests.Infrastructure`
- `TestHelpers` — range factory (`CreateRange`), cache factory, assertion helpers
- `Moq` — mock `IDataSource<int,int>` where needed

## Notes

- Storage tests exercise both `SnapshotAppendBufferStorage` and `LinkedListStrideIndexStorage` directly (no cache involved).
- Eviction tests use real policy and selector instances against in-memory segment lists; no cache or data source needed.
- `CacheNormalizationExecutorTests` wires a real storage and eviction engine together to verify the four-step Background Path sequence in isolation.
