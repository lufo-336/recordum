using System.Windows;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Controls;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

public sealed class TrendChartControlSmokeTests
{
    [WpfFact]
    public async Task Loads_and_hosts_a_chart_when_data_present()
    {
        var trend = new FakeTrendQueryService();
        trend.Result.Add(new TrendPoint(new DateOnly(2026, 5, 20), 3m, 3));
        trend.Result.Add(new TrendPoint(new DateOnly(2026, 5, 21), 5m, 5));
        var vm = new TrendChartViewModel(trend, new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter });

        var control = new TrendChartControl { DataContext = vm };

        Assert.NotNull(control.ChartHost.Content);
    }

    [WpfFact]
    public void Late_series_fill_triggers_rebuild()
    {
        var trend = new FakeTrendQueryService();
        var vm = new TrendChartViewModel(trend, new FakeClock());
        var control = new TrendChartControl { DataContext = vm };
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        // VM starts empty (no LoadAsync awaited yet): empty state, ChartHost has no content.
        Assert.Null(control.ChartHost.Content);

        // Simulate LoadAsync's completion arriving after the control already bound/loaded.
        vm.Series.Add(new TrendPoint(new DateOnly(2026, 7, 1), 5m, 1));
        vm.MaxValue = 5m;

        Assert.NotNull(control.ChartHost.Content);
    }
}
