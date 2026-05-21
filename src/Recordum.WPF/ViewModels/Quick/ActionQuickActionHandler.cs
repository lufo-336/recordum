using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.Quick;

public sealed class ActionQuickActionHandler(ICompletionService completions, IClock clock) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Action;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var draft = new Completion
        {
            HabitNodeId = habitNodeId,
            CompletedAt = now.UtcDateTime,
            CompletedAtMs = now.ToUnixTimeMilliseconds(),
        };
        await completions.RecordAsync(draft, ct: ct).ConfigureAwait(false);
        return true;
    }
}
