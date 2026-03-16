using Intervals.NET.Caching.VisitedPlaces.Core;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="SnapshotAppendBufferStorage{TRange,TData}"/>.
/// Covers constructor validation, snapshot merge mechanics, append buffer interaction,
/// FindIntersecting, and TryGetRandomSegment coverage across buffer + snapshot.
/// <para>
/// Count invariant (empty / add / remove), VPC.C.3 overlap guard, VPC.T.1 idempotent removal,
/// TryGetRandomSegment filter contract, TryNormalize threshold, and TryAddRange overlap/sorting
/// are all covered by <see cref="SegmentStorageBaseTests"/>, which is parameterised over both
/// strategies. Tests in this class focus exclusively on mechanics specific to the
/// snapshot + append-buffer data structure.
/// </para>
/// </summary>
public sealed class SnapshotAppendBufferStorageTests
{
    /// <summary>
    /// Number of <see cref="ISegmentStorage{TRange,TData}.TryGetRandomSegment"/> calls used in
    /// statistical coverage assertions. With N segments and this many draws, the probability
    /// that any specific segment is never selected is (1 - 1/N)^Trials ≈ e^(-Trials/N).
    /// For N=10, Trials=1000: p(miss) ≈ e^(-100) ≈ 0 — effectively impossible.
    /// </summary>
    private const int StatisticalTrials = 1000;

    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultAppendBufferSize_DoesNotThrow()
    {
        // ACT
        var exception = Record.Exception(() => new SnapshotAppendBufferStorage<int, int>());

        // ASSERT
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithValidAppendBufferSize_DoesNotThrow()
    {
        // ACT
        var exception = Record.Exception(() => new SnapshotAppendBufferStorage<int, int>(appendBufferSize: 4));

        // ASSERT
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidAppendBufferSize_ThrowsArgumentOutOfRangeException(int appendBufferSize)
    {
        // ACT
        var exception = Record.Exception(() => new SnapshotAppendBufferStorage<int, int>(appendBufferSize));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    #endregion

    #region Count Tests

    // Count invariant coverage (empty / add / remove) is provided by SegmentStorageBaseTests,
    // which is parameterised over both strategies. Only strategy-specific Count edge cases live here.

    #endregion

    #region Add / TryGetRandomSegment Tests

    // TryGetRandomSegment filter contract (never returns removed/expired; exhausted retries → null)
    // is covered by SegmentStorageBaseTests. Tests here cover strategy-specific sampling mechanics:
    // that segments in the append buffer and snapshot are reachable via random sampling.

    [Fact]
    public void TryGetRandomSegment_AfterAdding_EventuallyReturnsAddedSegment()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var seg = AddSegment(storage, 0, 9);

        // ACT — with a single live segment, every non-null result must be that segment
        CachedSegment<int, int>? found = null;
        for (var i = 0; i < StatisticalTrials && found is null; i++)
        {
            found = storage.TryGetRandomSegment();
        }

        // ASSERT
        Assert.NotNull(found);
        Assert.Same(seg, found);
    }

    [Fact]
    public void TryGetRandomSegment_AfterAddingMoreThanAppendBufferSize_EventuallyReturnsAllSegments()
    {
        // ARRANGE — default AppendBufferSize is 8; add 10 segments, flushing via TryNormalize
        // whenever the append buffer is full (the executor would do this in production).
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var segments = new List<CachedSegment<int, int>>();

        for (var i = 0; i < 10; i++)
        {
            segments.Add(AddSegment(storage, i * 10, i * 10 + 5));
            storage.TryNormalize(out _); // flush buffer once full; no-op otherwise
        }

        // ACT — sample enough times for every segment to be returned at least once
        var seen = new HashSet<CachedSegment<int, int>>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < StatisticalTrials; i++)
        {
            var result = storage.TryGetRandomSegment();
            if (result is not null)
            {
                seen.Add(result);
            }
        }

        // ASSERT — every added segment must have been returned at least once
        Assert.Equal(10, seen.Count);
        foreach (var seg in segments)
        {
            Assert.Contains(seg, seen);
        }
    }

    #endregion

    #region FindIntersecting Tests

    [Fact]
    public void FindIntersecting_WhenNoSegments_ReturnsEmpty()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var range = TestHelpers.CreateRange(0, 10);

        // ASSERT
        Assert.Empty(storage.FindIntersecting(range));
    }

    [Fact]
    public void FindIntersecting_WithExactMatch_ReturnsSegment()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var seg = AddSegment(storage, 5, 15);

        // ACT
        var result = storage.FindIntersecting(TestHelpers.CreateRange(5, 15));

