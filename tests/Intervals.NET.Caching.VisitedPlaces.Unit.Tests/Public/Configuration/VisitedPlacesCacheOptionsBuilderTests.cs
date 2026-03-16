using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Public.Configuration;

/// <summary>
/// Unit tests for <see cref="VisitedPlacesCacheOptionsBuilder{TRange,TData}"/>.
/// Validates fluent method behavior, null-guard enforcement, validation, and Build() output.
/// </summary>
public sealed class VisitedPlacesCacheOptionsBuilderTests
{
    #region Test Infrastructure

    private static VisitedPlacesCacheOptionsBuilder<int, int> CreateBuilder() => new();

    #endregion

    #region WithStorageStrategy Tests

    [Fact]
    public void WithStorageStrategy_WithValidStrategy_ReturnsSameBuilderInstance()
    {
        // ARRANGE
        var builder = CreateBuilder();
        var strategy = new SnapshotAppendBufferStorageOptions<int, int>(4);

        // ACT
        var returned = builder.WithStorageStrategy(strategy);

        // ASSERT
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithStorageStrategy_WithNullStrategy_ThrowsArgumentNullException()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var exception = Record.Exception(
            () => builder.WithStorageStrategy(null!));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
    }

    [Fact]
    public void Build_WithStorageStrategy_UsesProvidedStrategy()
    {
        // ARRANGE
        var strategy = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);

        // ACT
        var options = CreateBuilder()
            .WithStorageStrategy(strategy)
            .Build();

        // ASSERT
        Assert.Same(strategy, options.StorageStrategy);
    }

    [Fact]
    public void Build_WithoutStorageStrategy_UsesDefaultSnapshotAppendBuffer()
    {
        // ACT
        var options = CreateBuilder().Build();

        // ASSERT
        var strategy = Assert.IsType<SnapshotAppendBufferStorageOptions<int, int>>(options.StorageStrategy);
        Assert.Equal(8, strategy.AppendBufferSize);
    }

    #endregion

    #region WithEventChannelCapacity Tests

    [Fact]
    public void WithEventChannelCapacity_WithValidValue_ReturnsSameBuilderInstance()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var returned = builder.WithEventChannelCapacity(64);

        // ASSERT
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithEventChannelCapacity_WithValueOne_IsValid()
    {
        // ACT
        var options = CreateBuilder().WithEventChannelCapacity(1).Build();

        // ASSERT
        Assert.Equal(1, options.EventChannelCapacity);
    }

    [Fact]
    public void WithEventChannelCapacity_WithZero_ThrowsArgumentOutOfRangeException()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var exception = Record.Exception(() => builder.WithEventChannelCapacity(0));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("capacity", argEx.ParamName);
    }

    [Fact]
    public void WithEventChannelCapacity_WithNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var exception = Record.Exception(() => builder.WithEventChannelCapacity(-10));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Build_WithoutEventChannelCapacity_CapacityIsNull()
    {
        // ACT
        var options = CreateBuilder().Build();

        // ASSERT
        Assert.Null(options.EventChannelCapacity);
    }

    #endregion

    #region WithSegmentTtl Tests

    [Fact]
    public void WithSegmentTtl_WithValidValue_ReturnsSameBuilderInstance()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var returned = builder.WithSegmentTtl(TimeSpan.FromSeconds(30));

        // ASSERT
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithSegmentTtl_WithZero_ThrowsArgumentOutOfRangeException()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var exception = Record.Exception(() => builder.WithSegmentTtl(TimeSpan.Zero));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("ttl", argEx.ParamName);
    }

    [Fact]
    public void WithSegmentTtl_WithNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // ARRANGE
        var builder = CreateBuilder();

        // ACT
        var exception = Record.Exception(() => builder.WithSegmentTtl(TimeSpan.FromMilliseconds(-1)));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Build_WithoutSegmentTtl_TtlIsNull()
    {
        // ACT
        var options = CreateBuilder().Build();

        // ASSERT
        Assert.Null(options.SegmentTtl);
    }

    [Fact]
    public void Build_WithSegmentTtl_UsesProvidedTtl()
    {
        // ARRANGE
        var ttl = TimeSpan.FromMinutes(10);

        // ACT
        var options = CreateBuilder().WithSegmentTtl(ttl).Build();

        // ASSERT
        Assert.Equal(ttl, options.SegmentTtl);
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void Build_WithAllOptionsChained_ProducesCorrectOptions()
    {
        // ARRANGE
        var strategy = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);
        var ttl = TimeSpan.FromSeconds(60);

        // ACT
        var options = CreateBuilder()
            .WithStorageStrategy(strategy)
            .WithEventChannelCapacity(128)
            .WithSegmentTtl(ttl)
            .Build();

        // ASSERT
        Assert.Same(strategy, options.StorageStrategy);
        Assert.Equal(128, options.EventChannelCapacity);
        Assert.Equal(ttl, options.SegmentTtl);
    }

    [Fact]
    public void Build_CanBeCalledRepeatedly_ProducesFreshInstanceEachTime()
    {
        // ARRANGE
        var builder = CreateBuilder().WithEventChannelCapacity(32);

        // ACT
        var options1 = builder.Build();
        var options2 = builder.Build();

        // ASSERT — two independent equal instances
        Assert.NotSame(options1, options2);
        Assert.Equal(options1, options2);
    }

    #endregion
}
