namespace Recordum.Core.Abstractions.Services;

public interface IPurgeService
{
    ValueTask PurgeSubtreeAsync(Guid id, CancellationToken ct = default);
}
