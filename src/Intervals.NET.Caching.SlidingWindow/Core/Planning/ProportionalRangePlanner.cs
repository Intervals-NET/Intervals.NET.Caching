using Intervals.NET.Caching.Extensions;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.SlidingWindow.Core.State;

namespace Intervals.NET.Caching.SlidingWindow.Core.Planning;

/// <summary>
/// Computes the canonical DesiredCacheRange for a given user RequestedRange and cache geometry configuration. See docs/sliding-window/ for design details.
/// </summary>
/// <typeparam name="TRange">Type representing the boundaries of a window/range.</typeparam>
/// <typeparam name="TDomain">Provides domain-specific logic to compute spans, boundaries, and interval arithmetic for <c>TRange</c>.</typeparam>
internal sealed class ProportionalRangePlanner<TRange, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly RuntimeCacheOptionsHolder _optionsHolder;
    private readonly TDomain _domain;

    /// <summary>
    /// Initializes a new instance of <see cref="ProportionalRangePlanner{TRange, TDomain}"/>.
    /// </summary>
    /// <param name="optionsHolder">Shared holder for the current runtime options snapshot.</param>
    /// <param name="domain">Domain implementation used for range arithmetic and span calculations.</param>
    public ProportionalRangePlanner(RuntimeCacheOptionsHolder optionsHolder, TDomain domain)
    {
        _optionsHolder = optionsHolder;
        _domain = domain;
    }

    /// <summary>
    /// Computes the canonical DesiredCacheRange for a given <paramref name="requested"/> range, expanding left/right according to the current runtime configuration.
    /// </summary>
    /// <param name="requested">User-requested range for which cache expansion should be planned.</param>
    /// <returns>
    /// The canonical DesiredCacheRange representing the window the cache should hold.
    /// </returns>
    public Range<TRange> Plan(Range<TRange> requested)
    {
        // Snapshot current options once for consistency within this invocation
        var options = _optionsHolder.Current;

        var size = requested.Span(_domain);

        var left = size.Value * options.LeftCacheSize;
        var right = size.Value * options.RightCacheSize;

        return requested.Expand(
            domain: _domain,
            left: (long)Math.Round(left),
            right: (long)Math.Round(right)
        );
    }
}
