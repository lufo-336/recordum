using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Data.Services.Queries;

public interface IChartQueryService
{
    /// <summary>Builds a render model from a persisted chart, resolving each series' data over the configured range.</summary>
    ValueTask<ChartRenderModel> BuildModelAsync(Guid chartId, bool isDark, CancellationToken ct = default);

    /// <summary>Preview: builds the model from unsaved state — no access to the `chart` table; series still resolve their data from completions in the DB.</summary>
    ValueTask<ChartRenderModel> BuildModelAsync(Chart chart, IReadOnlyList<ChartSeries> series, bool isDark, CancellationToken ct = default);
}
