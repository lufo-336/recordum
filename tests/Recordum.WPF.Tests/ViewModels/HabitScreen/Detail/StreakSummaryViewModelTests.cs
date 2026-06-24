using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
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
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), new FakeCompletionService(), new FakeClock());
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

        var vm = new StreakSummaryViewModel(aggr, new FakeCompletionService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime });

        Assert.Equal(5, vm.CurrentLength);
    }

    [Fact]
    public async Task A_lapse_closed_run_ending_yesterday_does_not_feed_CurrentLength()
    {
        // CR12: a same-day lapse closes the run ending yesterday (BreakReason.Lapse); it must not keep
        // CurrentLength on that stale run — the current clean streak is 0 until a new clean day.
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-10), today.AddDays(-1), 10,
            IsCurrent: true, BreakReason: StreakBreakReason.Lapse));

        var vm = new StreakSummaryViewModel(aggr, new FakeCompletionService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime });

        Assert.Equal(0, vm.CurrentLength);
    }

    [Fact]
    public async Task Current_zero_when_last_active_day_more_than_one_day_ago()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-10), today.AddDays(-3), 8));

        var vm = new StreakSummaryViewModel(aggr, new FakeCompletionService(), clock);
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
        var nodeId = Guid.NewGuid();
        var completions = new FakeCompletionService();
        // last lapse was exactly 771 days ago → FormatElapsed gives "2a 41g"
        var lastLapse = clock.UtcNow.AddDays(-771);
        completions.Store[Guid.NewGuid()] = new Completion
        {
            HabitNodeId = nodeId, CompletedAt = lastLapse.UtcDateTime,
            CompletedAtMs = lastLapse.ToUnixTimeMilliseconds(),
        };

        var vm = new StreakSummaryViewModel(aggr, completions, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = nodeId,
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

        var vm = new StreakSummaryViewModel(aggr, new FakeCompletionService(), clock);
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

        var vm = new StreakSummaryViewModel(aggr, new FakeCompletionService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        Assert.Equal(14, vm.LongestLength);
        Assert.Equal(new DateOnly(2025, 1, 1), vm.LongestStartedOn);
    }

    [WpfFact]
    public async Task Resistance_current_shows_elapsed_since_last_lapse()
    {
        var today = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-100).UtcDateTime,
        };
        var completions = new FakeCompletionService();
        var lastLapse = today.AddDays(-3).AddHours(-6); // 3d 6h ago
        completions.Store[Guid.NewGuid()] = new Completion
        {
            HabitNodeId = node.Id, CompletedAt = lastLapse.UtcDateTime, CompletedAtMs = lastLapse.ToUnixTimeMilliseconds(),
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), completions, clock);

        await vm.LoadAsync(node);

        vm.CurrentDisplay.Should().Be("3g 6h");
    }

    [WpfFact]
    public async Task Resistance_never_lapsed_counts_from_creation()
    {
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-10).UtcDateTime,
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), new FakeCompletionService(), clock);

        await vm.LoadAsync(node);

        vm.CurrentDisplay.Should().Be("10g 0h");
    }

    [WpfFact]
    public async Task Resistance_caption_is_lapses_and_counts_lapse_days_in_90()
    {
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-100).UtcDateTime,
        };
        var completions = new FakeCompletionService();
        completions.HeatmapByHabit[node.Id] = new[]
        {
            new HeatmapCell(DateOnly.FromDateTime(today.AddDays(-10).UtcDateTime), 1, 0),
            new HeatmapCell(DateOnly.FromDateTime(today.AddDays(-40).UtcDateTime), 2, 0),
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), completions, clock);

        await vm.LoadAsync(node);

        vm.RunsCaption.Should().Be(Recordum.WPF.Resources.Strings.Detail_Streak_Lapses90);
        vm.RunsLast90Days.Should().Be(2);
    }
}
