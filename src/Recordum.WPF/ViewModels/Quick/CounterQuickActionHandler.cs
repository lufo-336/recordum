using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.Quick;

public sealed class CounterQuickActionHandler(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IClock clock) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Counter;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var list = await metrics.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(false);
        if (list.Count > 0) return false; // con campi il +1 cieco non ha senso: apri la schermata

        // Bare counter: one tap = one occurrence (no CompletionValue). Spec multivalue §4.1.
        await completions.RecordAsync(new Completion
        {
            HabitNodeId = habitNodeId,
            CompletedAt = clock.UtcNow.UtcDateTime,
            CompletedAtMs = clock.UtcNowMs,
        }, ct: ct).ConfigureAwait(false);
        return true;
    }
}
