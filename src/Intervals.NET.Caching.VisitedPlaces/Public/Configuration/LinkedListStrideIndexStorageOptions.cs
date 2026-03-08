using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Configuration and factory for the LinkedList + Stride Index storage strategy.
/// Optimised for larger caches (&gt;85 KB total data, &gt;~50 segments) where a single large
/// sorted array would create Large Object Heap pressure.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Selecting this strategy:</strong></para>
/// <para>
/// Pass an instance of this class to
/// <see cref="VisitedPlacesCacheOptionsBuilder{TRange,TData}.WithStorageStrategy"/> to select the
/// LinkedList + Stride Index implementation. The object carries all tuning parameters and is
/// responsible for constructing the storage instance at cache build time.
/// </para>
/// <para><strong>How the stride append buffer works:</strong></para>
/// <para>
/// New segments are inserted into the sorted linked list immediately, but are also written to a
/// small fixed-size stride append buffer. When the buffer reaches <see cref="AppendBufferSize"/>
/// entries, a normalization pass rebuilds the stride index and publishes it atomically via
/// <c>Volatile.Write</c> (RCU semantics, Invariant VPC.B.5).
/// </para>
/// <para><strong>Tuning <see cref="AppendBufferSize"/>:</strong></para>
/// <list type="bullet">
/// <item><description>
/// <strong>Smaller value</strong> — stride index rebuilt more frequently; index stays more
/// up-to-date, but normalization CPU cost (O(n) list traversal) is paid more often.
/// </description></item>
/// <item><description>
/// <strong>Larger value</strong> — stride index rebuilt less often; lower amortized CPU cost,
/// but the index may lag behind recently added segments for longer between rebuilds.
/// Note: new segments are always in the linked list and are still found by
/// <c>FindIntersecting</c> regardless of stride index staleness.
/// </description></item>
/// <item><description>
/// <strong>Default (8)</strong> — appropriate for most workloads. Only tune under profiling.
/// </description></item>
/// </list>
/// <para><strong>Tuning <see cref="Stride"/>:</strong></para>
/// <list type="bullet">
/// <item><description>
/// <strong>Smaller stride</strong> — denser index; faster lookup (shorter list walk from anchor),
/// but more memory for the stride index array and more nodes to update on normalization.
/// </description></item>
/// <item><description>
/// <strong>Larger stride</strong> — sparser index; slower lookup (longer list walk from anchor),
/// but less memory. Diminishing returns beyond ~32 for typical segment counts.
/// </description></item>
/// <item><description>
/// <strong>Default (16)</strong> — a balanced default. Tune based on your typical segment count
/// and read/write ratio.
/// </description></item>
/// </list>
/// <para>See <c>docs/visited-places/storage-strategies.md</c> for a full strategy comparison.</para>
/// </remarks>
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
