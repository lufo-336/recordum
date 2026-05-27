using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Charts;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeChartQueryService : IChartQueryService
{
    public ValueTask<ChartRenderModel> BuildModelAsync(Guid chartId, bool isDark, CancellationToken ct = default)
        => ValueTask.FromResult(new ChartRenderModel(ChartKind.Line, "t",
            [new ChartSeriesModel("S", null, [new ChartPoint(1, 1, "a")])],
            null, null, new ChartThemeModel(isDark, "#000", "#ccc")));
}
