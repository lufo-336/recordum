using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Quick;

public sealed class AbstinenceQuickActionHandler(IAbstinenceService abstinence) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Resistance;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
    {
        var state = await abstinence.GetStateAsync(habitNodeId, ct).ConfigureAwait(false);
        if (state.Status == AbstinenceStatus.Abstaining)
            await abstinence.LapseAsync(habitNodeId, ct).ConfigureAwait(false);
        else
            await abstinence.StartAsync(habitNodeId, ct).ConfigureAwait(false);
        return true;
    }
}
