using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeChartService : IChartService
{
    public Dictionary<Guid, Chart> Store { get; } = new();
    public List<(Chart chart, IReadOnlyList<ChartSeries> series)> Created { get; } = new();
    public List<Guid> SeriesReplaced { get; } = new();
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(Chart draft, IReadOnlyList<ChartSeries>? series = null, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        Created.Add((draft, series ?? []));
        return ValueTask.FromResult(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<Chart> mutate, CancellationToken ct = default)
    {
        Throw();
        if (Store.TryGetValue(id, out var c)) mutate(c);
        return ValueTask.CompletedTask;
    }

    public ValueTask ReplaceSeriesAsync(Guid chartId, IReadOnlyList<ChartSeries> series, CancellationToken ct = default)
    {
        Throw();
        SeriesReplaced.Add(chartId);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Chart?> GetAsync(Guid id, CancellationToken ct = default)
        => ValueTask.FromResult(Store.TryGetValue(id, out var c) ? c : null);

    public ValueTask<IReadOnlyList<Chart>> ListAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<Chart>>(Store.Values.OrderBy(c => c.Name).ToList());

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
