using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeGoalService : IGoalService
{
    public Dictionary<Guid, Goal> Store { get; } = new();
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(Goal draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<Goal> mutate, CancellationToken ct = default)
    {
        Throw();
        if (Store.TryGetValue(id, out var g)) mutate(g);
        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        if (Store.TryGetValue(id, out var g)) { g.IsClosed = true; g.ClosedAt = DateTime.UtcNow; }
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<Goal>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        return new(Store.Values.Where(g => g.HabitNodeId == habitNodeId).ToList());
    }

    public ValueTask<Goal?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        return new(Store.Values.FirstOrDefault(g => g.HabitNodeId == habitNodeId && !g.IsClosed));
    }

    public ValueTask UpsertActiveAsync(Guid habitNodeId, Action<Goal> mutate, CancellationToken ct = default)
    {
        Throw();
        var g = Store.Values.FirstOrDefault(x => x.HabitNodeId == habitNodeId && !x.IsClosed);
        if (g is null)
        {
            g = new Goal { Id = Guid.NewGuid(), HabitNodeId = habitNodeId };
            Store[g.Id] = g;
        }
        mutate(g);
        return ValueTask.CompletedTask;
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
