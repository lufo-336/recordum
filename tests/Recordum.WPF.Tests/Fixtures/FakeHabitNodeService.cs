using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeHabitNodeService : IHabitNodeService
{
    public List<(Guid Id, PinScope Scope)> PinScopeCalls { get; } = new();
    public List<(Guid Id, bool Archived)> ArchivedCalls { get; } = new();
    public List<Guid> SoftDeleteCalls { get; } = new();
    public List<Guid> RestoreCalls { get; } = new();
    public List<(Guid Id, Guid? NewParent)> MoveCalls { get; } = new();
    public Dictionary<Guid, HabitNode> Store { get; } = new();
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(HabitNode draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new ValueTask<Guid>(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<HabitNode> mutate, CancellationToken ct = default)
    {
        Throw();
        mutate(Store[id]);
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default)
    {
        Throw();
        MoveCalls.Add((id, newParentId));
        Store[id].ParentId = newParentId;
        return ValueTask.CompletedTask;
    }

    public ValueTask SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        SoftDeleteCalls.Add(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask RestoreAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        RestoreCalls.Add(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<HabitNode?> GetAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        return new ValueTask<HabitNode?>(Store.TryGetValue(id, out var n) ? n : null);
    }

    public ValueTask SetPinScopeAsync(Guid id, PinScope scope, CancellationToken ct = default)
    {
        Throw();
        PinScopeCalls.Add((id, scope));
        if (Store.TryGetValue(id, out var n)) n.PinScope = scope;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default)
    {
        Throw();
        ArchivedCalls.Add((id, archived));
        if (Store.TryGetValue(id, out var n)) n.IsArchived = archived;
        return ValueTask.CompletedTask;
    }

    private void Throw()
    {
        if (NextException is { } e)
        {
            NextException = null;
            throw e;
        }
    }
}
