using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency multiple-gaps partial-hit benchmarks for VisitedPlaces Cache.
/// Measures the complete end-to-end cost: User Path data assembly + data source fetches
/// for all K gaps + background segment storage (K stores, K/AppendBufferSize normalizations).
/// WaitForIdleAsync is inside the measurement boundary.
/// See <see cref="VpcMultipleGapsPartialHitBenchmarksBase"/> for layout, methodology, and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcMultipleGapsPartialHitStrongBenchmarks : VpcMultipleGapsPartialHitBenchmarksBase
{
    [IterationSetup]
    public void IterationSetup() => SetupCache();

    /// <summary>
    /// Measures complete partial-hit cost with multiple gaps.
    /// GapCount+1 existing segments hit; GapCount gaps fetched and stored.
    /// GapCount stores → GapCount/AppendBufferSize normalizations.
    /// Tests write-side scaling: normalization cost vs gap count and buffer size.
    /// </summary>
    [Benchmark]
    public async Task PartialHit_MultipleGaps()
    {
        await Cache!.GetDataAsync(MultipleGapsRange, CancellationToken.None);
        await Cache.WaitForIdleAsync();
    }
}
