using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeChartQueryService : IChartQueryService
{
    public ValueTask<ChartRenderModel> BuildModelAsync(Guid chartId, bool isDark, CancellationToken ct = default)
        => ValueTask.FromResult(new ChartRenderModel(ChartKind.Line, "t",
            [new ChartSeriesModel("S", null, [new ChartPoint(1, 1, "a")])],
            null, null, new ChartThemeModel(isDark, "#000", "#ccc")));

    public ValueTask<ChartRenderModel> BuildModelAsync(Chart chart, IReadOnlyList<ChartSeries> series, bool isDark, CancellationToken ct = default)
        => ValueTask.FromResult(new ChartRenderModel(ChartKind.Line, "t",
            [new ChartSeriesModel("S", null, [new ChartPoint(1, 1, "a")])],
            null, null, new ChartThemeModel(isDark, "#000", "#ccc")));
}
