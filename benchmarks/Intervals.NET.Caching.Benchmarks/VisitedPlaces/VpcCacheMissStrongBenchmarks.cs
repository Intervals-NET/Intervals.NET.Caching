using BenchmarkDotNet.Attributes;
using Intervals.NET.Caching.Benchmarks.VisitedPlaces.Base;

namespace Intervals.NET.Caching.Benchmarks.VisitedPlaces;

/// <summary>
/// Strong-consistency cache-miss benchmarks for VisitedPlaces Cache.
/// Measures the complete end-to-end miss cost: data source fetch + background segment
/// storage (+ optional eviction). WaitForIdleAsync is inside the measurement boundary.
/// See <see cref="VpcCacheMissBenchmarksBase"/> for setup methodology and parameters.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class VpcCacheMissStrongBenchmarks : VpcCacheMissBenchmarksBase
{
    [IterationSetup(Target = nameof(CacheMiss_NoEviction))]
    public void IterationSetup_NoEviction() => SetupNoEvictionCache();

    [IterationSetup(Target = nameof(CacheMiss_WithEviction))]
    public void IterationSetup_WithEviction() => SetupWithEvictionCache();

    /// <summary>
    /// Measures complete cache-miss cost without eviction.
    /// Includes: data source fetch + normalization (segment storage + metadata update).
    /// </summary>
    [Benchmark]
    public async Task CacheMiss_NoEviction()
    {
        await Cache!.GetDataAsync(MissRange, CancellationToken.None);
        await Cache.WaitForIdleAsync();
    }

    /// <summary>
    /// Measures complete cache-miss cost with eviction.
    /// Includes: data source fetch + normalization (segment storage + eviction evaluation
    /// + eviction execution).
    /// </summary>
    [Benchmark]
    public async Task CacheMiss_WithEviction()
    {
        await Cache!.GetDataAsync(MissRange, CancellationToken.None);
        await Cache.WaitForIdleAsync();
    }
}
