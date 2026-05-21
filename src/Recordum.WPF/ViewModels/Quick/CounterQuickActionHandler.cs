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
        var primary = list.OrderBy(m => m.SortOrder).FirstOrDefault();
        if (primary is null) return false;

        var now = clock.UtcNow;
        var draft = new Completion
        {
            HabitNodeId = habitNodeId,
            CompletedAt = now.UtcDateTime,
            CompletedAtMs = now.ToUnixTimeMilliseconds(),
        };
        var values = new[]
        {
            new CompletionValue
            {
                MetricDefinitionId = primary.Id,
                SeriesIndex = 0,
                ValueInteger = 1,
            },
        };
        await completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(false);
        return true;
    }
}
