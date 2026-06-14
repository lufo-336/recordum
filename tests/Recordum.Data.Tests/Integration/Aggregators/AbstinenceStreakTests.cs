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

public sealed class AbstinenceStreakTests
{
    private static SchedulingAwareStreakAggregator BuildAggregator(DbFixture fx)
    {
        var nodes = new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var pauses = new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        var dailyEval = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, new ScheduleEvaluator(),
            dailyEval, weeklyEval, new OccurrencesStreakModeEvaluator(), new BothStreakModeEvaluator(dailyEval, weeklyEval),
            new NoneStreakRecoveryPolicy(), new GraceDayStreakRecoveryPolicy(),
            new ManualStreakRecoveryPolicy(), new RetroactiveStreakRecoveryPolicy(), fx.Clock);
    }

    // created = habit CreatedAt; clock is left AT created so the interceptor stamps CreatedAt correctly.
    private static async Task<(DbFixture fx, FakeClock clock, Guid habitId)> SeedResistanceAsync(DateOnly created)
    {
        var clock = new FakeClock(new DateTimeOffset(created.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "no smoking" });
        await db.SaveChangesAsync();
        return (fx, clock, habitId);
    }

    private static async Task LapseOnAsync(DbFixture fx, Guid habitId, DateOnly day)
    {
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var dt = day.ToDateTime(new TimeOnly(10, 0));
        await svc.RecordAsync(new Completion
        {
            HabitNodeId = habitId,
            CompletedAt = dt,
            CompletedAtMs = new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            IsRetroactive = false,
        });
    }

    [Fact]
    public async Task Never_lapsed_is_one_current_run_from_creation_to_today()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().ContainSingle();
        runs[0].IsCurrent.Should().BeTrue();
        runs[0].Start.Should().Be(created);
        runs[0].End.Should().Be(today);
    }

    [Fact]
    public async Task A_lapse_closes_the_clean_run_and_a_new_run_starts_the_next_day()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        await LapseOnAsync(fx, id, today.AddDays(-10));
        clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().HaveCount(2);
        runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
        runs[0].IsCurrent.Should().BeFalse();
        runs[1].IsCurrent.Should().BeTrue();
        runs[1].Start.Should().Be(today.AddDays(-9)); // day after the lapse
        runs[1].End.Should().Be(today);
    }

    [Fact]
    public async Task A_lapse_today_leaves_no_current_clean_run()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        await LapseOnAsync(fx, id, today);
        clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().ContainSingle();
        runs[0].IsCurrent.Should().BeFalse();
        runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
        runs[0].End.Should().Be(today.AddDays(-1));
    }

    [Fact]
    public async Task Consecutive_lapses_leave_no_clean_run_between_them()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10);
        var (fx, clock, id) = await SeedResistanceAsync(created);
        await LapseOnAsync(fx, id, today.AddDays(-5));
        await LapseOnAsync(fx, id, today.AddDays(-4));
        clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().HaveCount(2); // [created..-6] closed, then [-3..today] current; nothing between the two lapses
        runs[1].Start.Should().Be(today.AddDays(-3));
        runs[1].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Paused_days_are_frozen_not_counted_and_do_not_break_the_run()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10); // 11 clean days inclusive
        var (fx, clock, id) = await SeedResistanceAsync(created);
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
}
