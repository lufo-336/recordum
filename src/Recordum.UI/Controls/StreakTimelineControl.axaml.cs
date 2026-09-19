using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;

namespace Recordum.UI.Controls;

public partial class StreakTimelineControl : UserControl
{
    public StreakTimelineControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
        // In WPF il colore era fissato al momento del disegno; qui il cambio di variante di tema
        // ridisegna, perché Brush.Accent è diverso in chiaro e scuro.
        ActualThemeVariantChanged += (_, _) => Redraw();
    }

    private void Redraw()
    {
        LayoutCanvas.Children.Clear();
        if (DataContext is not StreakTimelineViewModel vm || Bounds.Width <= 0) return;

        var totalDays = (vm.RangeTo.DayNumber - vm.RangeFrom.DayNumber) + 1;
        if (totalDays <= 0) return;

        var dayWidth = Bounds.Width / totalDays;

        // TryFindResource vuole la variante di tema in Avalonia; FindResource di WPF no.
        var fill = this.TryFindResource("Brush.Accent", ActualThemeVariant, out var found) && found is IBrush b
            ? b
            : Brushes.Transparent;

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
            };
            // In WPF ToolTip è una proprietà diretta su FrameworkElement; in Avalonia è allegata.
            ToolTip.SetTip(rect, $"{run.Start:yyyy-MM-dd} → {run.End:yyyy-MM-dd} ({run.Length} days)");
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 4);
            LayoutCanvas.Children.Add(rect);
        }
    }
}
