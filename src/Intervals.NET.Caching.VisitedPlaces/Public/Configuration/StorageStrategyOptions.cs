using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Abstract base class for all storage strategy configuration objects.
/// Carries tuning parameters and is responsible for constructing the corresponding
/// <see cref="ISegmentStorage{TRange,TData}"/> implementation at cache build time.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para>
/// Concrete strategy options classes (e.g., <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}"/>,
/// <see cref="LinkedListStrideIndexStorageOptions{TRange,TData}"/>) inherit from this class
/// and implement <see cref="Create"/> to instantiate their storage.
/// </para>
/// <para>
/// Pass a concrete instance to
/// <see cref="VisitedPlacesCacheOptionsBuilder{TRange,TData}.WithStorageStrategy"/> or directly
/// to the <see cref="VisitedPlacesCacheOptions{TRange,TData}"/> constructor. The <see cref="Create"/>
/// method is internal — callers never invoke it directly.
/// </para>
/// </remarks>
public abstract class StorageStrategyOptions<TRange, TData>
    where TRange : IComparable<TRange>
{
    // Prevent external inheritance outside this assembly while keeping the type public.
    internal StorageStrategyOptions() { }

    /// <summary>
    /// Creates and returns a new <see cref="ISegmentStorage{TRange,TData}"/> instance
    /// configured according to the options on this object.
    /// </summary>
    internal abstract ISegmentStorage<TRange, TData> Create();
}
