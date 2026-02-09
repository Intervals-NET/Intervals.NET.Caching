using Intervals.NET;
using Intervals.NET.Domain.Abstractions;
using SlidingWindowCache.Configuration;
using SlidingWindowCache.Extensions;

namespace SlidingWindowCache.DesiredRangePlanner;

internal readonly struct ProportionalRangePlanner<TRange, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly WindowCacheOptions _options;
    private readonly TDomain _domain;

    public ProportionalRangePlanner(WindowCacheOptions options, TDomain domain)
    {
        _options = options;
        _domain = domain;
    }

    public Range<TRange> Plan(Range<TRange> requested)
    {
        var size = requested.Span(_domain);

        var left = size.Value * _options.LeftCacheSize;
        var right = size.Value * _options.RightCacheSize;

        return requested.Expand(
            domain: _domain,
            left: (long)left,
            right: (long)right
        );
    }
}