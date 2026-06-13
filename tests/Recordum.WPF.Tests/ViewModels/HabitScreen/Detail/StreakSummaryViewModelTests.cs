using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class StreakSummaryViewModelTests
{
    [Fact]
    public async Task Empty_streaks_sets_IsEmpty_true()
    {
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });

        Assert.True(vm.IsEmpty);
        Assert.Equal(0, vm.CurrentLength);
        Assert.Equal(0, vm.LongestLength);
        Assert.Equal(0, vm.RunsLast90Days);
    }

    [Fact]
    public async Task Current_streak_is_run_that_contains_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-4), today, 5));

        var vm = new StreakSummaryViewModel(aggr, clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime });

        Assert.Equal(5, vm.CurrentLength);
    }

    [Fact]
    public async Task Current_zero_when_last_active_day_more_than_one_day_ago()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-10), today.AddDays(-3), 8));

        var vm = new StreakSummaryViewModel(aggr, clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime });

        Assert.Equal(0, vm.CurrentLength);
    }

    [Fact]
    public async Task Resistance_current_streak_renders_elapsed_format()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-770), today, 771)); // 771 days = 2y 41d

        var vm = new StreakSummaryViewModel(aggr, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = Guid.NewGuid(),
            HabitType = HabitType.Resistance,
            CreatedAt = clock.UtcNow.AddYears(-3).UtcDateTime,
        });

        Assert.Equal("2a 41g", vm.CurrentDisplay);
    }

    [Fact]
    public async Task Non_resistance_current_streak_stays_numeric()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-11), today, 12));

        var vm = new StreakSummaryViewModel(aggr, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = Guid.NewGuid(),
            HabitType = HabitType.Counter,
            CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime,
        });

        Assert.Equal("12", vm.CurrentDisplay);
    }

    [Fact]
    public async Task Longest_pulls_max_length_across_all_time()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 14), 14));
        aggr.Runs.Add(new StreakRun(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), 5));

        var vm = new StreakSummaryViewModel(aggr, clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        Assert.Equal(14, vm.LongestLength);
        Assert.Equal(new DateOnly(2025, 1, 1), vm.LongestStartedOn);
    }
}