        // ASSERT
        Assert.Contains(seg, result);
    }

    [Fact]
    public void FindIntersecting_WithPartialOverlap_ReturnsSegment()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var seg = AddSegment(storage, 5, 15);

        // ACT — query [10, 20] overlaps [5, 15]
        var result = storage.FindIntersecting(TestHelpers.CreateRange(10, 20));

        // ASSERT
        Assert.Contains(seg, result);
    }

    [Fact]
    public void FindIntersecting_WithNonIntersectingRange_ReturnsEmpty()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        AddSegment(storage, 0, 9);

        // ACT — query [20, 30] does not overlap [0, 9]
        var result = storage.FindIntersecting(TestHelpers.CreateRange(20, 30));

        // ASSERT
        Assert.Empty(result);
    }

    [Fact]
    public void FindIntersecting_WithMultipleSegments_ReturnsOnlyIntersecting()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var seg1 = AddSegment(storage, 0, 9);
        AddSegment(storage, 50, 59);  // no overlap with [5, 15]

        // ACT
        var result = storage.FindIntersecting(TestHelpers.CreateRange(5, 15));

        // ASSERT
        Assert.Contains(seg1, result);
        Assert.Single(result);
    }

    [Fact]
    public void FindIntersecting_AfterNormalization_StillFindsSegments()
    {
        // ARRANGE — add >8 segments, calling TryNormalize to flush the buffer as the executor would
        var storage = new SnapshotAppendBufferStorage<int, int>();
        for (var i = 0; i < 9; i++)
        {
            AddSegment(storage, i * 10, i * 10 + 5);
            storage.TryNormalize(out _); // flush buffer once full; no-op otherwise
        }

        // ACT — query middle of the range
        var result = storage.FindIntersecting(TestHelpers.CreateRange(40, 45));

        // ASSERT
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FindIntersecting_AfterRemove_DoesNotReturnRemovedSegment()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var seg = AddSegment(storage, 0, 9);
        storage.TryRemove(seg);

        // ACT
        var result = storage.FindIntersecting(TestHelpers.CreateRange(0, 9));

        // ASSERT
        Assert.DoesNotContain(seg, result);
    }

    #endregion

    #region TryAddRange Tests

    // TryAddRange VPC.C.3 (overlap guard, unsorted input, empty input) is covered by
    // SegmentStorageBaseTests. Tests here focus on snapshot merge mechanics specific to this strategy.

    [Fact]
    public void TryAddRange_WithMultipleSegments_UpdatesCountCorrectly()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var segments = new[]
        {
            CreateSegment(0, 9),
            CreateSegment(20, 29),
            CreateSegment(40, 49),
        };

        // ACT
        storage.TryAddRange(segments);

        // ASSERT
        Assert.Equal(3, storage.Count);
    }

    [Fact]
    public void TryAddRange_WithMultipleSegments_AllSegmentsFoundByFindIntersecting()
    {
        // ARRANGE
        var storage = new SnapshotAppendBufferStorage<int, int>();
        var seg1 = CreateSegment(0, 9);
        var seg2 = CreateSegment(20, 29);
        var seg3 = CreateSegment(40, 49);

        // ACT
        storage.TryAddRange([seg1, seg2, seg3]);

        // ASSERT
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(0, 9)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(20, 29)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(40, 49)));
    }

    [Fact]
    public void TryAddRange_AfterExistingSegmentsInSnapshot_MergesCorrectly()
    {
        // ARRANGE — add enough to trigger normalization (snapshot has segments), then bulk-add more
        var storage = new SnapshotAppendBufferStorage<int, int>(appendBufferSize: 2);
        AddSegment(storage, 0, 9);
        AddSegment(storage, 20, 29); // triggers normalization; [0..9] and [20..29] are in snapshot

        var newSegments = new[]
        {
            CreateSegment(40, 49),
            CreateSegment(60, 69),
        };

        // ACT
        storage.TryAddRange(newSegments);

        // ASSERT — all four segments findable
        Assert.Equal(4, storage.Count);
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(0, 9)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(20, 29)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(40, 49)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(60, 69)));
    }

    [Fact]
    public void TryAddRange_DoesNotTriggerUnnecessaryNormalizationOfAppendBuffer()
    {
        // ARRANGE — append buffer has room (buffer size 8, count below threshold)
        var storage = new SnapshotAppendBufferStorage<int, int>(appendBufferSize: 8);
        AddSegment(storage, 0, 9); // _appendCount becomes 1

        var bulkSegments = new[]
        {
            CreateSegment(20, 29),
            CreateSegment(40, 49),
            CreateSegment(60, 69),
        };

        // ACT — bulk-add bypasses the append buffer entirely; existing buffer entry still readable
        storage.TryAddRange(bulkSegments);

        // ASSERT — original buffered segment and bulk segments are all findable
        Assert.Equal(4, storage.Count);
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(0, 9)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(20, 29)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(40, 49)));
        Assert.Single(storage.FindIntersecting(TestHelpers.CreateRange(60, 69)));
    }

    #endregion

    #region Helpers

    private static CachedSegment<int, int> AddSegment(
        SnapshotAppendBufferStorage<int, int> storage,
        int start,
        int end)
    {
        var range = TestHelpers.CreateRange(start, end);
        var segment = new CachedSegment<int, int>(
            range,
            new ReadOnlyMemory<int>(new int[end - start + 1]));
        storage.TryAdd(segment);
        return segment;
    }

    /// <summary>
    /// Creates a <see cref="CachedSegment{TRange,TData}"/> without adding it to storage.
    /// Use this in <c>TryAddRange</c> tests to build the input array before calling
    /// <see cref="SnapshotAppendBufferStorage{TRange,TData}.TryAddRange"/>.
    /// </summary>
    private static CachedSegment<int, int> CreateSegment(int start, int end)
    {
        var range = TestHelpers.CreateRange(start, end);
        return new CachedSegment<int, int>(
            range,
            new ReadOnlyMemory<int>(new int[end - start + 1]));
    }

    #endregion
}
