using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeHabitNodeService : IHabitNodeService
{
    private readonly List<(Guid Id, PinScope Scope)> _pinScopeCalls = new();
    public IReadOnlyList<(Guid Id, PinScope Scope)> PinScopeCalls => _pinScopeCalls;

    private readonly List<(Guid Id, bool Archived)> _archivedCalls = new();
    public IReadOnlyList<(Guid Id, bool Archived)> ArchivedCalls => _archivedCalls;

    private readonly List<Guid> _softDeleteCalls = new();
    public IReadOnlyList<Guid> SoftDeleteCalls => _softDeleteCalls;

    private readonly List<Guid> _restoreCalls = new();
    public IReadOnlyList<Guid> RestoreCalls => _restoreCalls;

    private readonly List<(Guid Id, Guid? NewParent)> _moveCalls = new();
    public IReadOnlyList<(Guid Id, Guid? NewParent)> MoveCalls => _moveCalls;

    private readonly List<(Guid Id, Guid? NewParent, Guid? Anchor, bool After)> _reorderCalls = new();
    public IReadOnlyList<(Guid Id, Guid? NewParent, Guid? Anchor, bool After)> ReorderCalls => _reorderCalls;

    public Dictionary<Guid, HabitNode> Store { get; } = new();
    public Exception? NextException { get; set; }

    // Test hooks: let a test mirror the backend side effect (e.g. remove the
    // soft-deleted/moved row from the tree-query fake) so a reload reflects it.
    public Action<Guid>? OnSoftDelete { get; set; }
    public Action<Guid, bool>? OnSetArchived { get; set; }
    public Action<HabitNode>? OnCreate { get; set; }

    public ValueTask<Guid> CreateAsync(HabitNode draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        OnCreate?.Invoke(draft);
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
        _moveCalls.Add((id, newParentId));
        Store[id].ParentId = newParentId;
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveAndReorderAsync(
        Guid id, Guid? newParentId, Guid? anchorSiblingId, bool placeAfter, CancellationToken ct = default)
    {
        Throw();
        _reorderCalls.Add((id, newParentId, anchorSiblingId, placeAfter));
        if (Store.TryGetValue(id, out var n)) n.ParentId = newParentId;
        return ValueTask.CompletedTask;
    }

    public ValueTask SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        _softDeleteCalls.Add(id);
        OnSoftDelete?.Invoke(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask RestoreAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        _restoreCalls.Add(id);
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
        _pinScopeCalls.Add((id, scope));
        if (Store.TryGetValue(id, out var n)) n.PinScope = scope;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default)
    {
        Throw();
        _archivedCalls.Add((id, archived));
        if (Store.TryGetValue(id, out var n)) n.IsArchived = archived;
        OnSetArchived?.Invoke(id, archived);
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
