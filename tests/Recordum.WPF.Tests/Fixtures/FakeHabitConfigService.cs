using Recordum.Core.Abstractions.Services;
using Recordum.Core.Errors;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeHabitConfigService : IHabitConfigService
{
    public Dictionary<Guid, HabitConfig> Store { get; } = new();
    public List<SegmentDefinition> Segments { get; } = new();
    public Dictionary<Guid, List<HabitConfigHistory>> History { get; } = new();
    public Exception? NextException { get; set; }

    /// <summary>Optional gate awaited inside <see cref="GetAsync"/> — lets a test hold a load
    /// mid-flight to exercise re-entrancy (deselect-while-loading).</summary>
    public Func<Task>? GetGate { get; set; }

    public async ValueTask<HabitConfig?> GetAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        if (GetGate is { } gate) await gate().ConfigureAwait(false);
        return Store.TryGetValue(habitNodeId, out var c) ? c : null;
    }

    public ValueTask<IReadOnlyList<HabitConfigHistory>> ListHistoryAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<HabitConfigHistory> list = History.TryGetValue(habitNodeId, out var rows)
            ? rows.OrderBy(h => h.ValidFrom).ToList()
            : Array.Empty<HabitConfigHistory>();
        return new(list);
    }

    public ValueTask UpsertAsync(Guid habitNodeId, Action<HabitConfig> mutate, CancellationToken ct = default)
    {
        Throw();
        if (!Store.TryGetValue(habitNodeId, out var cfg))
        {
            cfg = new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = habitNodeId };
            Store[habitNodeId] = cfg;
        }
        mutate(cfg);
        return ValueTask.CompletedTask;
    }

    public ValueTask AddSegmentAsync(Guid habitNodeId, SegmentDefinition segment, CancellationToken ct = default)
    {
        Throw();
        if (segment.Id == Guid.Empty) segment.Id = Guid.NewGuid();
        segment.HabitNodeId = habitNodeId;
        Segments.Add(segment);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveSegmentAsync(Guid segmentId, CancellationToken ct = default)
    {
        Throw();
        Segments.RemoveAll(s => s.Id == segmentId);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<SegmentDefinition>> ListSegmentsAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        return new(Segments
            .Where(s => s.HabitNodeId == habitNodeId)
            .OrderBy(s => s.SortOrder)
            .ToList());
    }

    public ValueTask UpdateSegmentAsync(Guid segmentId, Action<SegmentDefinition> mutate, CancellationToken ct = default)
    {
        Throw();
        var seg = Segments.FirstOrDefault(s => s.Id == segmentId)
            ?? throw new SegmentDefinitionNotFoundException(segmentId);
        mutate(seg);
        return ValueTask.CompletedTask;
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
