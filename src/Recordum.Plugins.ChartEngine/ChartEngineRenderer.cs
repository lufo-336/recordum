using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.Contracts.ExtensionPoints;
using SkiaSharp;

namespace Recordum.Plugins.ChartEngine;

public sealed class ChartEngineRenderer : IChartRenderer
{
    public ExtensionPointId PointId => WellKnownExtensionPoints.ChartRenderer;
    public string PluginId => "io.recordum.chart-engine";

    public bool CanRender(ChartKind kind) => Enum.IsDefined(kind);

    /// <summary>Pure mapping ChartRenderModel → LiveCharts2 series. Testable without a control.</summary>
#pragma warning disable CA1822 // intentional public instance method — renderer's testable mapping surface, mirrors CreateView
    public ISeries[] MapSeries(ChartRenderModel model)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Kind == ChartKind.Pie)
        {
            return model.Series
                .Select(s => (ISeries)new PieSeries<double>
                {
                    Name = s.Label,
                    Values = [s.Points.Sum(p => p.Y ?? 0d)],
                    Fill = Paint(s.ColorHex),
                })
                .ToArray();
        }

        return model.Series.Select(s => BuildCartesian(model.Kind, s)).ToArray();
    }

    private static ISeries BuildCartesian(ChartKind kind, ChartSeriesModel s)
    {
        var values = s.Points.Select(p => new ObservablePoint(p.X, p.Y)).ToArray();
        var stroke = Paint(s.ColorHex);
        return kind switch
        {
            ChartKind.Bar => new ColumnSeries<ObservablePoint> { Name = s.Label, Values = values, Fill = stroke },
            ChartKind.StackedBar => new StackedColumnSeries<ObservablePoint> { Name = s.Label, Values = values, Fill = stroke },
            ChartKind.Scatter => new ScatterSeries<ObservablePoint> { Name = s.Label, Values = values, Fill = stroke },
            ChartKind.Area => new LineSeries<ObservablePoint> { Name = s.Label, Values = values, Stroke = stroke, Fill = Paint(s.ColorHex, alpha: 64), GeometryFill = null, GeometryStroke = null },
            _ => new LineSeries<ObservablePoint> { Name = s.Label, Values = values, Stroke = stroke, Fill = null, GeometryFill = null, GeometryStroke = null },
        };
    }

    private static SolidColorPaint? Paint(string? hex, byte alpha = 255)
    {
        if (string.IsNullOrWhiteSpace(hex) || !SKColor.TryParse(hex, out var c)) return null;
        return new SolidColorPaint(c.WithAlpha(alpha));
    }

    public object CreateView(ChartRenderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var textPaint = Paint(model.Theme.TextColorHex);
        var gridPaint = Paint(model.Theme.GridColorHex);
        var series = MapSeries(model);

        if (model.Kind == ChartKind.Pie)
        {
            return new LiveChartsCore.SkiaSharpView.WPF.PieChart
            {
                Series = series,
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
            };
        }

        var xAxis = new LiveChartsCore.SkiaSharpView.Axis
        {
            Name = model.XAxis?.Label,
            NamePaint = textPaint,
            LabelsPaint = textPaint,
            SeparatorsPaint = gridPaint,
        };
        if (model.XAxis?.IsTime == true)
        {
            // X carries OADate values → label ticks as dates. Non-time axes (e.g. hour 0–23)
            // keep LiveCharts' default numeric labels.
            xAxis.Labeler = value => DateTime.FromOADate(value).ToString("MM-dd", CultureInfo.InvariantCulture);
            xAxis.UnitWidth = TimeSpan.FromDays(1).TotalDays;
            xAxis.MinStep = TimeSpan.FromDays(1).TotalDays;
        }

        return new LiveChartsCore.SkiaSharpView.WPF.CartesianChart
        {
            Series = series,
            XAxes = [xAxis],
            YAxes =
            [
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Name = model.YAxis?.Label,
                    NamePaint = textPaint,
                    LabelsPaint = textPaint,
                    SeparatorsPaint = gridPaint,
                },
            ],
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
        };
    }
}
