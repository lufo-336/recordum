using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Aggregators;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Services.Policies;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration.Evaluators;

/// <summary>Tests for the Resistance (abstinence) branch of SchedulingAwareStreakAggregator
/// after Task 4: runs are derived from abstinence_period rows, not completion heatmaps.</summary>
public sealed class AbstinenceAggregatorTests
{
    // ── helpers ─────────────────────────────────────────────────────────────────

    private static SchedulingAwareStreakAggregator BuildAggregator(DbFixture fx)
    {
        var nodes      = new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance, new RecordingEventBus());
        var configs    = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
        var pauses     = new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);
        var dailyEval  = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, new ScheduleEvaluator(),
            dailyEval, weeklyEval,
            new OccurrencesStreakModeEvaluator(),
            new BothStreakModeEvaluator(dailyEval, weeklyEval),
            new NoneStreakRecoveryPolicy(),
            new GraceDayStreakRecoveryPolicy(),
            new ManualStreakRecoveryPolicy(),
            new RetroactiveStreakRecoveryPolicy(),
            abstinence,
            fx.Clock,
            fx.ResetTimes);
    }

    private static async Task<(DbFixture fx, FakeClock clock, Guid habitId)> SeedHabitAsync(DateOnly created)
    {
        var clock = new FakeClock(new DateTimeOffset(created.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode
        {
            Id = habitId,
            NodeType = NodeType.Habit,
            HabitType = HabitType.Resistance,
            Name = "no-smoking",
        });
        await db.SaveChangesAsync();
        return (fx, clock, habitId);
    }

    private static long ToMs(DateOnly d) =>
        new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>Seeds an abstinence_period row directly without going through AbstinenceService state-guards.</summary>
    private static async Task SeedPeriodAsync(DbFixture fx, Guid habitId, DateOnly start, DateOnly? end = null)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            StartedAtMs = ToMs(start),
            EndedAtMs = end.HasValue ? ToMs(end.Value) : null,
        });
        await db.SaveChangesAsync();
    }

    // ── tests ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_period_is_the_current_clean_run()
    {
        var today = new DateOnly(2026, 6, 10);
        var start = today.AddDays(-7);
        var (fx, clock, id) = await SeedHabitAsync(start);
        try
        {
            await SeedPeriodAsync(fx, id, start); // open period: no EndedAtMs
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, start, today);

            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeTrue();
            runs[0].Start.Should().Be(start);
            runs[0].End.Should().Be(today);
            runs[0].Length.Should().Be(8); // start(today-7)..today inclusive = 8 days
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Closed_period_is_a_historic_run_with_Lapse()
    {
        var today = new DateOnly(2026, 6, 10);
        var start = today.AddDays(-10);
        var end   = today.AddDays(-3);
        var (fx, clock, id) = await SeedHabitAsync(start);
        try
        {
            await SeedPeriodAsync(fx, id, start, end); // closed period
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, start, today);

            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeFalse();
            runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
            runs[0].Start.Should().Be(start);
            runs[0].End.Should().Be(end);
            runs[0].Length.Should().Be(8); // today-10..today-3 inclusive = 8 days
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Gap_between_lapse_and_restart_is_not_counted()
    {
        // period1 ends today-10; period2 starts today-5.
        // Gap [today-9..today-6] = 4 days that are "relapsed, not resumed" and must NOT appear in any run.
        var today   = new DateOnly(2026, 6, 10);
        var created = today.AddDays(-20);
        var (fx, clock, id) = await SeedHabitAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created, today.AddDays(-10)); // closed
            await SeedPeriodAsync(fx, id, today.AddDays(-5));            // open
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().HaveCount(2);
            runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
            runs[0].End.Should().Be(today.AddDays(-10));
            runs[0].Length.Should().Be(11); // created(today-20)..today-10 inclusive = 11 days
            runs[1].IsCurrent.Should().BeTrue();
            runs[1].Start.Should().Be(today.AddDays(-5));
            runs[1].Length.Should().Be(6);  // today-5..today inclusive = 6 days
            // Total clean days = 17, which is less than the full 21-day range (4 gap days excluded).
            (runs[0].Length + runs[1].Length).Should().Be(17);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task No_periods_yields_no_runs()
    {
        // Before the user presses "Inizio" no abstinence_period rows exist → aggregator returns empty.
        var today   = new DateOnly(2026, 6, 10);
        var created = today.AddDays(-5);
        var (fx, clock, id) = await SeedHabitAsync(created);
        try
        {
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().BeEmpty();
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Pause_inside_a_period_freezes_length()
    {
        // 11 calendar days in the open period; 3 are paused → Length = 8.
        var today = new DateOnly(2026, 6, 10);
        var start = today.AddDays(-10);
        var (fx, clock, id) = await SeedHabitAsync(start);
        try
        {
            await SeedPeriodAsync(fx, id, start); // open period from start
            await using (var db = await fx.Factory.CreateDbContextAsync())
            {
                db.StreakPauses.Add(new StreakPause
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = id,
                    PauseFrom = today.AddDays(-6),
                    PauseTo   = today.AddDays(-4), // 3 paused days: today-6, today-5, today-4
                });
                await db.SaveChangesAsync();
            }
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, start, today);

            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeTrue();
            runs[0].Length.Should().Be(8); // 11 calendar days - 3 paused = 8
        }
        finally { await fx.DisposeAsync(); }
    }
}
