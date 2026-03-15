using Intervals.NET.Domain.Abstractions;

namespace Intervals.NET.Caching.Extensions;

/// <summary>
/// Domain-agnostic extension methods that dispatch to Fixed or Variable implementations at runtime,
/// allowing the cache to work with any <see cref="IRangeDomain{TRange}"/> type.
/// O(N) cost for variable-step domains is acceptable given data source I/O is orders of magnitude slower.
/// </summary>
internal static class IntervalsNetDomainExtensions
{
    /// <summary>
    /// Calculates the number of discrete steps within a range for any domain type.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TDomain">The domain type (can be fixed or variable-step).</typeparam>
    /// <param name="range">The range to measure.</param>
    /// <param name="domain">The domain defining discrete steps.</param>
    /// <returns>The number of discrete steps, or infinity if unbounded.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the domain does not implement either IFixedStepDomain or IVariableStepDomain.
    /// </exception>
    internal static RangeValue<long> Span<TRange, TDomain>(this Range<TRange> range, TDomain domain)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange> => domain switch
        {
            // FQN required: Intervals.NET exposes Span/Expand in separate Fixed and Variable namespaces
            // (Intervals.NET.Domain.Extensions.Fixed and ...Variable). Both namespaces define a
            // RangeDomainExtensions class with the same method names, so a using directive would cause
            // an ambiguity error. Full qualification unambiguously selects the correct overload at
            // compile time without polluting the file's namespace imports.
            IFixedStepDomain<TRange> fixedDomain => Domain.Extensions.Fixed.RangeDomainExtensions.Span(range, fixedDomain),
            IVariableStepDomain<TRange> variableDomain => Domain.Extensions.Variable.RangeDomainExtensions.Span(range, variableDomain),
            _ => throw new NotSupportedException(
                $"Domain type {domain.GetType().Name} must implement either IFixedStepDomain<T> or IVariableStepDomain<T>.")
        };

    /// <summary>
    /// Expands a range by a specified number of steps on each side for any domain type.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TDomain">The domain type (can be fixed or variable-step).</typeparam>
    /// <param name="range">The range to expand.</param>
    /// <param name="domain">The domain defining discrete steps.</param>
    /// <param name="left">Number of steps to expand on the left.</param>
    /// <param name="right">Number of steps to expand on the right.</param>
    /// <returns>The expanded range.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the domain does not implement either IFixedStepDomain or IVariableStepDomain.
    /// </exception>
    internal static Range<TRange> Expand<TRange, TDomain>(
        this Range<TRange> range,
        TDomain domain,
        long left,
        long right)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange> => domain switch
        {
            IFixedStepDomain<TRange> fixedDomain => Domain.Extensions.Fixed.RangeDomainExtensions.Expand(
                range, fixedDomain, left, right),
            IVariableStepDomain<TRange> variableDomain => Domain.Extensions.Variable.RangeDomainExtensions
                .Expand(range, variableDomain, left, right),
            _ => throw new NotSupportedException(
                $"Domain type {domain.GetType().Name} must implement either IFixedStepDomain<T> or IVariableStepDomain<T>.")
        };

    /// <summary>
    /// Expands or shrinks a range by a ratio of its size for any domain type.
    /// </summary>
    /// <typeparam name="TRange">The type representing range boundaries.</typeparam>
    /// <typeparam name="TDomain">The domain type (can be fixed or variable-step).</typeparam>
    /// <param name="range">The range to modify.</param>
    /// <param name="domain">The domain defining discrete steps.</param>
    /// <param name="leftRatio">Ratio to expand/shrink the left boundary (negative shrinks).</param>
    /// <param name="rightRatio">Ratio to expand/shrink the right boundary (negative shrinks).</param>
    /// <returns>The modified range.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the domain does not implement either IFixedStepDomain or IVariableStepDomain.
    /// </exception>
    internal static Range<TRange> ExpandByRatio<TRange, TDomain>(
        this Range<TRange> range,
        TDomain domain,
        double leftRatio,
        double rightRatio)
        where TRange : IComparable<TRange>
        where TDomain : IRangeDomain<TRange> => domain switch
        {
            IFixedStepDomain<TRange> fixedDomain => Domain.Extensions.Fixed.RangeDomainExtensions
                .ExpandByRatio(range, fixedDomain, leftRatio, rightRatio),
            IVariableStepDomain<TRange> variableDomain => Domain.Extensions.Variable.RangeDomainExtensions
                .ExpandByRatio(range, variableDomain, leftRatio, rightRatio),
            _ => throw new NotSupportedException(
                $"Domain type {domain.GetType().Name} must implement either IFixedStepDomain<T> or IVariableStepDomain<T>.")
        };
}