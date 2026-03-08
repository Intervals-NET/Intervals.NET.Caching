using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;

namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Configuration and factory for the Snapshot + Append Buffer storage strategy.
/// Optimised for smaller caches (&lt;85 KB total data, &lt;~50 segments) with high read-to-write ratios.
/// </summary>
/// <typeparam name="TRange">The type representing range boundaries.</typeparam>
/// <typeparam name="TData">The type of data being cached.</typeparam>
/// <remarks>
/// <para><strong>Selecting this strategy:</strong></para>
/// <para>
/// Pass an instance of this class to
/// <see cref="VisitedPlacesCacheOptionsBuilder{TRange,TData}.WithStorageStrategy"/> to select the
/// Snapshot + Append Buffer implementation. The object carries all tuning parameters and is
/// responsible for constructing the storage instance at cache build time.
/// </para>
/// <para><strong>How the append buffer works:</strong></para>
/// <para>
/// New segments are written to a small fixed-size buffer rather than being immediately integrated
/// into the main sorted snapshot. When the buffer reaches <see cref="AppendBufferSize"/> entries,
/// a normalization pass merges the buffer into the sorted snapshot and publishes the new snapshot
/// atomically via <c>Volatile.Write</c> (RCU semantics, Invariant VPC.B.5).
/// </para>
/// <para><strong>Tuning <see cref="AppendBufferSize"/>:</strong></para>
/// <list type="bullet">
/// <item><description>
/// <strong>Smaller value</strong> — normalizes more frequently; the snapshot stays more
/// up-to-date between normalizations, but normalization CPU cost is paid more often per segment added.
/// </description></item>
/// <item><description>
/// <strong>Larger value</strong> — normalizes less frequently; lower amortized CPU cost,
/// but the snapshot may lag behind recently added segments for longer between flushes.
/// </description></item>
/// <item><description>
/// <strong>Default (8)</strong> — appropriate for most workloads. Only tune under profiling.
/// </description></item>
/// </list>
/// <para>See <c>docs/visited-places/storage-strategies.md</c> for a full strategy comparison.</para>
/// </remarks>
public sealed class SnapshotAppendBufferStorageOptions<TRange, TData>
    : StorageStrategyOptions<TRange, TData>
    where TRange : IComparable<TRange>
{
    /// <summary>
    /// A default instance using <see cref="AppendBufferSize"/> = 8.
    /// </summary>
    public static readonly SnapshotAppendBufferStorageOptions<TRange, TData> Default = new();

    /// <summary>
    /// Number of segments the append buffer can hold before a normalization pass is triggered.
    /// Controls both the pre-allocated buffer array size and the flush threshold.
    /// Must be &gt;= 1. Default: 8.
    /// </summary>
    public int AppendBufferSize { get; }

    /// <summary>
    /// Initializes a new <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}"/>
    /// with the specified append buffer size.
    /// </summary>
    /// <param name="appendBufferSize">
    /// Number of segments the append buffer holds before normalization is triggered.
    /// Must be &gt;= 1. Default: 8.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="appendBufferSize"/> is less than 1.
    /// </exception>
    public SnapshotAppendBufferStorageOptions(int appendBufferSize = 8)
    {
        if (appendBufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appendBufferSize),
                "AppendBufferSize must be greater than or equal to 1.");
        }

        AppendBufferSize = appendBufferSize;
    }

    /// <inheritdoc/>
    internal override ISegmentStorage<TRange, TData> Create() =>
        new SnapshotAppendBufferStorage<TRange, TData>(AppendBufferSize);

    /// <inheritdoc/>
    public bool Equals(SnapshotAppendBufferStorageOptions<TRange, TData>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return AppendBufferSize == other.AppendBufferSize;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is SnapshotAppendBufferStorageOptions<TRange, TData> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => AppendBufferSize.GetHashCode();

    /// <summary>Returns <c>true</c> if the two instances are equal.</summary>
    public static bool operator ==(
        SnapshotAppendBufferStorageOptions<TRange, TData>? left,
        SnapshotAppendBufferStorageOptions<TRange, TData>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Returns <c>true</c> if the two instances are not equal.</summary>
    public static bool operator !=(
        SnapshotAppendBufferStorageOptions<TRange, TData>? left,
        SnapshotAppendBufferStorageOptions<TRange, TData>? right) =>
        !(left == right);
}
