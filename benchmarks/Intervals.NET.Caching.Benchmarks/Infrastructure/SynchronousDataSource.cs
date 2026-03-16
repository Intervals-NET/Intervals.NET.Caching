using Intervals.NET.Caching.Dto;
using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Domain.Extensions.Fixed;

namespace Intervals.NET.Caching.Benchmarks.Infrastructure;

/// <summary>
/// Zero-latency synchronous IDataSource for benchmark learning passes.
/// Auto-caches every FetchAsync result so subsequent calls for the same range are
/// allocation-free. Call Freeze() after the learning pass to obtain a FrozenDataSource
/// and disable this instance.
/// </summary>
public sealed class SynchronousDataSource : IDataSource<int, int>
{
    private readonly IntegerFixedStepDomain _domain;
    private Dictionary<Range<int>, RangeChunk<int, int>>? _cache = new();

    public SynchronousDataSource(IntegerFixedStepDomain domain)
    {
        _domain = domain;
    }

    /// <summary>
    /// Transfers dictionary ownership to a new <see cref="FrozenDataSource"/> and disables
    /// this instance. Any FetchAsync call after Freeze() throws InvalidOperationException.
    /// </summary>
    public FrozenDataSource Freeze()
    {
        var cache = _cache ?? throw new InvalidOperationException(
            "SynchronousDataSource has already been frozen.");
        _cache = null;
        return new FrozenDataSource(cache);
    }

    /// <summary>
    /// Fetches data for a single range with zero latency.
    /// Returns cached data if available; otherwise generates, caches, and returns new data.
    /// </summary>
    public Task<RangeChunk<int, int>> FetchAsync(Range<int> range, CancellationToken cancellationToken)
    {
        var cache = _cache ?? throw new InvalidOperationException(
            "SynchronousDataSource has been frozen. Use the FrozenDataSource returned by Freeze().");

        if (!cache.TryGetValue(range, out var cached))
        {
            cached = new RangeChunk<int, int>(range, GenerateDataForRange(range).ToArray());
            cache[range] = cached;
        }

        return Task.FromResult(cached);
    }

    /// <summary>
    /// Fetches data for multiple ranges with zero latency.
    /// Returns cached data per range where available; caches any new ranges.
    /// </summary>
    public Task<IEnumerable<RangeChunk<int, int>>> FetchAsync(
        IEnumerable<Range<int>> ranges,
        CancellationToken cancellationToken)
    {
        var cache = _cache ?? throw new InvalidOperationException(
            "SynchronousDataSource has been frozen. Use the FrozenDataSource returned by Freeze().");

        var chunks = ranges.Select(range =>
        {
            if (!cache.TryGetValue(range, out var cached))
            {
                cached = new RangeChunk<int, int>(range, GenerateDataForRange(range).ToArray());
                cache[range] = cached;
            }

            return cached;
        });

        return Task.FromResult(chunks);
    }

    /// <summary>
    /// Generates deterministic data for a range: position i produces value i.
    /// </summary>
    private IEnumerable<int> GenerateDataForRange(Range<int> range)
    {
        var start = range.Start.Value;
        var count = (int)range.Span(_domain).Value;

        for (var i = 0; i < count; i++)
        {
            yield return start + i;
        }
    }
}
