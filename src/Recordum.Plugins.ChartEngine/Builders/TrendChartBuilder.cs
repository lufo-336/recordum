using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Plugins.ChartEngine.Builders;

public static class TrendChartBuilder
{
    public static object Build(IReadOnlyList<(double x, double y, string label)> points, bool isBar, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(points);
        var model = new ChartRenderModel(
            isBar ? ChartKind.Bar : ChartKind.Line,
            null,
            [new ChartSeriesModel("trend", null, points.Select(p => new ChartPoint(p.x, p.y, p.label)).ToList())],
            new ChartAxisModel(null, true, "yyyy-MM-dd"),
            new ChartAxisModel(null, false, null),
            new ChartThemeModel(isDark, isDark ? "#E6E6E6" : "#202020", isDark ? "#333333" : "#E0E0E0"));
        return new ChartEngineRenderer().CreateView(model);
    }
}
