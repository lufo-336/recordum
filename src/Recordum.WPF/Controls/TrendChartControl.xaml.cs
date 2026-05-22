using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.Controls;

public partial class TrendChartControl : UserControl
{
    public TrendChartControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        LayoutCanvas.Children.Clear();
        EmptyText.Visibility = Visibility.Collapsed;
        if (DataContext is not TrendChartViewModel vm || ActualWidth <= 0 || ActualHeight <= 0) return;
        if (vm.Series.Count == 0 || vm.MaxValue == 0)
        {
            EmptyText.Text = Strings.Detail_Trend_Empty;
            EmptyText.Visibility = Visibility.Visible;
            return;
        }
        double margin = 24, plotW = ActualWidth - 2 * margin, plotH = ActualHeight - 2 * margin;
        if (plotW <= 0 || plotH <= 0) return;
        var stroke = (Brush)FindResource("Brush.Accent");
        var area = stroke.Clone();
        area.Opacity = 0.2;

        if (vm.ChartKind == TrendChartKind.Bar)
        {
            double barW = plotW / vm.Series.Count;
            for (int i = 0; i < vm.Series.Count; i++)
            {
                var p = vm.Series[i];
                double h = plotH * (double)(p.Value / vm.MaxValue);
                var rect = new Rectangle
                {
                    Width = Math.Max(2, barW - 2),
                    Height = h,
                    Fill = stroke,
                    ToolTip = $"{p.Bucket:yyyy-MM-dd} — {p.Count}",
                };
                Canvas.SetLeft(rect, margin + i * barW);
                Canvas.SetTop(rect, margin + (plotH - h));
                LayoutCanvas.Children.Add(rect);
            }
        }
        else
        {
            var poly = new Polyline { Stroke = stroke, StrokeThickness = 2 };
            var areaPoly = new Polygon { Fill = area };
            double xStep = vm.Series.Count <= 1 ? plotW : plotW / (vm.Series.Count - 1);
            var points = new PointCollection();
            for (int i = 0; i < vm.Series.Count; i++)
            {
                double y = margin + plotH * (1 - (double)(vm.Series[i].Value / vm.MaxValue));
                double x = margin + i * xStep;
                points.Add(new Point(x, y));
            }
            poly.Points = points;
            areaPoly.Points = new PointCollection(points);
            areaPoly.Points.Insert(0, new Point(margin, margin + plotH));
            areaPoly.Points.Add(new Point(margin + (vm.Series.Count - 1) * xStep, margin + plotH));
            LayoutCanvas.Children.Add(areaPoly);
            LayoutCanvas.Children.Add(poly);
        }
    }
}
