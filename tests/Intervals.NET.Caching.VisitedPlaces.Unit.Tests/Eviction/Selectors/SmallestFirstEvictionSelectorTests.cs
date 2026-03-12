using Intervals.NET.Domain.Default.Numeric;
using Intervals.NET.Caching.VisitedPlaces.Core;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction;
using Intervals.NET.Caching.VisitedPlaces.Core.Eviction.Selectors;
using Intervals.NET.Caching.VisitedPlaces.Infrastructure.Storage;
using Intervals.NET.Caching.VisitedPlaces.Tests.Infrastructure.Helpers;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Eviction.Selectors;

/// <summary>
/// Unit tests for <see cref="SmallestFirstEvictionSelector{TRange,TData,TDomain}"/>.
/// Validates that <see cref="IEvictionSelector{TRange,TData}.TrySelectCandidate"/> returns the
/// segment with the smallest span from the sample.
/// All datasets are small (≤ SampleSize = 32), so sampling is exhaustive and deterministic.
/// </summary>
public sealed class SmallestFirstEvictionSelectorTests
{
    private static readonly IReadOnlySet<CachedSegment<int, int>> NoImmune =
        new HashSet<CachedSegment<int, int>>();

    private readonly IntegerFixedStepDomain _domain = TestHelpers.CreateIntDomain();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDomain_DoesNotThrow()
    {
        // ARRANGE & ACT
        var exception = Record.Exception(() =>
            new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain));

        // ASSERT
        Assert.Null(exception);
    }

    #endregion

    #region InitializeMetadata Tests

    [Fact]
    public void InitializeMetadata_SetsSpanOnEvictionMetadata()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var segment = CreateSegmentRaw(10, 19); // span = 10

        // ACT
        selector.InitializeMetadata(segment);

        // ASSERT
        var meta = Assert.IsType<SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>.SmallestFirstMetadata>(
            segment.EvictionMetadata);
        Assert.Equal(10L, meta.Span);
    }

    [Fact]
    public void InitializeMetadata_OnSegmentWithExistingMetadata_OverwritesMetadata()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var segment = CreateSegmentRaw(0, 4); // span = 5
        selector.InitializeMetadata(segment);

        // ACT — re-initialize (e.g., segment re-stored after selector swap)
        selector.InitializeMetadata(segment);

        // ASSERT — still correct metadata, not stale
        var meta = Assert.IsType<SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>.SmallestFirstMetadata>(
            segment.EvictionMetadata);
        Assert.Equal(5L, meta.Span);
    }

    #endregion

    #region TrySelectCandidate — Returns Smallest-Span Candidate

    [Fact]
    public void TrySelectCandidate_ReturnsTrueAndSelectsSmallestSpan()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        var small = CreateSegment(selector, 0, 2);    // span 3
        var large = CreateSegment(selector, 20, 29);  // span 10

        InitializeStorage(selector, [small, large]);

        // ACT
        var result = selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — smallest span is selected
        Assert.True(result);
        Assert.Same(small, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithReversedInput_StillSelectsSmallestSpan()
    {
        // ARRANGE — storage insertion order does not matter
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        var small = CreateSegment(selector, 0, 2);    // span 3
        var large = CreateSegment(selector, 20, 29);  // span 10

        InitializeStorage(selector, [large, small]);

        // ACT
        var result = selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — regardless of insertion order, smallest is found
        Assert.True(result);
        Assert.Same(small, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithMultipleCandidates_SelectsSmallestSpan()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        var small = CreateSegment(selector, 0, 2);    // span 3
        var medium = CreateSegment(selector, 10, 15); // span 6
        var large = CreateSegment(selector, 20, 29);  // span 10

        InitializeStorage(selector, [large, small, medium]);

        // ACT
        var result = selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — smallest span wins
        Assert.True(result);
        Assert.Same(small, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithSingleCandidate_ReturnsThatCandidate()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var seg = CreateSegment(selector, 0, 5);
        InitializeStorage(selector, [seg]);

        // ACT
        var result = selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT
        Assert.True(result);
        Assert.Same(seg, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WithEmptyStorage_ReturnsFalse()
    {
        // ARRANGE — initialize with empty storage
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        InitializeStorage(selector, []);

        // ACT
        var result = selector.TrySelectCandidate(NoImmune, out _);

        // ASSERT
        Assert.False(result);
    }

    [Fact]
    public void TrySelectCandidate_WithNoMetadata_EnsureMetadataLazilyComputesSpan()
    {
        // ARRANGE — segments without InitializeMetadata called (metadata = null)
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var small = CreateSegmentRaw(0, 2);    // span 3
        var large = CreateSegmentRaw(20, 29);  // span 10

        // Storage without pre-initialized metadata — EnsureMetadata lazily computes span
        InitializeStorage(selector, [large, small]);

        // ACT — EnsureMetadata lazily computes and stores span before IsWorse comparison
        var result = selector.TrySelectCandidate(NoImmune, out var candidate);

        // ASSERT — lazily computed span still selects the smallest
        Assert.True(result);
        Assert.Same(small, candidate);
    }

    #endregion

    #region TrySelectCandidate — Immunity

    [Fact]
    public void TrySelectCandidate_WhenSmallestIsImmune_SelectsNextSmallest()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);

        var small = CreateSegment(selector, 0, 2);    // span 3 — immune
        var medium = CreateSegment(selector, 10, 15); // span 6
        var large = CreateSegment(selector, 20, 29);  // span 10

        InitializeStorage(selector, [small, medium, large]);

        var immune = new HashSet<CachedSegment<int, int>> { small };

        // ACT
        var result = selector.TrySelectCandidate(immune, out var candidate);

        // ASSERT — small is immune, so medium (next smallest) is selected
        Assert.True(result);
        Assert.Same(medium, candidate);
    }

    [Fact]
    public void TrySelectCandidate_WhenAllCandidatesAreImmune_ReturnsFalse()
    {
        // ARRANGE
        var selector = new SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain>(_domain);
        var seg = CreateSegment(selector, 0, 5);
        InitializeStorage(selector, [seg]);
        var immune = new HashSet<CachedSegment<int, int>> { seg };

        // ACT
        var result = selector.TrySelectCandidate(immune, out _);

        // ASSERT
        Assert.False(result);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a <see cref="SnapshotAppendBufferStorage{TRange,TData}"/> populated with
    /// <paramref name="segments"/> and injects it into <paramref name="selector"/> via
    /// <see cref="IStorageAwareEvictionSelector{TRange,TData}"/>.
    /// </summary>
    private static void InitializeStorage(
        IEvictionSelector<int, int> selector,
        IEnumerable<CachedSegment<int, int>> segments)
    {
        var storage = new SnapshotAppendBufferStorage<int, int>();
        foreach (var seg in segments)
        {
            storage.Add(seg);
        }

        if (selector is IStorageAwareEvictionSelector<int, int> storageAware)
        {
            storageAware.Initialize(storage);
        }
    }

    private static CachedSegment<int, int> CreateSegment(
        SmallestFirstEvictionSelector<int, int, IntegerFixedStepDomain> selector,
        int start, int end)
    {
        var segment = CreateSegmentRaw(start, end);
        selector.InitializeMetadata(segment);
        return segment;
    }

    private static CachedSegment<int, int> CreateSegmentRaw(int start, int end)
    {
        var range = TestHelpers.CreateRange(start, end);
        return new CachedSegment<int, int>(
            range,
            new ReadOnlyMemory<int>(new int[end - start + 1]));
    }

    #endregion
}
