using Intervals.NET.Domain.Default.Numeric;
using SlidingWindowCache.Infrastructure.Storage;
using SlidingWindowCache.Unit.Tests.Infrastructure.Extensions;
using SlidingWindowCache.Unit.Tests.Infrastructure.Storage.TestInfrastructure;

namespace SlidingWindowCache.Unit.Tests.Infrastructure.Storage;

/// <summary>
/// Unit tests for SnapshotReadStorage that verify the ICacheStorage interface contract,
/// data correctness (Invariant B.11), and error handling.
/// Shared tests are inherited from <see cref="CacheStorageTestsBase"/>.
/// </summary>
public class SnapshotReadStorageTests : CacheStorageTestsBase
{
    protected override object CreateStorageObject(IntegerFixedStepDomain domain) =>
        new SnapshotReadStorage<int, int, IntegerFixedStepDomain>(domain);

    protected override object CreateVariableStepStorageObject(IntegerVariableStepDomain domain) =>
        new SnapshotReadStorage<int, int, IntegerVariableStepDomain>(domain);
}
