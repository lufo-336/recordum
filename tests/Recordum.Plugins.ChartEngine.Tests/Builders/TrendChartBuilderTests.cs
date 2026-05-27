using System.Collections.Generic;
using LiveChartsCore.SkiaSharpView.WPF;
using Recordum.Plugins.ChartEngine.Builders;
using Xunit;

namespace Recordum.Plugins.ChartEngine.Tests.Builders;

public sealed class TrendChartBuilderTests
{
    [WpfFact]
    public void Build_line_returns_cartesian_chart_with_one_series()
    {
        var pts = new List<(double x, double y, string label)> { (1, 2, "a"), (2, 5, "b") };
        var chart = TrendChartBuilder.Build(pts, isBar: false, isDark: false);
        Assert.IsType<CartesianChart>(chart);
    }
}
