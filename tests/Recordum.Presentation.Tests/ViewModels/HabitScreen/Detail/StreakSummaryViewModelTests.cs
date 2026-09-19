using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Detail;

public sealed class StreakSummaryViewModelTests
{
    [Fact]
    public async Task Empty_streaks_sets_IsEmpty_true()
    {
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), new FakeAbstinenceService(), new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }, DayBoundary.Utc);

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

        var vm = new StreakSummaryViewModel(aggr, new FakeAbstinenceService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime }, DayBoundary.Utc);

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

        var vm = new StreakSummaryViewModel(aggr, new FakeAbstinenceService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime }, DayBoundary.Utc);

        Assert.Equal(0, vm.CurrentLength);
    }

    [Fact]
    public async Task Current_zero_when_last_active_day_more_than_one_day_ago()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-10), today.AddDays(-3), 8));

        var vm = new StreakSummaryViewModel(aggr, new FakeAbstinenceService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime }, DayBoundary.Utc);

        Assert.Equal(0, vm.CurrentLength);
    }

    [Fact]
    public async Task Resistance_current_streak_renders_stopwatch_format()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-770), today, 771)); // 771 days
        var nodeId = Guid.NewGuid();
        var abstinence = new FakeAbstinenceService();
        // abstaining since exactly 771 days ago → FormatStopwatch gives "771g 0:00:00"
        var currentStart = clock.UtcNow.AddDays(-771);
        abstinence.States[nodeId] = new AbstinenceState(AbstinenceStatus.Abstaining, currentStart.ToUnixTimeMilliseconds(), null);

        var vm = new StreakSummaryViewModel(aggr, abstinence, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = nodeId,
            HabitType = HabitType.Resistance,
            CreatedAt = clock.UtcNow.AddYears(-3).UtcDateTime,
        }, DayBoundary.Utc);

        Assert.Equal("771g 0:00:00", vm.CurrentDisplay);
    }

    [Fact]
    public async Task Non_resistance_current_streak_stays_numeric()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(today.AddDays(-11), today, 12));

        var vm = new StreakSummaryViewModel(aggr, new FakeAbstinenceService(), clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = Guid.NewGuid(),
            HabitType = HabitType.Counter,
            CreatedAt = clock.UtcNow.AddYears(-1).UtcDateTime,
        }, DayBoundary.Utc);

        Assert.Equal("12", vm.CurrentDisplay);
    }

    [Fact]
    public async Task Longest_pulls_max_length_across_all_time()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var aggr = new FakeStreakAggregator();
        aggr.Runs.Add(new StreakRun(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 14), 14));
        aggr.Runs.Add(new StreakRun(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), 5));

        var vm = new StreakSummaryViewModel(aggr, new FakeAbstinenceService(), clock);
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid(), CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, DayBoundary.Utc);

        Assert.Equal(14, vm.LongestLength);
        Assert.Equal(new DateOnly(2025, 1, 1), vm.LongestStartedOn);
    }

    // ── New TDD tests from brief (Task-7) ──────────────────────────────────

    /// <summary>Brief test (a): Resistance + Abstaining → CurrentDisplay == FormatStopwatch; RunsCaption == Lapses90.</summary>
    [Fact]
    public async Task Resistance_abstaining_shows_stopwatch_from_current_start()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 28, 10, 0, 0, TimeSpan.Zero));
        var nodeId = Guid.NewGuid();
        var currentStart = clock.UtcNow.AddDays(-5); // 5 days ago
        var abstinence = new FakeAbstinenceService();
        abstinence.States[nodeId] = new AbstinenceState(AbstinenceStatus.Abstaining, currentStart.ToUnixTimeMilliseconds(), null);

        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = nodeId,
            HabitType = HabitType.Resistance,
            CreatedAt = clock.UtcNow.AddDays(-100).UtcDateTime,
        }, DayBoundary.Utc);

        var expectedDisplay = Recordum.Presentation.Formatting.DurationFormat.FormatStopwatch(clock.UtcNow - currentStart);
        vm.CurrentDisplay.Should().Be(expectedDisplay);
        vm.RunsCaption.Should().Be(Recordum.Presentation.Resources.Strings.Detail_Streak_Lapses90);
    }

    /// <summary>Brief test (b): Resistance + Relapsed/NotStarted → CurrentDisplay == Abstinence_NotStarted.</summary>
    [Fact]
    public async Task Resistance_not_abstaining_shows_not_started_state()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 28, 10, 0, 0, TimeSpan.Zero));
        var nodeId = Guid.NewGuid();
        var abstinence = new FakeAbstinenceService();
        abstinence.States[nodeId] = new AbstinenceState(AbstinenceStatus.Relapsed, null, clock.UtcNow.AddDays(-1).ToUnixTimeMilliseconds());

        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = nodeId,
            HabitType = HabitType.Resistance,
            CreatedAt = clock.UtcNow.AddDays(-100).UtcDateTime,
        }, DayBoundary.Utc);

        vm.CurrentDisplay.Should().Be(Recordum.Presentation.Resources.Strings.Abstinence_NotStarted);
    }

    /// <summary>Brief test (c): RunsLast90Days == number of periods closed in the last 90 days.</summary>
    [Fact]
    public async Task Resistance_lapses90_counts_closed_periods_in_window()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var nodeId = Guid.NewGuid();
        var abstinence = new FakeAbstinenceService();
        var t = clock.UtcNow;
        abstinence.Periods[nodeId] =
        [
            // closed 10 days ago — inside window
            new AbstinencePeriod { HabitNodeId = nodeId, StartedAtMs = t.AddDays(-20).ToUnixTimeMilliseconds(), EndedAtMs = t.AddDays(-10).ToUnixTimeMilliseconds() },
            // closed 50 days ago — inside window
            new AbstinencePeriod { HabitNodeId = nodeId, StartedAtMs = t.AddDays(-60).ToUnixTimeMilliseconds(), EndedAtMs = t.AddDays(-50).ToUnixTimeMilliseconds() },
            // closed 100 days ago — outside 90-day window
            new AbstinencePeriod { HabitNodeId = nodeId, StartedAtMs = t.AddDays(-110).ToUnixTimeMilliseconds(), EndedAtMs = t.AddDays(-100).ToUnixTimeMilliseconds() },
            // still open (EndedAtMs == null) — does not count as a closed lapse
            new AbstinencePeriod { HabitNodeId = nodeId, StartedAtMs = t.AddDays(-5).ToUnixTimeMilliseconds(), EndedAtMs = null },
        ];

        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);
        await vm.LoadAsync(new HabitNode
        {
            Id = nodeId,
            HabitType = HabitType.Resistance,
            CreatedAt = clock.UtcNow.AddDays(-200).UtcDateTime,
        }, DayBoundary.Utc);

        vm.RunsLast90Days.Should().Be(2);
    }

    // ── Live-clock tests (ITickSource is injected, so no STA thread / Dispatcher needed) ──

    [Fact]
    public async Task Resistance_current_shows_stopwatch_since_last_lapse()
    {
        var today = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-100).UtcDateTime,
        };
        var abstinence = new FakeAbstinenceService();
        var currentStart = today.AddDays(-3).AddHours(-6); // 3d 6h ago
        abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.Abstaining, currentStart.ToUnixTimeMilliseconds(), null);
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

        await vm.LoadAsync(node, DayBoundary.Utc);

        vm.CurrentDisplay.Should().Be("3g 6:00:00");
    }

    [Fact]
    public async Task Resistance_abstaining_since_creation_shows_stopwatch()
    {
        // If the user started abstaining at creation, CurrentDisplay shows stopwatch format.
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-10).UtcDateTime,
        };
        var abstinence = new FakeAbstinenceService();
        abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.Abstaining, today.AddDays(-10).ToUnixTimeMilliseconds(), null);
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

        await vm.LoadAsync(node, DayBoundary.Utc);

        vm.CurrentDisplay.Should().Be("10g 0:00:00");
    }

    [Fact]
    public async Task Resistance_not_started_shows_not_started_string()
    {
        // NotStarted (no abstinence state at all) → CurrentDisplay = Abstinence_NotStarted.
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-10).UtcDateTime,
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), new FakeAbstinenceService(), clock);

        await vm.LoadAsync(node, DayBoundary.Utc);

        vm.CurrentDisplay.Should().Be(Recordum.Presentation.Resources.Strings.Abstinence_NotStarted);
    }

    [Fact]
    public async Task Resistance_caption_is_lapses_and_counts_closed_periods_in_90()
    {
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-100).UtcDateTime,
        };
        var abstinence = new FakeAbstinenceService();
        abstinence.Periods[node.Id] =
        [
            new AbstinencePeriod { HabitNodeId = node.Id, StartedAtMs = today.AddDays(-20).ToUnixTimeMilliseconds(), EndedAtMs = today.AddDays(-10).ToUnixTimeMilliseconds() },
            new AbstinencePeriod { HabitNodeId = node.Id, StartedAtMs = today.AddDays(-50).ToUnixTimeMilliseconds(), EndedAtMs = today.AddDays(-40).ToUnixTimeMilliseconds() },
        ];
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

        await vm.LoadAsync(node, DayBoundary.Utc);

        vm.RunsCaption.Should().Be(Recordum.Presentation.Resources.Strings.Detail_Streak_Lapses90);
        vm.RunsLast90Days.Should().Be(2);
    }

    // ── Task-6 TDD tests: live stopwatch in Detail summary ─────────────────

    [Fact]
    public async Task Resistance_current_display_is_stopwatch_from_start()
    {
        // clock "now" minus 1 day 6h as the abstinence start -> "1g 6:00:00"
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var startMs = new DateTimeOffset(2026, 5, 20, 6, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var abstinence = new FakeAbstinenceService();
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, Name = "x" };
        abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.Abstaining, startMs, null);
        var sut = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.CurrentDisplay.Should().Be("1g 6:00:00");
    }

    [Fact]
    public async Task Resistance_not_abstaining_shows_not_started_and_stops_clock()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var abstinence = new FakeAbstinenceService();
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, Name = "x" };
        abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.NotStarted, null, null);
        var sut = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.CurrentDisplay.Should().Be(Strings.Abstinence_NotStarted);
    }
}
