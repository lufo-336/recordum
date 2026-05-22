using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface ICompletionService
{
    ValueTask<Guid> RecordAsync(
        Completion draft,
        IReadOnlyList<CompletionValue>? values = null,
        IReadOnlyList<CompletionSegment>? segments = null,
        CancellationToken ct = default);

    ValueTask UpdateAsync(Guid id, Action<Completion> mutate, CancellationToken ct = default);

    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);

    ValueTask<Completion?> GetAsync(Guid id, CancellationToken ct = default);

    ValueTask<IReadOnlyList<Completion>> ListByHabitAsync(
        Guid habitNodeId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int? take = null,
        CancellationToken ct = default);

    ValueTask<IReadOnlyList<HeatmapCell>> GetHeatmapAsync(
        Guid habitNodeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);

    ValueTask<IReadOnlyList<DailyValue>> GetDailySumAsync(
        Guid habitNodeId,
        Guid metricDefinitionId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
}
