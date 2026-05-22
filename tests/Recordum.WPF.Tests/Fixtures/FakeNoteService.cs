using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeNoteService : INoteService
{
    public Dictionary<Guid, Note> Store { get; } = new();
    public List<Guid> DeleteCalls { get; } = [];
    public List<(Guid Id, Action<Note> Mutator)> UpdateCalls { get; } = [];
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(Note draft, CancellationToken ct = default)
    {
        Throw();
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new ValueTask<Guid>(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<Note> mutate, CancellationToken ct = default)
    {
        Throw();
        ArgumentNullException.ThrowIfNull(mutate);
        UpdateCalls.Add((id, mutate));
        if (Store.TryGetValue(id, out var n)) mutate(n);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        DeleteCalls.Add(id);
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Note?> GetAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        return new ValueTask<Note?>(Store.TryGetValue(id, out var n) ? n : null);
    }

    public ValueTask<IReadOnlyList<Note>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<Note> list = Store.Values
            .Where(n => n.HabitNodeId == habitNodeId)
            .OrderByDescending(n => n.UpdatedAtMs)
            .ToList();
        return new ValueTask<IReadOnlyList<Note>>(list);
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
