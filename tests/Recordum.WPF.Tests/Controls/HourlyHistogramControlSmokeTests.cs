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
}
