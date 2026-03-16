using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency cache-hit benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: GetDataAsync returns as soon as the normalization
/// event is enqueued — background LRU metadata updates are NOT included in the measurement.
/// IterationCleanup drains pending background events after each iteration to prevent
/// accumulation across the benchmark run.
/// See <see cref="VpcCacheHitBenchmarksBase"/> for setup methodology and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcCacheHitEventualBenchmarks : VpcCacheHitBenchmarksBase
{
    /// <summary>
    /// Measures User Path latency for a full cache hit: data assembly only.
    /// Background LRU metadata update is enqueued but not awaited.
    /// </summary>
    [Benchmark]
    public async Task<ReadOnlyMemory<int>> CacheHit()
    {
        return (await Cache!.GetDataAsync(HitRange, CancellationToken.None)).Data;
    }

    /// <summary>
    /// Drains background normalization events (LRU metadata updates) published
    /// during the benchmark iteration before the next iteration starts.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        Cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
