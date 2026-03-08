namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Fluent builder for constructing <see cref="VisitedPlacesCacheOptions{TRange,TData}"/>.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// Obtain an instance via
/// <see cref="Cache.VisitedPlacesCacheBuilder{TRange,TData,TDomain}.WithOptions(Action{VisitedPlacesCacheOptionsBuilder{TRange,TData}})"/>.
/// </remarks>
public sealed class VisitedPlacesCacheOptionsBuilder<TRange, TData>
    where TRange : IComparable<TRange>
{
    private StorageStrategyOptions<TRange, TData> _storageStrategy =
        SnapshotAppendBufferStorageOptions<TRange, TData>.Default;
    private int _eventChannelCapacity = 128;

    /// <summary>
    /// Sets the storage strategy by supplying a typed options object.
    /// Defaults to <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}.Default"/>.
    /// </summary>
    /// <param name="strategy">
    /// A storage strategy options object, such as
    /// <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}"/> or
    /// <see cref="LinkedListStrideIndexStorageOptions{TRange,TData}"/>.
    /// Must be non-null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="strategy"/> is <see langword="null"/>.
    /// </exception>
    public VisitedPlacesCacheOptionsBuilder<TRange, TData> WithStorageStrategy(
        StorageStrategyOptions<TRange, TData> strategy)
    {
        _storageStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    /// <summary>
    /// Sets the background event channel capacity.
    /// Defaults to 128.
    /// </summary>
    public VisitedPlacesCacheOptionsBuilder<TRange, TData> WithEventChannelCapacity(int capacity)
    {
        _eventChannelCapacity = capacity;
        return this;
    }

    /// <summary>
    /// Builds and returns a <see cref="VisitedPlacesCacheOptions{TRange,TData}"/> with the configured values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any value fails validation.
    /// </exception>
    public VisitedPlacesCacheOptions<TRange, TData> Build() => new(_storageStrategy, _eventChannelCapacity);
}
