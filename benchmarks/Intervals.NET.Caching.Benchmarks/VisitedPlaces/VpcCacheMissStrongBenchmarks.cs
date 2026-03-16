using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;
using Intervals.NET.Domain.Default.Numeric;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency cache-miss benchmarks for VisitedPlaces Cache.
/// Measures the complete end-to-end miss cost: data source fetch + background segment
/// storage (+ optional eviction). WaitForIdleAsync is inside the measurement boundary.
///
/// Two benchmark methods isolate the eviction dimension:
///   - CacheMiss_NoEviction:   ample capacity — background stores only, no eviction.
///   - CacheMiss_WithEviction: at capacity    — every store triggers eviction evaluation
///                             and execution (evicts 1, stores 1 → count stays stable).
///
/// Parameters: TotalSegments, StorageStrategy, AppendBufferSize.
/// AppendBufferSize is included because normalization frequency directly affects the
/// background work measured by WaitForIdleAsync.
///
/// Setup strategy (no IterationSetup re-population):
/// - Two caches (NoEviction and WithEviction) populated once in GlobalSetup.
/// - MaxIterations unique miss ranges pre-computed and learned in GlobalSetup.
/// - Each method tracks its own rotating counter independently.
/// - NoEviction cache grows by 1 segment per iteration (negligible drift).
/// - WithEviction cache stays at TotalSegments (evicts 1, stores 1 per iteration).
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcCacheMissStrongBenchmarks : VpcCacheMissBenchmarksBase
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _noEvictionCache;
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _withEvictionCache;
    private IntegerFixedStepDomain _domain;
    private Range<int>[] _missRanges = null!;
    private int _noEvictionIndex;
    private int _withEvictionIndex;

    /// <summary>
    /// Total segments in cache — tests scaling from small to large segment counts.
    /// Values straddle the ~50K crossover point between Snapshot and LinkedList strategies.
    /// </summary>
    [Params(10, 1_000, 100_000)]
    public int TotalSegments { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot vs LinkedList.
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    /// <summary>
    /// Append buffer size — controls normalization frequency.
    /// 1 = normalize every store, 8 = normalize every 8 stores (default).
    /// Affects the background normalization cost measured by WaitForIdleAsync.
    /// </summary>
    [Params(1, 8)]
    public int AppendBufferSize { get; set; }

    /// <summary>
    /// Runs once per parameter combination.
    /// Populates both caches and pre-computes MaxIterations unique miss ranges so that
    /// IterationSetup requires no re-population.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _missRanges = BuildMissRanges(TotalSegments);

        var frozenDataSource = RunLearningPass(
            _domain, StorageStrategy,
            totalSegments: TotalSegments,
            appendBufferSize: AppendBufferSize,
            missRanges: _missRanges);

        // NoEviction: ample capacity — no eviction ever triggered.
        _noEvictionCache = CreateAndPopulate(
            frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + MaxIterations + 1000,
            appendBufferSize: AppendBufferSize,
            totalSegments: TotalSegments);

        // WithEviction: at capacity — every store triggers eviction (evicts 1, stores 1).
        _withEvictionCache = CreateAndPopulate(
            frozenDataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments,
            appendBufferSize: AppendBufferSize,
            totalSegments: TotalSegments);

        _noEvictionIndex = 0;
        _withEvictionIndex = 0;
    }

    [IterationSetup(Target = nameof(CacheMiss_NoEviction))]
    public void IterationSetup_NoEviction()
    {
        _noEvictionIndex++;
    }

    [IterationSetup(Target = nameof(CacheMiss_WithEviction))]
    public void IterationSetup_WithEviction()
    {
        _withEvictionIndex++;
    }

    /// <summary>
    /// Measures complete cache-miss cost without eviction.
    /// Includes: data source fetch + background normalization (segment storage + metadata update).
    /// Cache capacity is ample; eviction is never triggered.
    /// </summary>
    [Benchmark]
    public async Task CacheMiss_NoEviction()
    {
        await _noEvictionCache!.GetDataAsync(_missRanges[_noEvictionIndex % MaxIterations], CancellationToken.None);
        await _noEvictionCache.WaitForIdleAsync();
    }

    /// <summary>
    /// Measures complete cache-miss cost with eviction.
    /// Includes: data source fetch + background normalization (segment storage + eviction
    /// evaluation + eviction execution). Cache is at capacity; each store evicts one segment.
    /// </summary>
    [Benchmark]
    public async Task CacheMiss_WithEviction()
    {
        await _withEvictionCache!.GetDataAsync(_missRanges[_withEvictionIndex % MaxIterations], CancellationToken.None);
        await _withEvictionCache.WaitForIdleAsync();
    }
}
