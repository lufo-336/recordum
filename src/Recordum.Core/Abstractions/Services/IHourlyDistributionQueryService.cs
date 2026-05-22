namespace Recordum.Core.Abstractions.Services;

public interface IHourlyDistributionQueryService
{
    ValueTask<IReadOnlyList<int>> GetHourlyAsync(
        Guid habitNodeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
}
