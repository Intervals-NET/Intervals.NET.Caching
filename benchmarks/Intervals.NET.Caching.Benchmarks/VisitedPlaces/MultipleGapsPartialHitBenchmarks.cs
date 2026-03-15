using BenchmarkDotNet.Attributes;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.Benchmarks.Infrastructure;
using Intervals.NET.Caching.VisitedPlaces.Public.Cache;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Multiple-Gaps Partial Hit Benchmarks for VisitedPlaces Cache.
/// Measures write-side scaling: K+1 existing segments hit with K internal gaps.
/// K gaps → K stores → K/AppendBufferSize normalizations.
/// 
/// Isolates: normalization cost as GapCount grows, and how AppendBufferSize amortizes it.
/// 
/// Methodology:
/// - Cache pre-populated with alternating segment/gap layout in IterationSetup
/// - Request spans the entire alternating pattern, hitting all K gaps
/// - WaitForIdleAsync INSIDE benchmark (measuring complete partial hit + normalization cost)
/// - Fresh cache per iteration (benchmark stores K new gap segments each time)
/// 
/// Parameters:
/// - GapCount: {1, 10, 100, 1_000} — write-side scaling (K stores per invocation)
/// - MultiGapTotalSegments: {1_000, 10_000} — background segment count
/// - StorageStrategy: Snapshot vs LinkedList
/// - AppendBufferSize: {1, 8} — normalization frequency (every store vs every 8 stores)
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class MultipleGapsPartialHitBenchmarks
{
    private VisitedPlacesCache<int, int, IntegerFixedStepDomain>? _cache;
    private SynchronousDataSource _dataSource = null!;
    private IntegerFixedStepDomain _domain;
    private Range<int> _multipleGapsRange;

    private const int SegmentSpan = 10;
    private const int GapSize = SegmentSpan; // Gap size = segment span for uniform layout

    /// <summary>
    /// Number of internal gaps — each gap produces one data source fetch and one store.
    /// K stores → K/AppendBufferSize normalizations.
    /// </summary>
    [Params(1, 10, 100, 1_000)]
    public int GapCount { get; set; }

    /// <summary>
    /// Total background segments in cache (beyond the gap pattern).
    /// Controls storage overhead and FindIntersecting baseline cost.
    /// </summary>
    [Params(1_000, 10_000)]
    public int MultiGapTotalSegments { get; set; }

    /// <summary>
    /// Storage strategy — Snapshot vs LinkedList.
    /// </summary>
    [Params(StorageStrategyType.Snapshot, StorageStrategyType.LinkedList)]
    public StorageStrategyType StorageStrategy { get; set; }

    /// <summary>
    /// Append buffer size — controls normalization frequency.
    /// 1 = normalize every store, 8 = normalize every 8 stores (default).
    /// </summary>
    [Params(1, 8)]
    public int AppendBufferSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _domain = new IntegerFixedStepDomain();
        _dataSource = new SynchronousDataSource(_domain);

        // Request spans all non-adjacent segments (hitting all gaps)
        // Layout: alternating segments and gaps, each span=10
        // stride = SegmentSpan + GapSize = 20
        // GapCount+1 segments exist: at positions 0, 20, 40, ...
        var nonAdjacentCount = GapCount + 1;
        var stride = SegmentSpan + GapSize;
        var requestEnd = (nonAdjacentCount - 1) * stride + SegmentSpan - 1;
        _multipleGapsRange = Factories.Range.Closed<int>(0, requestEnd);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Fresh cache per iteration: the benchmark stores GapCount new segments each time
        var nonAdjacentCount = GapCount + 1;

        _cache = VpcCacheHelpers.CreateCache(
            _dataSource, _domain, StorageStrategy,
            maxSegmentCount: MultiGapTotalSegments + 1000,
            appendBufferSize: AppendBufferSize);

        // First populate the non-adjacent segments that create the gap pattern
        VpcCacheHelpers.PopulateWithGaps(_cache, nonAdjacentCount, SegmentSpan, GapSize);

        // Then populate remaining segments beyond the gap pattern to reach MultiGapTotalSegments
        var remainingCount = MultiGapTotalSegments - nonAdjacentCount;
        if (remainingCount > 0)
        {
            var startAfterPattern = nonAdjacentCount * (SegmentSpan + GapSize) + GapSize;
            VpcCacheHelpers.PopulateSegments(_cache, remainingCount, SegmentSpan, startAfterPattern);
        }
    }

    /// <summary>
    /// Measures partial hit cost with multiple gaps.
    /// GapCount+1 existing segments hit; GapCount gaps fetched and stored.
    /// GapCount stores → GapCount/AppendBufferSize normalizations.
    /// Tests write-side scaling: normalization cost vs gap count and buffer size.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_MultipleGaps()
    {
        await _cache!.GetDataAsync(_multipleGapsRange, CancellationToken.None);
        await _cache.WaitForIdleAsync();
    }
}
