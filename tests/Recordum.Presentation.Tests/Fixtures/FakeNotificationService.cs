using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeNotificationService : INotificationService
{
    public Dictionary<Guid, Notification> Store { get; } = new();
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(Notification draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<Notification> mutate, CancellationToken ct = default)
    {
        Throw();
        if (Store.TryGetValue(id, out var n)) mutate(n);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<Notification>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        return new(Store.Values.Where(n => n.HabitNodeId == habitNodeId).ToList());
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
