using System.Windows;
using LiveChartsCore.SkiaSharpView.WPF;
using Recordum.Plugins.Contracts.Charts;
using Xunit;

namespace Recordum.Plugins.ChartEngine.Tests;

public sealed class ChartEngineRendererViewTests
{
    private static ChartRenderModel Model(ChartKind kind) => new(
        kind, "t",
        [new ChartSeriesModel("S1", "#FF0000", [new ChartPoint(1, 2, "a")])],
        new ChartAxisModel("X", true, "yyyy-MM-dd"), new ChartAxisModel("Y", false, null),
        new ChartThemeModel(true, "#E6E6E6", "#333333"));

    [WpfFact]
    public void CreateView_cartesian_returns_CartesianChart()
        => Assert.IsType<CartesianChart>(new ChartEngineRenderer().CreateView(Model(ChartKind.Line)));

    [WpfFact]
    public void CreateView_pie_returns_PieChart()
        => Assert.IsType<PieChart>(new ChartEngineRenderer().CreateView(Model(ChartKind.Pie)));

    [WpfFact]
    public void CreateView_result_is_a_FrameworkElement()
        => Assert.IsAssignableFrom<FrameworkElement>(new ChartEngineRenderer().CreateView(Model(ChartKind.Bar)));
}
