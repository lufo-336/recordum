using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Quick;

public sealed class CounterQuickActionHandler(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IClock clock,
    IResetTimeResolver resetTimes) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Counter;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
    {
        var list = await metrics.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(false);
        if (list.Count > 0) return false; // con campi il +1 cieco non ha senso: apri la schermata

        var boundary = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
        var stamp = CompletionStamp.For(boundary, clock, day ?? boundary.Today(clock));

        // Bare counter: one tap = one occurrence (no CompletionValue). Spec multivalue §4.1.
        await completions.RecordAsync(new Completion
        {
            HabitNodeId = habitNodeId,
            CompletedAt = stamp.Timestamp,
            CompletedAtMs = stamp.Ms,
            IsRetroactive = stamp.IsRetroactive,
        }, ct: ct).ConfigureAwait(false);
        return true;
    }
}
