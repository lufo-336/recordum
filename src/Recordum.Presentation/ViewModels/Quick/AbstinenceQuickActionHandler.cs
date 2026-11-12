using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Quick;

public sealed class AbstinenceQuickActionHandler(
    IAbstinenceService abstinence,
    IClock clock,
    IResetTimeResolver resetTimes) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Resistance;

    /// <summary>From the Home a period is only ever started (today); closing one needs the screen.</summary>
    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
    {
        if (day is { } requested)
        {
            var boundary = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
            if (requested != boundary.Today(clock)) return false;
        }

        var state = await abstinence.GetStateAsync(habitNodeId, ct).ConfigureAwait(false);
        if (state.Status == AbstinenceStatus.Abstaining) return false;

        await abstinence.StartAsync(habitNodeId, ct).ConfigureAwait(false);
        return true;
    }
}
