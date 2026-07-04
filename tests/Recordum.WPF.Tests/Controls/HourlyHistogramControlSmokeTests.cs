using System.Windows;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Controls;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

public sealed class HourlyHistogramControlSmokeTests
{
    [WpfFact]
    public async Task Loads_and_hosts_a_chart_when_data_present()
    {
        var svc = new FakeHourlyDistributionQueryService();
        svc.Buckets[9] = 4;
        svc.Buckets[18] = 2;
        var vm = new HourlyHistogramViewModel(svc, new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action });

        var control = new HourlyHistogramControl { DataContext = vm };

        Assert.NotNull(control.ChartHost.Content);
    }

    [WpfFact]
    public void Late_bucket_fill_triggers_rebuild()
    {
        var svc = new FakeHourlyDistributionQueryService();
        var vm = new HourlyHistogramViewModel(svc, new FakeClock());
        var control = new HourlyHistogramControl { DataContext = vm };
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        // VM starts empty (no LoadAsync awaited yet): MaxCount == 0, ChartHost has no content.
        Assert.Null(control.ChartHost.Content);

        // Simulate LoadAsync/SetRangeAsync's completion arriving after the control already bound/loaded:
        // Buckets is reassigned wholesale in real code (private setter here, so we mutate the existing
        // array in place — the array reference itself is what Rebuild() reads), then MaxCount changes.
        vm.Buckets[9] = 4;
        vm.MaxCount = 4;

        Assert.NotNull(control.ChartHost.Content);
    }
}
