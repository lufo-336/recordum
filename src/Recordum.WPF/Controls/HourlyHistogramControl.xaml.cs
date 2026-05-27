using System.Windows.Controls;
using Recordum.Plugins.ChartEngine.Builders;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.Controls;

public partial class HourlyHistogramControl : UserControl
{
    public HourlyHistogramControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebuild();
        Loaded += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        if (DataContext is not HourlyHistogramViewModel vm) return;
        if (vm.MaxCount == 0) { ChartHost.Content = null; return; }
        var buckets = Array.ConvertAll(vm.Buckets, b => (double)b);
        ChartHost.Content = HourlyChartBuilder.Build(buckets, ThemeProbe.IsDark());
    }
}
