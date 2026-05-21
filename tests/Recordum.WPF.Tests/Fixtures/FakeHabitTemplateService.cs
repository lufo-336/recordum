using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeHabitTemplateService : IHabitTemplateService
{
    public Dictionary<Guid, HabitTemplate> Store { get; } = new();
    public List<(Guid TemplateId, Guid? ParentId, Guid NewNodeId)> InstantiateCalls { get; } = new();
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default)
    {
        Throw();
        if (Store.TryGetValue(id, out var t)) mutate(t);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        return new(Store.TryGetValue(id, out var t) ? t : null);
    }

    public ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default)
    {
        Throw();
        return new(Store.Values.OrderBy(t => t.Name).ToList());
    }

    public ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default)
    {
        Throw();
        var newId = Guid.NewGuid();
        InstantiateCalls.Add((templateId, parentId, newId));
        return new(newId);
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
