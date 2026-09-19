using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Detail;

public sealed class TrendChartViewModelTests
{
    [Fact]
    public async Task ChartKind_is_Line_for_Counter()
    {
        var vm = new TrendChartViewModel(new FakeTrendQueryService(), new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter }, DayBoundary.Utc);
        Assert.Equal(TrendChartKind.Line, vm.ChartKind);
    }

    [Fact]
    public async Task ChartKind_is_Bar_for_Action()
    {
        var vm = new TrendChartViewModel(new FakeTrendQueryService(), new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action }, DayBoundary.Utc);
        Assert.Equal(TrendChartKind.Bar, vm.ChartKind);
    }

    [Fact]
    public async Task SetRangeAsync_reloads_with_new_range_and_raises_RangeChanged()
    {
        var trend = new FakeTrendQueryService();
        var vm = new TrendChartViewModel(trend, new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero)));
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter }, DayBoundary.Utc);
        TrendRange captured = TrendRange.Days30;
        vm.RangeChanged += (_, r) => captured = r;

        await vm.SetRangeAsync(TrendRange.Year1);

        Assert.Equal(TrendRange.Year1, vm.SelectedRange);
        Assert.Equal(TrendRange.Year1, captured);
    }

    [Fact]
    public async Task Year1_uses_Weekly_granularity()
    {
        var trend = new FakeTrendQueryService();
        var vm = new TrendChartViewModel(trend, new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter }, DayBoundary.Utc);

        await vm.SetRangeAsync(TrendRange.Year1);

        Assert.Equal(TrendGranularity.Weekly, vm.CurrentGranularity);
        Assert.Equal(TrendGranularity.Weekly, trend.LastGranularity);
    }
}
