using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Data.Services.Queries;

public interface IChartQueryService
{
    /// <summary>Builds a render model from a persisted chart, resolving each series' data over the configured range.</summary>
    ValueTask<ChartRenderModel> BuildModelAsync(Guid chartId, bool isDark, CancellationToken ct = default);
}
