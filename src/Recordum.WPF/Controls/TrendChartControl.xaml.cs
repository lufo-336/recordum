using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Recordum.Plugins.ChartEngine.Builders;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.Controls;

public partial class TrendChartControl : UserControl
{
    public TrendChartControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebuild();
        Loaded += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        if (DataContext is not TrendChartViewModel vm) return;
        if (vm.Series.Count == 0 || vm.MaxValue == 0)
        {
            ChartHost.Content = null;
            EmptyText.Text = Strings.Detail_Trend_Empty;
            EmptyText.Visibility = Visibility.Visible;
            return;
        }
        EmptyText.Visibility = Visibility.Collapsed;
        var pts = vm.Series
            .Select(p => (
                p.Bucket.ToDateTime(TimeOnly.MinValue).ToOADate(),
                (double)p.Value,
                p.Bucket.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .ToList();
        var isBar = vm.ChartKind == TrendChartKind.Bar;
        ChartHost.Content = TrendChartBuilder.Build(pts, isBar, ThemeProbe.IsDark());
    }
}
