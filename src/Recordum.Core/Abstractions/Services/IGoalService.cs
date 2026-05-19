using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IGoalService
{
    ValueTask<Guid> CreateAsync(Goal draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<Goal> mutate, CancellationToken ct = default);
    ValueTask CloseAsync(Guid id, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Goal>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);
}
