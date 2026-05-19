using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IStreakPauseService
{
    ValueTask<Guid> StartAsync(Guid habitNodeId, DateOnly from, DateOnly until, string? reason, CancellationToken ct = default);
    ValueTask EndAsync(Guid pauseId, DateOnly newUntil, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid pauseId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<StreakPause>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);
}
