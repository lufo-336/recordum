namespace Recordum.Core.Abstractions.Services;

public interface IStreakAggregator
{
    ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(
        Guid habitNodeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
}

public sealed record StreakRun(DateOnly Start, DateOnly End, int Length);
