using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class StreakTimelineViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_90_day_window_ending_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-10), today.AddDays(-6), 5));

        var vm = new StreakTimelineViewModel(aggr, clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid() });

        Assert.Equal(today.AddDays(-89), vm.RangeFrom);
        Assert.Equal(today, vm.RangeTo);
        Assert.Single(vm.Runs);
        Assert.Equal(5, vm.Runs[0].Length);
    }
}
