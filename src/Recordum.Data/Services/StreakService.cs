using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.Data.Services;

public sealed class StreakService(IHabitConfigService configs, IClock clock) : IStreakService
{
    public ValueTask MarkBrokenAsync(Guid habitNodeId, CancellationToken ct = default)
        => configs.UpsertAsync(habitNodeId, c => c.StreakBrokenAt = clock.UtcNow.UtcDateTime, ct);

    public ValueTask ClearBrokenAsync(Guid habitNodeId, CancellationToken ct = default)
        => configs.UpsertAsync(habitNodeId, c => c.StreakBrokenAt = null, ct);
}
