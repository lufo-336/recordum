using System.Windows;
using System.Windows.Controls;
using Recordum.Plugins.ChartEngine.Builders;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.Controls;

public partial class HourlyHistogramControl : UserControl
{
    private HourlyHistogramViewModel? _vm;

    public HourlyHistogramControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { Rehook(); Rebuild(); };
        // Rehook (not just Rebuild) here too: Unloaded drops the VM subscription, and a
        // Loaded/Unloaded cycle without a DataContext change (e.g. tab virtualization) would
        // otherwise leave the control rendering once but never reacting to later async data.
        Loaded += (_, _) => { Rehook(); Rebuild(); };
        Unloaded += (_, _) => Unhook();
    }

    private void Rehook()
    {
        Unhook();
        _vm = DataContext as HourlyHistogramViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void Unhook()
    {
        if (_vm is null) return;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HourlyHistogramViewModel.MaxCount) or nameof(HourlyHistogramViewModel.Buckets)) Rebuild();
    }

    private void Rebuild()
    {
        if (DataContext is not HourlyHistogramViewModel vm) return;
        if (vm.MaxCount == 0)
        {
            ChartHost.Content = null;
            EmptyText.Text = Strings.Detail_Hourly_Empty;
            EmptyText.Visibility = Visibility.Visible;
            return;
        }
        EmptyText.Visibility = Visibility.Collapsed;
        var buckets = Array.ConvertAll(vm.Buckets, b => (double)b);
        ChartHost.Content = HourlyChartBuilder.Build(buckets, ThemeProbe.IsDark());
    }
}
