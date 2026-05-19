using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface INotificationService
{
    ValueTask<Guid> CreateAsync(Notification draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<Notification> mutate, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Notification>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);
}
