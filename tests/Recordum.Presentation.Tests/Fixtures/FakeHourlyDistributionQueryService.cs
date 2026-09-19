using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeHourlyDistributionQueryService : IHourlyDistributionQueryService
{
    public int[] Buckets { get; set; } = new int[24];

    public ValueTask<IReadOnlyList<int>> GetHourlyAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
        => new(Buckets);
}
