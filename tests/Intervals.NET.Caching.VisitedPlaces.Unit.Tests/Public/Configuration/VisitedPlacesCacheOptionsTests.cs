using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Public.Configuration;

/// <summary>
/// Unit tests for <see cref="VisitedPlacesCacheOptions{TRange,TData}"/>.
/// Validates validation logic, property initialization, equality, and edge cases.
/// </summary>
public sealed class VisitedPlacesCacheOptionsTests
{
    #region Constructor — Valid Parameters Tests

    [Fact]
    public void Constructor_WithAllDefaults_InitializesWithDefaultValues()
    {
        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>();

        // ASSERT
        Assert.IsType<SnapshotAppendBufferStorageOptions<int, int>>(options.StorageStrategy);
        Assert.Null(options.EventChannelCapacity);
        Assert.Null(options.SegmentTtl);
    }

    [Fact]
    public void Constructor_WithExplicitValues_InitializesAllProperties()
    {
        // ARRANGE
        var strategy = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);
        var ttl = TimeSpan.FromMinutes(5);

        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: strategy,
            eventChannelCapacity: 64,
            segmentTtl: ttl);

        // ASSERT
        Assert.Same(strategy, options.StorageStrategy);
        Assert.Equal(64, options.EventChannelCapacity);
        Assert.Equal(ttl, options.SegmentTtl);
    }

    [Fact]
    public void Constructor_WithNullStorageStrategy_UsesDefaultSnapshotAppendBuffer()
    {
        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>(storageStrategy: null);

        // ASSERT
        var strategy = Assert.IsType<SnapshotAppendBufferStorageOptions<int, int>>(options.StorageStrategy);
        Assert.Equal(8, strategy.AppendBufferSize); // Default buffer size
    }

    [Fact]
    public void Constructor_WithEventChannelCapacityOne_IsValid()
    {
        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 1);

        // ASSERT
        Assert.Equal(1, options.EventChannelCapacity);
    }

    [Fact]
    public void Constructor_WithLargeEventChannelCapacity_IsValid()
    {
        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: int.MaxValue);

        // ASSERT
        Assert.Equal(int.MaxValue, options.EventChannelCapacity);
    }

    [Fact]
    public void Constructor_WithMinimalPositiveSegmentTtl_IsValid()
    {
        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>(segmentTtl: TimeSpan.FromTicks(1));

        // ASSERT
        Assert.Equal(TimeSpan.FromTicks(1), options.SegmentTtl);
    }

    #endregion

    #region Constructor — Validation Tests

    [Fact]
    public void Constructor_WithEventChannelCapacityZero_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 0));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("eventChannelCapacity", argEx.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativeEventChannelCapacity_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: -1));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("eventChannelCapacity", argEx.ParamName);
    }

    [Fact]
    public void Constructor_WithZeroSegmentTtl_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new VisitedPlacesCacheOptions<int, int>(segmentTtl: TimeSpan.Zero));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("segmentTtl", argEx.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativeSegmentTtl_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new VisitedPlacesCacheOptions<int, int>(segmentTtl: TimeSpan.FromSeconds(-1)));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("segmentTtl", argEx.ParamName);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_TwoIdenticalOptions_AreEqual()
    {
        // ARRANGE
        var options1 = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: new SnapshotAppendBufferStorageOptions<int, int>(16),
            eventChannelCapacity: 32,
            segmentTtl: TimeSpan.FromMinutes(1));

        var options2 = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: new SnapshotAppendBufferStorageOptions<int, int>(16),
            eventChannelCapacity: 32,
            segmentTtl: TimeSpan.FromMinutes(1));

        // ACT & ASSERT
        Assert.Equal(options1, options2);
        Assert.True(options1 == options2);
        Assert.False(options1 != options2);
    }

    [Fact]
    public void Equality_SameReference_IsEqual()
    {
        // ARRANGE
        var options = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 10);

        // ACT & ASSERT
        Assert.True(options.Equals(options));
    }

    [Fact]
    public void Equality_WithNull_IsNotEqual()
    {
        // ARRANGE
        var options = new VisitedPlacesCacheOptions<int, int>();

        // ACT & ASSERT
        Assert.False(options.Equals(null));
        Assert.False(options == null);
        Assert.True(options != null);
    }

    [Fact]
    public void Equality_DifferentEventChannelCapacity_AreNotEqual()
    {
        // ARRANGE
        var options1 = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 10);
        var options2 = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 20);

        // ACT & ASSERT
        Assert.NotEqual(options1, options2);
        Assert.False(options1 == options2);
        Assert.True(options1 != options2);
    }

    [Fact]
    public void Equality_DifferentSegmentTtl_AreNotEqual()
    {
        // ARRANGE
        var options1 = new VisitedPlacesCacheOptions<int, int>(segmentTtl: TimeSpan.FromSeconds(10));
        var options2 = new VisitedPlacesCacheOptions<int, int>(segmentTtl: TimeSpan.FromSeconds(20));

        // ACT & ASSERT
        Assert.NotEqual(options1, options2);
    }

    [Fact]
    public void Equality_DifferentStorageStrategy_AreNotEqual()
    {
        // ARRANGE
        var options1 = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: new SnapshotAppendBufferStorageOptions<int, int>(8));
        var options2 = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: new SnapshotAppendBufferStorageOptions<int, int>(16));

        // ACT & ASSERT
        Assert.NotEqual(options1, options2);
    }

    [Fact]
    public void Equality_NullVsNonNull_AreNotEqual()
    {
        // ARRANGE
        var options = new VisitedPlacesCacheOptions<int, int>();

        // ACT & ASSERT
        Assert.False(options == null);
        Assert.True(options != null);
    }

    [Fact]
    public void GetHashCode_EqualInstances_ReturnSameHashCode()
    {
        // ARRANGE
        var options1 = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: new SnapshotAppendBufferStorageOptions<int, int>(8),
            eventChannelCapacity: 16,
            segmentTtl: TimeSpan.FromSeconds(30));

        var options2 = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: new SnapshotAppendBufferStorageOptions<int, int>(8),
            eventChannelCapacity: 16,
            segmentTtl: TimeSpan.FromSeconds(30));

        // ACT & ASSERT
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Constructor_WithNullCapacityAndNullTtl_AllNullsAreValid()
    {
        // ACT
        var options = new VisitedPlacesCacheOptions<int, int>(
            storageStrategy: null,
            eventChannelCapacity: null,
            segmentTtl: null);

        // ASSERT
        Assert.Null(options.EventChannelCapacity);
        Assert.Null(options.SegmentTtl);
    }

    [Fact]
    public void Equals_WithObjectOverload_WorksCorrectly()
    {
        // ARRANGE
        var options1 = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 5);
        var options2 = new VisitedPlacesCacheOptions<int, int>(eventChannelCapacity: 5);

        // ACT & ASSERT
        Assert.True(options1.Equals((object)options2));
        Assert.False(options1.Equals((object)new object()));
        Assert.False(options1.Equals((object)null!));
    }

    #endregion
}
