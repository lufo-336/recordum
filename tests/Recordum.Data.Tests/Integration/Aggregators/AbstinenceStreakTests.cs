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

namespace Recordum.Data.Tests.Integration.Aggregators;

// AbstinenceStreakTests — updated for Task 4 (period-based model).
// The aggregator now derives Resistance runs from abstinence_period rows, NOT completion heatmaps.
// Tests that previously seeded completions via LapseOnAsync now seed periods directly.
// Semantic note: period.EndedAtMs day is INCLUSIVE in the run (it is the last clean/boundary day).
public sealed class AbstinenceStreakTests
{
    private static SchedulingAwareStreakAggregator BuildAggregator(DbFixture fx)
    {
        var nodes = new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
        var pauses = new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);
        var dailyEval = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, new ScheduleEvaluator(),
            dailyEval, weeklyEval, new OccurrencesStreakModeEvaluator(), new BothStreakModeEvaluator(dailyEval, weeklyEval),
            new NoneStreakRecoveryPolicy(), new GraceDayStreakRecoveryPolicy(),
            new ManualStreakRecoveryPolicy(), new RetroactiveStreakRecoveryPolicy(), abstinence, fx.Clock, fx.ResetTimes);
    }

    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    // created = habit CreatedAt; clock is left AT created so the interceptor stamps CreatedAt correctly.
    private static async Task<(DbFixture fx, FakeClock clock, Guid habitId)> SeedResistanceAsync(
        DateOnly created, TimeZoneInfo? tz = null)
    {
        var clock = new FakeClock(new DateTimeOffset(created.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), tz);
        var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "no smoking" });
        await db.SaveChangesAsync();
        return (fx, clock, habitId);
    }

    private static long ToMs(DateOnly d) =>
        new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>Seeds an abstinence_period row directly (bypasses AbstinenceService guards).</summary>
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

    /// <summary>Seeds an abstinence_period row at an exact instant (not a day-rounded one).</summary>
    private static async Task SeedPeriodAtAsync(
        DbFixture fx, Guid habitId, DateTimeOffset startedAt, DateTimeOffset? endedAt = null)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            StartedAtMs = startedAt.ToUnixTimeMilliseconds(),
            EndedAtMs = endedAt?.ToUnixTimeMilliseconds(),
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task An_abstinence_period_started_after_local_midnight_starts_on_the_new_day()
    {
        // StartedAtMs = 2026-08-18T22:30Z → il periodo comincia il 19 locale, non il 18.
        var (fx, clock, id) = await SeedResistanceAsync(new DateOnly(2026, 8, 17), Plus2);
        try
        {
            await SeedPeriodAtAsync(fx, id, new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero));
            clock.Set(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 19));

            runs.Should().ContainSingle().Which.Start.Should().Be(new DateOnly(2026, 8, 19));
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task An_open_period_runs_to_the_local_today()
    {
        // Orologio alle 22:30Z del 19 = 20 alle 00:30 locali: il periodo aperto arriva al 20.
        // In UTC "oggi" era ancora il 19 e il contatore dei giorni puliti restava indietro di uno.
        var (fx, clock, id) = await SeedResistanceAsync(new DateOnly(2026, 8, 15), Plus2);
        try
        {
            await SeedPeriodAtAsync(fx, id, new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero));
            clock.Set(new DateTimeOffset(2026, 8, 19, 22, 30, 0, TimeSpan.Zero));

            var runs = await BuildAggregator(fx).GetHistoryAsync(
                id, new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 20));

            var run = runs.Should().ContainSingle().Subject;
            run.End.Should().Be(new DateOnly(2026, 8, 20));
            run.Length.Should().Be(5); // 16, 17, 18, 19, 20
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task A_period_ended_after_local_midnight_ends_on_the_new_day()
    {
        // EndedAtMs = 2026-08-18T22:30Z = 19 alle 00:30 locali: la ricaduta appartiene al 19.
        // In UTC il periodo si chiudeva il 18 e il 19 spariva dalla serie.
        var (fx, clock, id) = await SeedResistanceAsync(new DateOnly(2026, 8, 15), Plus2);
        try
        {
            await SeedPeriodAtAsync(fx, id,
                new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero));
            clock.Set(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero));

            var runs = await BuildAggregator(fx).GetHistoryAsync(
                id, new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 20));

            var run = runs.Should().ContainSingle().Subject;
            run.End.Should().Be(new DateOnly(2026, 8, 19));
            run.Length.Should().Be(4); // 16, 17, 18, 19
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Never_lapsed_is_one_current_run_from_period_start_to_today()
    {
        // One open period started on the creation day: produces a single current run from created to today.
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created); // open period
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeTrue();
            runs[0].Start.Should().Be(created);
            runs[0].End.Should().Be(today);
            runs[0].Length.Should().Be(11); // created=today-10 → 11 inclusive clean days
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task A_closed_period_produces_a_historic_run_and_an_open_period_continues_the_streak()
    {
        // Two periods: period1 closed (end=today-10), period2 open from today-9.
        // period1.end (today-10) is the INCLUSIVE boundary day of that run.
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created, today.AddDays(-10)); // closed — period end day inclusive
            await SeedPeriodAsync(fx, id, today.AddDays(-9));            // new open period starts next day
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().HaveCount(2);
            runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
            runs[0].IsCurrent.Should().BeFalse();
            runs[0].End.Should().Be(today.AddDays(-10)); // EndedAtMs day is inclusive
            runs[0].Length.Should().Be(21);              // created=today-30 to today-10 inclusive = 21 days
            runs[1].IsCurrent.Should().BeTrue();
            runs[1].Start.Should().Be(today.AddDays(-9));
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task A_closed_period_ending_today_leaves_no_current_clean_run()
    {
        // Period closed with EndedAtMs = today → run is historic (IsCurrent=false), End=today.
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created, today); // closed today
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeFalse();
            runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
            runs[0].End.Should().Be(today); // End = EndedAtMs day (inclusive)
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Gap_between_periods_leaves_no_clean_run_in_between()
    {
        // period1 ends today-5; period2 starts today-3.
        // today-4 is a gap day ("relapsed, not resumed") — not in any run.
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created, today.AddDays(-5)); // closed
            await SeedPeriodAsync(fx, id, today.AddDays(-3));           // open, gap day today-4 excluded
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().HaveCount(2); // period1 run + period2 run; gap not a run
            runs[0].Start.Should().Be(created);
            runs[0].End.Should().Be(today.AddDays(-5)); // period end day inclusive
            runs[0].Length.Should().Be(6);              // created=today-10 to today-5 inclusive = 6 days
            runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
            runs[1].Start.Should().Be(today.AddDays(-3));
            runs[1].IsCurrent.Should().BeTrue();
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Paused_days_are_frozen_not_counted_and_do_not_break_the_run()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10); // 11 clean days inclusive
        var (fx, clock, id) = await SeedResistanceAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created); // open period covers created..today
            await using (var db = await fx.Factory.CreateDbContextAsync())
            {
                db.StreakPauses.Add(new StreakPause
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = id,
                    PauseFrom = today.AddDays(-6),
                    PauseTo = today.AddDays(-4), // 3 paused days
                });
                await db.SaveChangesAsync();
            }
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, created, today);

            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeTrue();
            runs[0].Length.Should().Be(8); // 11 calendar days - 3 paused
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Current_run_length_counts_from_period_start_not_the_query_window()
    {
        // CR11: querying with a fromDate AFTER period2's start must not truncate the current run's
        // length. period2 started today-19 but query starts today-5 → length still 20 (full period).
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        try
        {
            await SeedPeriodAsync(fx, id, created, today.AddDays(-20));  // closed period (filtered out by query window)
            await SeedPeriodAsync(fx, id, today.AddDays(-19));            // open period — current run
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, today.AddDays(-5), today);

            // Only the current run intersects the window; the closed pre-lapse period is filtered out.
            runs.Should().ContainSingle();
            runs[0].IsCurrent.Should().BeTrue();
            runs[0].Start.Should().Be(today.AddDays(-19)); // period start = real run start
            runs[0].End.Should().Be(today);
            runs[0].Length.Should().Be(20); // days -19..0 inclusive = 20, NOT 6 (the window size)
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Habit_with_no_period_started_yet_has_no_runs()
    {
        // Before the user presses "Inizio" (Start), no abstinence_period rows exist → no streak runs.
        // Replaces the old "A_lapse_on_the_creation_day_produces_no_run" test: the new model no longer
        // derives lapses from completions; without periods there is simply nothing to aggregate.
        var today = new DateOnly(2026, 6, 1);
        var (fx, clock, id) = await SeedResistanceAsync(today);
        try
        {
            clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var aggr = BuildAggregator(fx);

            var runs = await aggr.GetHistoryAsync(id, today, today);

            runs.Should().BeEmpty();
        }
        finally { await fx.DisposeAsync(); }
    }
}
