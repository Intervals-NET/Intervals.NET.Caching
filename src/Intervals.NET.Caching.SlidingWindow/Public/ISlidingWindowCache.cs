using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Caching.Layered;
using Intervals.NET.Caching.SlidingWindow.Public.Configuration;

namespace Intervals.NET.Caching.SlidingWindow.Public;

/// <summary>
/// Represents a sliding window cache that retrieves and caches data for specified ranges,
/// with automatic rebalancing based on access patterns.
/// </summary>
/// <typeparam name="TRange">
/// The type representing the range boundaries. Must implement <see cref="IComparable{T}"/>.
/// </typeparam>
/// <typeparam name="TData">
/// The type of data being cached.
/// </typeparam>
/// <typeparam name="TDomain">
/// The type representing the domain of the ranges. Must implement <see cref="IRangeDomain{TRange}"/>.
/// Supports both fixed-step (O(1)) and variable-step (O(N)) domains. While variable-step domains
/// have O(N) complexity for range calculations, this cost is negligible compared to data source I/O.
/// </typeparam>

public interface ISlidingWindowCache<TRange, TData, TDomain> : IRangeCache<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    /// <summary>
    /// Atomically updates one or more runtime configuration values on the live cache instance.
    /// </summary>
    /// <param name="configure">
    /// A delegate that receives a <see cref="RuntimeOptionsUpdateBuilder"/> and applies the desired changes.
    /// Only the fields explicitly set on the builder are changed; all others retain their current values.
    /// </param>
    /// <remarks>
    /// Only the fields explicitly set on the builder are changed; all others retain their current values.
    /// The merged options are validated before publishing. If validation fails, an exception is thrown
    /// and the current options are left unchanged. Updates take effect on the next rebalance cycle.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed cache instance.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any updated value fails validation.</exception>
    /// <exception cref="ArgumentException">Thrown when the merged threshold sum exceeds 1.0.</exception>
    void UpdateRuntimeOptions(Action<RuntimeOptionsUpdateBuilder> configure);

    /// <summary>
    /// Gets a snapshot of the current runtime-updatable option values on this cache instance.
    /// </summary>
    /// <remarks>
    /// The returned snapshot captures values at the moment the property is read. Obtain a new
    /// snapshot after calling <see cref="UpdateRuntimeOptions"/> to see updated values.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed cache instance.</exception>
    RuntimeOptionsSnapshot CurrentRuntimeOptions { get; }
}
