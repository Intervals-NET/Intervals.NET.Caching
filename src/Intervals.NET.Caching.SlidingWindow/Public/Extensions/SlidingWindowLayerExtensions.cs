using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.Layered;
using Intervals.NET.Caching.SlidingWindow.Public.Cache;
using Intervals.NET.Caching.SlidingWindow.Public.Configuration;
using Intervals.NET.Caching.SlidingWindow.Public.Instrumentation;

namespace Intervals.NET.Caching.SlidingWindow.Public.Extensions;

/// <summary>
/// Extension methods on <see cref="LayeredRangeCacheBuilder{TRange,TData,TDomain}"/> that add
/// a <see cref="SlidingWindowCache{TRange,TData,TDomain}"/> layer to the cache stack.
/// </summary>
public static class SlidingWindowLayerExtensions
{
    /// <summary>
    /// Adds a <see cref="SlidingWindowCache{TRange,TData,TDomain}"/> layer configured with
    /// a pre-built <see cref="SlidingWindowCacheOptions"/> instance.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <typeparam name="TDomain">The range domain type. Must implement <see cref="IRangeDomain{TRange}"/>.</typeparam>
    /// <param name="builder">The layered cache builder to add the layer to.</param>
    /// <param name="options">The configuration options for this layer's SlidingWindowCache.</param>
    /// <param name="diagnostics">
    /// Optional diagnostics implementation. When <c>null</c>, <see cref="NoOpDiagnostics.Instance"/> is used.
    /// </param>
    /// <returns>The same builder instance, for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is <c>null</c>.
    /// </exception>
    public static LayeredRangeCacheBuilder<TRange, TData, TDomain> AddSlidingWindowLayer<TRange, TData, TDomain>(
        this LayeredRangeCacheBuilder<TRange, TData, TDomain> builder,
        SlidingWindowCacheOptions options,
        ISlidingWindowCacheDiagnostics? diagnostics = null)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
    {
        ArgumentNullException.ThrowIfNull(options);

        var domain = builder.Domain;
        return builder.AddLayer(dataSource =>
            new SlidingWindowCache<TRange, TData, TDomain>(dataSource, domain, options, diagnostics));
    }

    /// <summary>
    /// Adds a <see cref="SlidingWindowCache{TRange,TData,TDomain}"/> layer configured inline
    /// using a fluent <see cref="SlidingWindowCacheOptionsBuilder"/>.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TData">The type of data being cached.</typeparam>
    /// <typeparam name="TDomain">The range domain type. Must implement <see cref="IRangeDomain{TRange}"/>.</typeparam>
    /// <param name="builder">The layered cache builder to add the layer to.</param>
    /// <param name="configure">A delegate that applies the desired settings for this layer's options.</param>
    /// <param name="diagnostics">
    /// Optional diagnostics implementation. When <c>null</c>, <see cref="NoOpDiagnostics.Instance"/> is used.
    /// </param>
    /// <returns>The same builder instance, for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is <c>null</c>.
    /// </exception>
    public static LayeredRangeCacheBuilder<TRange, TData, TDomain> AddSlidingWindowLayer<TRange, TData, TDomain>(
        this LayeredRangeCacheBuilder<TRange, TData, TDomain> builder,
        Action<SlidingWindowCacheOptionsBuilder> configure,
        ISlidingWindowCacheDiagnostics? diagnostics = null)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange>
    {
        ArgumentNullException.ThrowIfNull(configure);

        var domain = builder.Domain;
        return builder.AddLayer(dataSource =>
        {
            var optionsBuilder = new SlidingWindowCacheOptionsBuilder();
            configure(optionsBuilder);
            var options = optionsBuilder.Build();
            return new SlidingWindowCache<TRange, TData, TDomain>(dataSource, domain, options, diagnostics);
        });
    }
}
