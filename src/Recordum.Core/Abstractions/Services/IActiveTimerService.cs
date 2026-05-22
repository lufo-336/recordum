using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IActiveTimerService
{
    ValueTask<ActiveTimer> StartAsync(Guid habitNodeId, TimerMode mode, CancellationToken ct = default);
    ValueTask PauseAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask ResumeAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask<Completion> StopAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask NextSegmentAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask<ActiveTimer?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ActiveTimer>> ListActiveAsync(CancellationToken ct = default);
}
