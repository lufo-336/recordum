using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeMetricDefinitionService : IMetricDefinitionService
{
    public Dictionary<Guid, MetricDefinition> Store { get; } = new();
    public Exception? NextException { get; set; }
    public int UpdateCallCount { get; private set; }

    public ValueTask<Guid> CreateAsync(MetricDefinition draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<MetricDefinition> mutate, CancellationToken ct = default)
    {
        Throw();
        UpdateCallCount++;
        if (Store.TryGetValue(id, out var m)) mutate(m);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<MetricDefinition>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        return new(Store.Values.Where(m => m.HabitNodeId == habitNodeId).ToList());
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
