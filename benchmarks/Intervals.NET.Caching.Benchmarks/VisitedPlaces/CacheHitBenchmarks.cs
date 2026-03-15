using BenchmarkDotNet.Attributes;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Cache Hit Benchmarks for VisitedPlaces Cache.
/// Measures user-facing read latency when all requested data is already cached.
/// 
/// EXECUTION FLOW: User Request > Full cache hit, zero data source calls
/// 
/// Methodology:
/// - Cache created and populated once in GlobalSetup (population is NOT part of the measurement)
/// - Request spans exactly HitSegments adjacent segments (guaranteed full hit)
/// - CacheHit only reads: normalization events may update LRU timestamps but do not
///   structurally modify the segment collection, so GlobalSetup state remains valid
/// 
/// Parameters:
/// - HitSegments: Number of segments the request spans (read-side scaling)
/// - TotalSegments: Total cached segments (storage size scaling, affects FindIntersecting)
/// - StorageStrategy: Snapshot vs LinkedList (algorithm differences)
/// - EvictionSelector: LRU vs FIFO (UpdateMetadata cost difference on read path)
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class CacheHitBenchmarks
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private SynchronousDataSource _dataSource = null!;
    private IntegerFixedStepDomain _domain;
    private Range<int> _hitRange;

    private const int SegmentSpan = 10;

    /// <summary>
    /// Number of segments the request spans — measures read-side scaling.
    /// </summary>
    [Params(1, 10, 100, 1_000)]
    public int HitSegments { get; set; }

    /// <summary>
    /// Total segments in cache — measures storage size impact on FindIntersecting.
    /// </summary>
    [Params(1_000, 10_000)]
    public int TotalSegments { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot (sorted array + binary search) vs LinkedList (stride index).
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    /// <summary>
    /// Eviction selector — LRU has O(usedSegments) UpdateMetadata, FIFO has O(1) no-op.
    /// </summary>
    [Params(EvictionSelectorType.Lru, EvictionSelectorType.Fifo)]
    public EvictionSelectorType EvictionSelector { get; set; }

    /// <summary>
    /// GlobalSetup runs once per parameter combination.
    /// Population cost is paid once, not repeated every iteration.
    /// Safe because CacheHit is a pure read: it does not add or remove segments.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _dataSource = new SynchronousDataSource(_domain);

        // MaxSegmentCount must accommodate TotalSegments without eviction
        _cache = VpcCacheHelpers.CreateCache(
            _dataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 1000,
            selectorType: EvictionSelector);

        // Populate TotalSegments adjacent segments (once per parameter combination)
        VpcCacheHelpers.PopulateSegments(_cache, TotalSegments, SegmentSpan);

        // Pre-calculate the hit range: spans HitSegments adjacent segments
        // Segments are placed at [0,9], [10,19], [20,29], ...
        var hitStart = 0;
        var hitEnd = (HitSegments * SegmentSpan) - 1;
        _hitRange = Factories.Range.Closed<int>(hitStart, hitEnd);
    }

    /// <summary>
    /// Measures user-facing latency for a full cache hit spanning HitSegments segments.
    /// </summary>
    [Benchmark]
    public async Task<ReadOnlyMemory<int>> CacheHit()
    {
        return (await _cache!.GetDataAsync(_hitRange, CancellationToken.None)).Data;
    }
}
