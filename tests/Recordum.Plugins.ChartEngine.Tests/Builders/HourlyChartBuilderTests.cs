using LiveChartsCore.SkiaSharpView.WPF;
using Recordum.Plugins.ChartEngine.Builders;
using Xunit;

namespace Recordum.Plugins.ChartEngine.Tests.Builders;

public sealed class HourlyChartBuilderTests
{
    [WpfFact]
    public void Build_returns_cartesian_chart_for_24_buckets()
    {
        var buckets = new double[24];
        buckets[9] = 3; buckets[21] = 1;
        var chart = HourlyChartBuilder.Build(buckets, isDark: true);
        Assert.IsType<CartesianChart>(chart);
    }
}
