namespace Intervals.NET.Caching.VisitedPlaces.Public.Configuration;

/// <summary>
/// Immutable configuration options for the sampling-based eviction selector strategy.
/// Controls how many segments are randomly examined per eviction candidate selection.
/// </summary>
public sealed class EvictionSamplingOptions
{
    /// <summary>
    /// The default sample size used when no custom options are provided.
    /// </summary>
    public const int DefaultSampleSize = 32;

    /// <summary>
    /// The number of segments randomly examined during each eviction candidate selection.
    /// The worst candidate among the sampled segments is returned for eviction.
    /// Must be &gt;= 1.
    /// </summary>
    public int SampleSize { get; }

    /// <summary>
    /// The default <see cref="EvictionSamplingOptions"/> instance using
    /// <see cref="DefaultSampleSize"/> (32).
    /// </summary>
    public static EvictionSamplingOptions Default { get; } = new();

    /// <summary>
    /// Initializes a new <see cref="EvictionSamplingOptions"/>.
    /// </summary>
    /// <param name="sampleSize">
    /// The number of segments to randomly sample per eviction candidate selection.
    /// Defaults to <see cref="DefaultSampleSize"/> (32). Must be &gt;= 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sampleSize"/> is less than 1.
    /// </exception>
    public EvictionSamplingOptions(int sampleSize = DefaultSampleSize)
    {
        if (sampleSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleSize),
                "SampleSize must be greater than or equal to 1.");
        }

        SampleSize = sampleSize;
    }
}
