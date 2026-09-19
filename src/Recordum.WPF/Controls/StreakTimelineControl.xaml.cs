using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.Controls;

public partial class StreakTimelineControl : UserControl
{
    public StreakTimelineControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Redraw();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        LayoutCanvas.Children.Clear();
        if (DataContext is not StreakTimelineViewModel vm || ActualWidth <= 0) return;
        var totalDays = (vm.RangeTo.DayNumber - vm.RangeFrom.DayNumber) + 1;
        if (totalDays <= 0) return;
        var dayWidth = ActualWidth / totalDays;
        var fill = (Brush)FindResource("Brush.Accent");
        foreach (var run in vm.Runs)
        {
            var x = (run.Start.DayNumber - vm.RangeFrom.DayNumber) * dayWidth;
            var w = run.Length * dayWidth;
            var rect = new Rectangle
            {
                Width = Math.Max(2, w),
                Height = 16,
                RadiusX = 2,
                RadiusY = 2,
                Fill = fill,
                ToolTip = $"{run.Start:yyyy-MM-dd} → {run.End:yyyy-MM-dd} ({run.Length} days)",
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 4);
            LayoutCanvas.Children.Add(rect);
        }
    }
}
