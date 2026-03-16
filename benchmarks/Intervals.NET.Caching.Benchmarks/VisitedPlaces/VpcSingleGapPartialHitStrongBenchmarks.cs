using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency single-gap partial-hit benchmarks for VisitedPlaces Cache.
/// Measures the complete per-request cost: User Path data assembly + data source fetch
/// for the gap + background segment storage. WaitForIdleAsync is inside the measurement
/// boundary.
/// See <see cref="VpcSingleGapPartialHitBenchmarksBase"/> for layout, methodology, and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcSingleGapPartialHitStrongBenchmarks : VpcSingleGapPartialHitBenchmarksBase
{
    [IterationSetup(Target = nameof(PartialHit_SingleGap_OneHit))]
    public void IterationSetup_OneHit() => SetupOneHitCache();

    [IterationSetup(Target = nameof(PartialHit_SingleGap_TwoHits))]
    public void IterationSetup_TwoHits() => SetupTwoHitsCache();

    /// <summary>
    /// Partial hit: request [0,9] crosses the initial gap [0,4] into segment [5,14].
    /// Produces 1 gap fetch + 1 cache hit. Includes background segment storage cost.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_OneHit()
    {
        await Cache!.GetDataAsync(OneHitRange, CancellationToken.None);
        await Cache.WaitForIdleAsync();
    }

    /// <summary>
    /// Partial hit: request [12,21] spans across gap [15,19] touching segments [5,14] and [20,29].
    /// Produces 1 gap fetch + 2 cache hits. Includes background segment storage cost.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_SingleGap_TwoHits()
    {
        await Cache!.GetDataAsync(TwoHitsRange, CancellationToken.None);
        await Cache.WaitForIdleAsync();
    }
}
