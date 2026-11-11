using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Quick;

public sealed class ActionQuickActionHandler(
    ICompletionService completions,
    IClock clock,
    IResetTimeResolver resetTimes) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Action;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
    {
        var boundary = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
        var stamp = CompletionStamp.For(boundary, clock, day ?? boundary.Today(clock));
        var draft = new Completion
        {
            HabitNodeId = habitNodeId,
            CompletedAt = stamp.Timestamp,
            CompletedAtMs = stamp.Ms,
            IsRetroactive = stamp.IsRetroactive,
        };
        await completions.RecordAsync(draft, ct: ct).ConfigureAwait(false);
        return true;
    }
}
