using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency single-gap partial-hit benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: data source fetch for the gap + normalization event
/// enqueue. Background segment storage is NOT included in the measurement.
/// IterationCleanup drains the background loop after each iteration so the next
/// IterationSetup starts with a clean slate.
/// See <see cref="VpcSingleGapPartialHitBenchmarksBase"/> for layout, methodology, and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcSingleGapPartialHitEventualBenchmarks : VpcSingleGapPartialHitBenchmarksBase
{
    [IterationSetup(Target = nameof(PartialHit_SingleGap_OneHit))]
    public void IterationSetup_OneHit() => SetupOneHitCache();

    [IterationSetup(Target = nameof(PartialHit_SingleGap_TwoHits))]
    public void IterationSetup_TwoHits() => SetupTwoHitsCache();

    /// <summary>
    /// Partial hit: request [0,9] crosses the initial gap [0,4] into segment [5,14].
    /// Produces 1 gap fetch + 1 cache hit. Background segment storage is not awaited.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_OneHit()
    {
        await Cache!.GetDataAsync(OneHitRange, CancellationToken.None);
    }

    /// <summary>
    /// Partial hit: request [12,21] spans across gap [15,19] touching segments [5,14] and [20,29].
    /// Produces 1 gap fetch + 2 cache hits. Background segment storage is not awaited.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_TwoHits()
    {
        await Cache!.GetDataAsync(TwoHitsRange, CancellationToken.None);
    }

    /// <summary>
    /// Drains background normalization (gap segment storage) published during the benchmark
    /// iteration before the next IterationSetup creates a fresh cache.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        Cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
