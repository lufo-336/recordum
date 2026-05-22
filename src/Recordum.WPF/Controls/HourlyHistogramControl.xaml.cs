using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.Controls;

public partial class HourlyHistogramControl : UserControl
{
    public HourlyHistogramControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        LayoutCanvas.Children.Clear();
        if (DataContext is not HourlyHistogramViewModel vm || ActualWidth <= 0 || ActualHeight <= 0) return;
        if (vm.MaxCount == 0) return;
        double margin = 16, barW = (ActualWidth - 2 * margin) / 24, plotH = ActualHeight - 2 * margin;
        if (plotH <= 0) return;
        var fill = (Brush)FindResource("Brush.Accent");
        for (int h = 0; h < 24; h++)
        {
            double height = plotH * vm.Buckets[h] / vm.MaxCount;
            var rect = new Rectangle
            {
                Width = Math.Max(2, barW - 2),
                Height = height,
                Fill = fill,
                ToolTip = $"{h:00}:00 — {vm.Buckets[h]}",
            };
            Canvas.SetLeft(rect, margin + h * barW);
            Canvas.SetTop(rect, margin + (plotH - height));
            LayoutCanvas.Children.Add(rect);
        }
    }
}
