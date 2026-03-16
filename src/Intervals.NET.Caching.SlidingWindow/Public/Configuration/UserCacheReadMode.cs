namespace Intervals.NET.Caching.SlidingWindow.Public.Configuration;

/// <summary>
/// Defines how materialized cache data is exposed to users.
/// </summary>
/// <remarks>
/// Configured once at cache creation time and cannot be changed at runtime.
/// </remarks>
public enum UserCacheReadMode
{
    /// <summary>
    /// Stores data in a contiguous array internally.
    /// User reads return <see cref="ReadOnlyMemory{T}"/> pointing directly to the internal array.
    /// Zero-allocation reads; rebalance always allocates a new array.
    /// </summary>
    Snapshot,

    /// <summary>
    /// Stores data in a growable structure internally.
    /// User reads allocate a new array for the requested range and return it as <see cref="ReadOnlyMemory{T}"/>.
    /// Cheaper rebalance with less memory pressure; allocates on every read.
    /// </summary>
    CopyOnRead
}
