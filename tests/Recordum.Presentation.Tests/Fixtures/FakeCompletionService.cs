using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeCompletionService : ICompletionService
{
    public Dictionary<Guid, Completion> Store { get; } = new();
    public Dictionary<Guid, List<CompletionValue>> ValuesByCompletion { get; } = new();
    public Dictionary<Guid, IReadOnlyList<HeatmapCell>> HeatmapByHabit { get; } = new();
    public Dictionary<(Guid HabitId, Guid MetricId), IReadOnlyList<DailyValue>> DailySumByKey { get; } = new();
    public Dictionary<Guid, IReadOnlyList<DailyMagnitude>> MagnitudeByHabit { get; } = new();
    public Exception? NextException { get; set; }

    public Completion? LastRecord { get; private set; }
    public IReadOnlyList<CompletionValue>? LastValues { get; private set; }
    public IReadOnlyList<CompletionSegment>? LastSegments { get; private set; }

    public List<Guid> DeleteCalls { get; } = [];
    public List<(Guid Id, Action<Completion> Mutator)> UpdateCalls { get; } = [];

    /// <summary>Se impostato, RecordAsync attende questo task prima di registrare (salvataggio in volo).</summary>
    public Task? RecordGate { get; set; }
    public int RecordCalls { get; private set; }

    public async ValueTask<Guid> RecordAsync(
        Completion draft,
        IReadOnlyList<CompletionValue>? values = null,
        IReadOnlyList<CompletionSegment>? segments = null,
        CancellationToken ct = default)
    {
        RecordCalls++;
        if (RecordGate is { } gate) await gate.ConfigureAwait(false);
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        LastRecord = draft;
        LastValues = values;
        LastSegments = segments;
        if (values is not null) ValuesByCompletion[draft.Id] = values.ToList();
        return draft.Id;
    }

    public ValueTask UpdateAsync(Guid id, Action<Completion> mutate, CancellationToken ct = default)
    {
        Throw();
        UpdateCalls.Add((id, mutate));
        if (Store.TryGetValue(id, out var c)) mutate(c);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        DeleteCalls.Add(id);
        Store.Remove(id);
        ValuesByCompletion.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Completion?> GetAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        return new(Store.TryGetValue(id, out var c) ? c : null);
    }

    public ValueTask<IReadOnlyList<Completion>> ListByHabitAsync(
        Guid habitNodeId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int? take = null,
        CancellationToken ct = default)
    {
        Throw();
        var q = Store.Values.Where(c => c.HabitNodeId == habitNodeId);
        if (fromUtc is { } f) q = q.Where(c => c.CompletedAtMs >= f.ToUnixTimeMilliseconds());
        if (toUtc is { } t) q = q.Where(c => c.CompletedAtMs <= t.ToUnixTimeMilliseconds());
        var ordered = q.OrderByDescending(c => c.CompletedAtMs);
        IReadOnlyList<Completion> list = take is { } n
            ? ordered.Take(n).ToList()
            : ordered.ToList();
        return new(list);
    }

    public ValueTask<IReadOnlyList<HeatmapCell>> GetHeatmapAsync(
        Guid habitNodeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<HeatmapCell> list = HeatmapByHabit.TryGetValue(habitNodeId, out var cells)
            ? cells
            : [];
        return new(list);
    }

    public ValueTask<IReadOnlyList<DailyValue>> GetDailySumAsync(
        Guid habitNodeId,
        Guid metricDefinitionId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<DailyValue> list = DailySumByKey.TryGetValue((habitNodeId, metricDefinitionId), out var rows)
            ? rows
            : [];
        return new(list);
    }

    public ValueTask<IReadOnlyList<DailyMagnitude>> GetDailyMagnitudeAsync(
        Guid habitNodeId,
        int year,
        int month,
        CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<DailyMagnitude> list = MagnitudeByHabit.TryGetValue(habitNodeId, out var rows)
            ? rows.Where(m => m.Date.Year == year && m.Date.Month == month).ToList()
            : [];
        return new(list);
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
