using BenchmarkDotNet.Attributes;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Single-Gap Partial Hit Benchmarks for VisitedPlaces Cache.
/// Measures read-side scaling: K existing segments hit + 1 gap fetched from data source.
/// 
/// Isolates: FindIntersecting cost + ComputeGaps cost as IntersectingSegments grows.
/// A single gap means exactly one store + one normalization per iteration.
/// 
/// Methodology:
/// - Cache pre-populated with TotalSegments adjacent segments in IterationSetup
/// - Request spans IntersectingSegments existing segments + 1 gap at the right edge
/// - WaitForIdleAsync INSIDE benchmark (measuring complete partial hit + normalization cost)
/// - Fresh cache per iteration (benchmark stores a new gap segment each time)
/// 
/// Parameters:
/// - IntersectingSegments: {1, 10, 100, 1_000} — read-side scaling
/// - TotalSegments: {1_000, 10_000} — storage size impact on FindIntersecting
/// - StorageStrategy: Snapshot vs LinkedList
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class SingleGapPartialHitBenchmarks
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private SynchronousDataSource _dataSource = null!;
    private IntegerFixedStepDomain _domain;
    private Range<int> _singleGapRange;

    private const int SegmentSpan = 10;

    /// <summary>
    /// Number of existing segments the request intersects — measures read-side scaling.
    /// </summary>
    [Params(1, 10, 100, 1_000)]
    public int IntersectingSegments { get; set; }

    /// <summary>
    /// Total segments in cache — measures storage size impact on FindIntersecting.
    /// </summary>
    [Params(1_000, 10_000)]
    public int TotalSegments { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot vs LinkedList.
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _dataSource = new SynchronousDataSource(_domain);

        // SingleGap: request spans IntersectingSegments existing segments + 1 gap at the right edge
        // Existing segments: [0,9], [10,19], ..., [(IntersectingSegments-1)*10, IntersectingSegments*10-1]
        // Request extends SegmentSpan beyond the last intersecting segment into uncached territory
        const int requestStart = 0;
        var requestEnd = (IntersectingSegments * SegmentSpan) + SegmentSpan - 1;
        _singleGapRange = Factories.Range.Closed<int>(requestStart, requestEnd);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Fresh cache per iteration: the benchmark stores the gap segment each time
        _cache = VpcCacheHelpers.CreateCache(
            _dataSource, _domain, StorageStrategy,
            maxSegmentCount: TotalSegments + 1000,
            appendBufferSize: 8);

        // Populate TotalSegments adjacent segments
        VpcCacheHelpers.PopulateSegments(_cache, TotalSegments, SegmentSpan);
    }

    /// <summary>
    /// Measures partial hit cost with a single gap.
    /// IntersectingSegments existing segments are hit; 1 gap is fetched and stored.
    /// Isolates read-side scaling: FindIntersecting + ComputeGaps cost vs K intersecting segments.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap()
    {
        await _cache!.GetDataAsync(_singleGapRange, CancellationToken.None);
        await _cache.WaitForIdleAsync();
    }
}
