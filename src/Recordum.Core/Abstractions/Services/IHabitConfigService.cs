using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitConfigService
{
    ValueTask<HabitConfig?> GetAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask UpsertAsync(Guid habitNodeId, Action<HabitConfig> mutate, CancellationToken ct = default);
    ValueTask AddSegmentAsync(Guid habitNodeId, SegmentDefinition segment, CancellationToken ct = default);
    ValueTask RemoveSegmentAsync(Guid segmentId, CancellationToken ct = default);
}
