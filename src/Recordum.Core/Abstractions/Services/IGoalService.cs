using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IGoalService
{
    ValueTask<Guid> CreateAsync(Goal draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<Goal> mutate, CancellationToken ct = default);
    ValueTask CloseAsync(Guid id, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Goal>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recently created non-closed Goal for the habit, or null when none.
    /// </summary>
    ValueTask<Goal?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new active Goal if none exists, otherwise mutates the active one.
    /// </summary>
    ValueTask UpsertActiveAsync(Guid habitNodeId, Action<Goal> mutate, CancellationToken ct = default);
}
