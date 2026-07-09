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
    private TrendChartViewModel? _vm;

    public TrendChartControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { Rehook(); Rebuild(); };
        // Rehook (not just Rebuild) here too: Unloaded drops the VM subscriptions, and a
        // Loaded/Unloaded cycle without a DataContext change (e.g. tab virtualization) would
        // otherwise leave the control rendering once but never reacting to later async data.
        Loaded += (_, _) => { Rehook(); Rebuild(); };
        Unloaded += (_, _) => Unhook();
    }

    private void Rehook()
    {
        Unhook();
        _vm = DataContext as TrendChartViewModel;
        if (_vm is null) return;
        _vm.Series.CollectionChanged += OnVmChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void Unhook()
    {
        if (_vm is null) return;
        _vm.Series.CollectionChanged -= OnVmChanged;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
    }

    private void OnVmChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrendChartViewModel.MaxValue) or nameof(TrendChartViewModel.ChartKind)) Rebuild();
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
