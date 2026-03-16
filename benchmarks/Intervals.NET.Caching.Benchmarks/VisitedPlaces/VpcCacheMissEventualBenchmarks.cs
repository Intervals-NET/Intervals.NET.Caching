using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Eventual-consistency cache-miss benchmarks for VisitedPlaces Cache.
/// Measures User Path latency only: data source fetch + normalization event enqueue.
/// Background segment storage and eviction are NOT included in the measurement.
/// IterationCleanup drains the background loop after each iteration so the next
/// IterationSetup starts with a clean slate.
/// See <see cref="VpcCacheMissBenchmarksBase"/> for setup methodology and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcCacheMissEventualBenchmarks : VpcCacheMissBenchmarksBase
{
    [IterationSetup(Target = nameof(CacheMiss_NoEviction))]
    public void IterationSetup_NoEviction() => SetupNoEvictionCache();

    [IterationSetup(Target = nameof(CacheMiss_WithEviction))]
    public void IterationSetup_WithEviction() => SetupWithEvictionCache();

    /// <summary>
    /// Measures User Path cache-miss cost without eviction: data source fetch only.
    /// Background normalization (segment storage) is enqueued but not awaited.
    /// </summary>
    [Benchmark]
    public async Task CacheMiss_NoEviction()
    {
        await Cache!.GetDataAsync(MissRange, CancellationToken.None);
    }

    /// <summary>
    /// Measures User Path cache-miss cost with eviction: data source fetch only.
    /// Background normalization (segment storage + eviction) is enqueued but not awaited.
    /// </summary>
    [Benchmark]
    public async Task CacheMiss_WithEviction()
    {
        await Cache!.GetDataAsync(MissRange, CancellationToken.None);
    }

    /// <summary>
    /// Drains background normalization (segment storage + optional eviction) published
    /// during the benchmark iteration before the next IterationSetup creates a fresh cache.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        Cache!.WaitForIdleAsync().GetAwaiter().GetResult();
    }
}
