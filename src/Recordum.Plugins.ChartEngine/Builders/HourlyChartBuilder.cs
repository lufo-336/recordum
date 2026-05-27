using System.Globalization;
using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Plugins.ChartEngine.Builders;

public static class HourlyChartBuilder
{
    public static object Build(IReadOnlyList<double> hourBuckets, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(hourBuckets);
        var points = hourBuckets
            .Select((v, h) => new ChartPoint(h, v, h.ToString("00", CultureInfo.InvariantCulture)))
            .ToList();
        var model = new ChartRenderModel(
            ChartKind.Bar, null,
            [new ChartSeriesModel("hour", null, points)],
            new ChartAxisModel(null, false, null),
            new ChartAxisModel(null, false, null),
            new ChartThemeModel(isDark, isDark ? "#E6E6E6" : "#202020", isDark ? "#333333" : "#E0E0E0"));
        return new ChartEngineRenderer().CreateView(model);
    }
}
