using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Configuration and factory for the LinkedList + Stride Index storage strategy.
/// Optimised for larger caches (&gt;85 KB total data, &gt;~50 segments) where a single large
/// sorted array would create Large Object Heap pressure.
/// </summary>
public sealed class LinkedListStrideIndexStorageOptions<TRange, TData>
    : StorageStrategyOptions<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// A default instance using <see cref="AppendBufferSize"/> = 8 and <see cref="Stride"/> = 16.
    /// </summary>
    public static readonly LinkedListStrideIndexStorageOptions<TRange, TData> Default = new();

    /// <summary>
    /// Number of segments accumulated in the stride append buffer before the stride index
    /// normalization pass is triggered. Controls both the pre-allocated buffer array size
    /// and the flush threshold. Must be &gt;= 1. Default: 8.
    /// </summary>
    public int AppendBufferSize { get; }

    /// <summary>
    /// Distance between stride anchors in the sorted linked list.
    /// Every <see cref="Stride"/>-th node is recorded as an anchor in the stride index,
    /// enabling O(log(n/N)) binary search followed by an O(N) local list walk on the User Path.
    /// Must be &gt;= 1. Default: 16.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Initializes a new <see cref="LinkedListStrideIndexStorageOptions{TRange,TData}"/>
    /// with the specified buffer size and stride.
    /// </summary>
    /// <param name="appendBufferSize">
    /// Number of segments accumulated before stride index normalization is triggered.
    /// Must be &gt;= 1. Default: 8.
    /// </param>
    /// <param name="stride">
    /// Distance between stride anchors in the sorted linked list.
    /// Must be &gt;= 1. Default: 16.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="appendBufferSize"/> or <paramref name="stride"/> is less than 1.
    /// </exception>
    public LinkedListStrideIndexStorageOptions(int appendBufferSize = 8, int stride = 16)
    {
        if (appendBufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appendBufferSize),
                "AppendBufferSize must be greater than or equal to 1.");
        }

        if (stride < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                "Stride must be greater than or equal to 1.");
        }

        AppendBufferSize = appendBufferSize;
        Stride = stride;
    }

    /// <inheritdoc/>
    internal override ISegmentStorage<TRange, TData> Create() =>
        new LinkedListStrideIndexStorage<TRange, TData>(AppendBufferSize, Stride);

    /// <inheritdoc/>
    public bool Equals(LinkedListStrideIndexStorageOptions<TRange, TData>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return AppendBufferSize == other.AppendBufferSize
               && Stride == other.Stride;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is LinkedListStrideIndexStorageOptions<TRange, TData> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(AppendBufferSize, Stride);

    /// <summary>Returns <c>true</c> if the two instances are equal.</summary>
    public static bool operator ==(
        LinkedListStrideIndexStorageOptions<TRange, TData>? left,
        LinkedListStrideIndexStorageOptions<TRange, TData>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Returns <c>true</c> if the two instances are not equal.</summary>
    public static bool operator !=(
        LinkedListStrideIndexStorageOptions<TRange, TData>? left,
        LinkedListStrideIndexStorageOptions<TRange, TData>? right) =>
        !(left == right);
}
