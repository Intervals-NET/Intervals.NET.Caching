using Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

namespace Intervals.NET.Caching.VisitedPlaces.Unit.Tests.Public.Configuration;

/// <summary>
/// Unit tests for <see cref="SnapshotAppendBufferStorageOptions{TRange,TData}"/> and
/// <see cref="LinkedListStrideIndexStorageOptions{TRange,TData}"/>.
/// Validates construction, validation, defaults, equality, and the Default singletons.
/// </summary>
public sealed class StorageStrategyOptionsTests
{
    #region SnapshotAppendBufferStorageOptions — Construction Tests

    [Fact]
    public void SnapshotAppendBuffer_DefaultConstructor_UsesBufferSizeEight()
    {
        // ACT
        var options = new SnapshotAppendBufferStorageOptions<int, int>();

        // ASSERT
        Assert.Equal(8, options.AppendBufferSize);
    }

    [Fact]
    public void SnapshotAppendBuffer_WithExplicitBufferSize_StoresValue()
    {
        // ACT
        var options = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 32);

        // ASSERT
        Assert.Equal(32, options.AppendBufferSize);
    }

    [Fact]
    public void SnapshotAppendBuffer_WithBufferSizeOne_IsValid()
    {
        // ACT
        var options = new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 1);

        // ASSERT
        Assert.Equal(1, options.AppendBufferSize);
    }

    [Fact]
    public void SnapshotAppendBuffer_WithBufferSizeZero_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: 0));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("appendBufferSize", argEx.ParamName);
    }

    [Fact]
    public void SnapshotAppendBuffer_WithNegativeBufferSize_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new SnapshotAppendBufferStorageOptions<int, int>(appendBufferSize: -1));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    #endregion

    #region SnapshotAppendBufferStorageOptions — Default Singleton Tests

    [Fact]
    public void SnapshotAppendBuffer_Default_HasBufferSizeEight()
    {
        // ACT & ASSERT
        Assert.Equal(8, SnapshotAppendBufferStorageOptions<int, int>.Default.AppendBufferSize);
    }

    [Fact]
    public void SnapshotAppendBuffer_Default_IsSameReference()
    {
        // ACT & ASSERT — same instance both times
        Assert.Same(
            SnapshotAppendBufferStorageOptions<int, int>.Default,
            SnapshotAppendBufferStorageOptions<int, int>.Default);
    }

    #endregion

    #region SnapshotAppendBufferStorageOptions — Equality Tests

    [Fact]
    public void SnapshotAppendBuffer_EqualBufferSizes_AreEqual()
    {
        // ARRANGE
        var a = new SnapshotAppendBufferStorageOptions<int, int>(16);
        var b = new SnapshotAppendBufferStorageOptions<int, int>(16);

        // ACT & ASSERT
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void SnapshotAppendBuffer_DifferentBufferSizes_AreNotEqual()
    {
        // ARRANGE
        var a = new SnapshotAppendBufferStorageOptions<int, int>(8);
        var b = new SnapshotAppendBufferStorageOptions<int, int>(16);

        // ACT & ASSERT
        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void SnapshotAppendBuffer_EqualInstances_HaveSameHashCode()
    {
        // ARRANGE
        var a = new SnapshotAppendBufferStorageOptions<int, int>(4);
        var b = new SnapshotAppendBufferStorageOptions<int, int>(4);

        // ACT & ASSERT
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void SnapshotAppendBuffer_SameReference_IsEqualToSelf()
    {
        // ARRANGE
        var a = new SnapshotAppendBufferStorageOptions<int, int>(8);

        // ACT & ASSERT
        Assert.True(a.Equals(a));
    }

    [Fact]
    public void SnapshotAppendBuffer_NullComparison_IsNotEqual()
    {
        // ARRANGE
        var a = new SnapshotAppendBufferStorageOptions<int, int>(8);

        // ACT & ASSERT
        Assert.False(a.Equals(null));
        Assert.False(a == null);
        Assert.True(a != null);
    }

    #endregion

    #region LinkedListStrideIndexStorageOptions — Construction Tests

    [Fact]
    public void LinkedListStrideIndex_DefaultConstructor_UsesDefaultValues()
    {
        // ACT
        var options = new LinkedListStrideIndexStorageOptions<int, int>();

        // ASSERT
        Assert.Equal(8, options.AppendBufferSize);
        Assert.Equal(16, options.Stride);
    }

    [Fact]
    public void LinkedListStrideIndex_WithExplicitValues_StoresValues()
    {
        // ACT
        var options = new LinkedListStrideIndexStorageOptions<int, int>(appendBufferSize: 4, stride: 32);

        // ASSERT
        Assert.Equal(4, options.AppendBufferSize);
        Assert.Equal(32, options.Stride);
    }

    [Fact]
    public void LinkedListStrideIndex_WithMinimumValues_IsValid()
    {
        // ACT
        var options = new LinkedListStrideIndexStorageOptions<int, int>(appendBufferSize: 1, stride: 1);

        // ASSERT
        Assert.Equal(1, options.AppendBufferSize);
        Assert.Equal(1, options.Stride);
    }

    [Fact]
    public void LinkedListStrideIndex_WithZeroAppendBufferSize_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new LinkedListStrideIndexStorageOptions<int, int>(appendBufferSize: 0, stride: 16));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("appendBufferSize", argEx.ParamName);
    }

    [Fact]
    public void LinkedListStrideIndex_WithZeroStride_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new LinkedListStrideIndexStorageOptions<int, int>(appendBufferSize: 8, stride: 0));

        // ASSERT
        Assert.NotNull(exception);
        var argEx = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("stride", argEx.ParamName);
    }

    [Fact]
    public void LinkedListStrideIndex_WithNegativeAppendBufferSize_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new LinkedListStrideIndexStorageOptions<int, int>(appendBufferSize: -1, stride: 16));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void LinkedListStrideIndex_WithNegativeStride_ThrowsArgumentOutOfRangeException()
    {
        // ACT
        var exception = Record.Exception(
            () => new LinkedListStrideIndexStorageOptions<int, int>(appendBufferSize: 8, stride: -1));

        // ASSERT
        Assert.NotNull(exception);
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    #endregion

    #region LinkedListStrideIndexStorageOptions — Default Singleton Tests

    [Fact]
    public void LinkedListStrideIndex_Default_HasExpectedDefaults()
    {
        // ACT & ASSERT
        Assert.Equal(8, LinkedListStrideIndexStorageOptions<int, int>.Default.AppendBufferSize);
        Assert.Equal(16, LinkedListStrideIndexStorageOptions<int, int>.Default.Stride);
    }

    [Fact]
    public void LinkedListStrideIndex_Default_IsSameReference()
    {
        // ACT & ASSERT
        Assert.Same(
            LinkedListStrideIndexStorageOptions<int, int>.Default,
            LinkedListStrideIndexStorageOptions<int, int>.Default);
    }

    #endregion

    #region LinkedListStrideIndexStorageOptions — Equality Tests

    [Fact]
    public void LinkedListStrideIndex_EqualOptions_AreEqual()
    {
        // ARRANGE
        var a = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);
        var b = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);

        // ACT & ASSERT
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void LinkedListStrideIndex_DifferentAppendBufferSize_AreNotEqual()
    {
        // ARRANGE
        var a = new LinkedListStrideIndexStorageOptions<int, int>(4, 16);
        var b = new LinkedListStrideIndexStorageOptions<int, int>(8, 16);

        // ACT & ASSERT
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void LinkedListStrideIndex_DifferentStride_AreNotEqual()
    {
        // ARRANGE
        var a = new LinkedListStrideIndexStorageOptions<int, int>(8, 8);
        var b = new LinkedListStrideIndexStorageOptions<int, int>(8, 16);

        // ACT & ASSERT
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void LinkedListStrideIndex_EqualInstances_HaveSameHashCode()
    {
        // ARRANGE
        var a = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);
        var b = new LinkedListStrideIndexStorageOptions<int, int>(4, 8);

        // ACT & ASSERT
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void LinkedListStrideIndex_SameReference_IsEqualToSelf()
    {
        // ARRANGE
        var a = new LinkedListStrideIndexStorageOptions<int, int>(8, 16);

        // ACT & ASSERT
        Assert.True(a.Equals(a));
    }

    [Fact]
    public void LinkedListStrideIndex_NullComparison_IsNotEqual()
    {
        // ARRANGE
        var a = new LinkedListStrideIndexStorageOptions<int, int>(8, 16);

        // ACT & ASSERT
        Assert.False(a.Equals(null));
        Assert.False(a == null);
        Assert.True(a != null);
    }

    #endregion
}
