using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency multiple-gaps partial-hit benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: data source fetches for all K gaps + normalization
/// event enqueue. Background segment storage is NOT included in the measurement.
/// IterationCleanup drains the background loop after each iteration so the next
/// IterationSetup starts with a clean slate.
/// See <see cref="VpcMultipleGapsPartialHitBenchmarksBase"/> for layout, methodology, and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcMultipleGapsPartialHitEventualBenchmarks : VpcMultipleGapsPartialHitBenchmarksBase
{
    [IterationSetup]
    public void IterationSetup() => SetupCache();

    /// <summary>
    /// Measures User Path partial-hit cost with multiple gaps.
    /// GapCount+1 existing segments hit; GapCount gaps fetched from the data source.
    /// Background storage of K gap segments is enqueued but not awaited.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_MultipleGaps()
    {
        await Cache!.GetDataAsync(MultipleGapsRange, CancellationToken.None);
    }

    /// <summary>
    /// Drains background normalization (K gap segment stores) published during the
    /// benchmark iteration before the next IterationSetup creates a fresh cache.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        Cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
