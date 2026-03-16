using Intervals.NET.Caching.Extensions;
using Intervals.NET.Data;
using Intervals.NET.Data.Extensions;
using Intervals.NET.Domain.Abstractions;
using Intervals.NET.Extensions;

namespace Intervals.NET.Caching.SlidingWindow.Infrastructure.Storage;

/// <summary>
/// CopyOnRead strategy that stores data using a dual-buffer (staging buffer) pattern.
/// Uses two internal lists: one active storage for reads, one staging buffer for rematerialization.
/// </summary>
/// <typeparam name="TRange">
/// The type representing the range boundaries. Must implement <see cref="IComparable{T}"/>.
/// </typeparam>
/// <typeparam name="TData">
/// The type of data being cached.
/// </typeparam>
/// <typeparam name="TDomain">
/// The type representing the domain of the ranges. Must implement <see cref="IRangeDomain{TRange}"/>.
/// </typeparam>
internal sealed class CopyOnReadStorage<TRange, TData, TDomain> : ICacheStorage<TRange, TData, TDomain>
    where TRange : IComparable<TRange>
    where TDomain : IRangeDomain<TRange>
{
    private readonly TDomain _domain;

    // Shared lock: acquired by Read(), Rematerialize(), and ToRangeData() to prevent observation of
    // mid-swap state and to ensure each caller captures a consistent (_activeStorage, Range) pair.
    private readonly object _lock = new();

    // Active storage: serves data to Read() and ToRangeData() operations; never mutated while _lock is held
    // volatile is NOT needed: Read(), ToRangeData(), and the swap in Rematerialize() access this field
    // exclusively under _lock, which provides full acquire/release fence semantics.
    private List<TData> _activeStorage = [];

    // Staging buffer: write-only during Rematerialize(); reused across operations
    // This buffer may grow but never shrinks, amortizing allocation cost
    // volatile is NOT needed: _stagingBuffer is only accessed by the rebalance thread outside the lock,
    // and inside _lock during the swap — it never crosses thread boundaries directly.
    private List<TData> _stagingBuffer = [];

    public CopyOnReadStorage(TDomain domain)
    {
        _domain = domain;
    }

    /// <inheritdoc />
    public Range<TRange> Range { get; private set; }

    /// <inheritdoc />
    public void Rematerialize(RangeData<TRange, TData, TDomain> rangeData)
    {
        // Enumerate incoming data BEFORE acquiring the lock.
        // rangeData.Data may be a lazy LINQ chain over _activeStorage (e.g., during cache expansion).
        // Holding the lock during enumeration would block concurrent Read() calls for the full
        // enumeration duration. Instead, we materialize into a local staging buffer first, then
        // acquire the lock only for the fast swap operation.
        _stagingBuffer.Clear();                        // Preserves capacity
        _stagingBuffer.AddRange(rangeData.Data);       // Single-pass enumeration outside the lock

        lock (_lock)
        {
            // Swap buffers: staging (now filled) becomes active; old active becomes staging for next use.
            // Range update is inside the lock so Read() always observes a consistent (list, Range) pair.
            // There is no case when during Read the read buffer is changed due to lock.
            (_activeStorage, _stagingBuffer) = (_stagingBuffer, _activeStorage);
            Range = rangeData.Range;
        }
    }

    /// <inheritdoc />
    public ReadOnlyMemory<TData> Read(Range<TRange> range)
    {
        lock (_lock)
        {
            if (_activeStorage.Count == 0)
            {
                return ReadOnlyMemory<TData>.Empty;
            }

            // Validate that the requested range is within the stored range
            if (!Range.Contains(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range),
                    $"Requested range {range} is not contained within the cached range {Range}");
            }

            // Calculate the offset and length for the requested range
            var startOffset = _domain.Distance(Range.Start.Value, range.Start.Value);
            var length = (int)range.Span(_domain);

            // Validate bounds before accessing storage
            if (startOffset < 0 || length < 0 || (int)startOffset + length > _activeStorage.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(range),
                    $"Calculated offset {startOffset} and length {length} exceed storage bounds (storage count: {_activeStorage.Count})");
            }

            // Allocate a new array and copy the requested data (copy-on-read semantics)
            var result = new TData[length];
            for (var i = 0; i < length; i++)
            {
                result[i] = _activeStorage[(int)startOffset + i];
            }

            return new ReadOnlyMemory<TData>(result);
        }
    }

    /// <inheritdoc />
    public RangeData<TRange, TData, TDomain> ToRangeData()
    {
        lock (_lock)
        {
            return _activeStorage.ToArray().ToRangeData(Range, _domain);
        }
    }
}
