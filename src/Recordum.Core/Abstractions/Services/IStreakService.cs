namespace Recordum.Core.Abstractions.Services;

public interface IStreakService
{
    ValueTask MarkBrokenAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask ClearBrokenAsync(Guid habitNodeId, CancellationToken ct = default);
}
