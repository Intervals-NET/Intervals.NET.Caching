using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency cache-hit benchmarks for VisitedPlaces Cache.
/// Measures the complete per-request cost: User Path data assembly plus background
/// LRU metadata update. WaitForIdleAsync is inside the measurement boundary.
/// See <see cref="VpcCacheHitBenchmarksBase"/> for setup methodology and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcCacheHitStrongBenchmarks : VpcCacheHitBenchmarksBase
{
    /// <summary>
    /// Measures complete cache-hit cost: data assembly + background LRU metadata update.
    /// WaitForIdleAsync ensures the background normalization event is fully processed
    /// before the benchmark iteration completes.
    /// </summary>
    [Benchmark]
    public async Task<ReadOnlyMemory<int>> CacheHit()
    {
        var result = (await Cache!.GetDataAsync(HitRange, CancellationToken.None)).Data;
        await Cache.WaitForIdleAsync();
        return result;
    }
}
