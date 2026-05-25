using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IChartService
{
    ValueTask<Guid> CreateAsync(Chart draft, IReadOnlyList<ChartSeries>? series = null, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<Chart> mutate, CancellationToken ct = default);
    ValueTask ReplaceSeriesAsync(Guid chartId, IReadOnlyList<ChartSeries> series, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<Chart?> GetAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Chart>> ListAsync(CancellationToken ct = default);
}
