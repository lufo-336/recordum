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
}
