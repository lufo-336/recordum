using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Xunit;

namespace Recordum.Plugins.ChartEngine.Tests;

public sealed class ChartEngineRendererMapTests
{
    private static ChartRenderModel Model(ChartKind kind) => new(
        kind, "t",
        [new ChartSeriesModel("S1", "#FF0000", [new ChartPoint(1, 2, "a"), new ChartPoint(2, 4, "b")])],
        new ChartAxisModel(null, true, "yyyy-MM-dd"), new ChartAxisModel(null, false, null),
        new ChartThemeModel(false, "#202020", "#E0E0E0"));

    [Fact]
    public void Identity_and_point()
    {
        var r = new ChartEngineRenderer();
        Assert.Equal(WellKnownExtensionPoints.ChartRenderer, r.PointId);
        Assert.Equal("io.recordum.chart-engine", r.PluginId);
    }

    [Theory]
    [InlineData(ChartKind.Line)]
    [InlineData(ChartKind.Bar)]
    [InlineData(ChartKind.Area)]
    [InlineData(ChartKind.StackedBar)]
    [InlineData(ChartKind.Scatter)]
    [InlineData(ChartKind.Pie)]
    public void CanRender_all_six(ChartKind kind) => Assert.True(new ChartEngineRenderer().CanRender(kind));

    [Fact]
    public void MapSeries_line_produces_one_line_series()
    {
        var series = new ChartEngineRenderer().MapSeries(Model(ChartKind.Line));
        Assert.Single(series);
        Assert.IsType<LineSeries<ObservablePoint>>(series[0]);
    }

    [Fact]
    public void MapSeries_bar_produces_column_series()
        => Assert.IsType<ColumnSeries<ObservablePoint>>(new ChartEngineRenderer().MapSeries(Model(ChartKind.Bar))[0]);

    [Fact]
    public void MapSeries_pie_produces_pie_series_per_model_series()
        => Assert.IsType<PieSeries<double>>(new ChartEngineRenderer().MapSeries(Model(ChartKind.Pie))[0]);
}
