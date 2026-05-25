using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Aggregators;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration.Aggregators;

public sealed class SchedulingAwareStreakAggregatorTests
{
    private static async Task<(DbFixture fx, SchedulingAwareStreakAggregator aggr, Guid habitId)> SeedAsync(
        ScheduleType st, StreakMode sm, StreakRecovery sr, DateOnly created, int? weeklyTarget = null)
    {
        var clock = new FakeClock(new DateTimeOffset(created.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var fx = new DbFixture(clock);

        // Seed habit directly so CreatedAt reflects FakeClock value via the timestamp interceptor.
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "h",
            });
            await db.SaveChangesAsync();
        }

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        await configs.UpsertAsync(habitId, c =>
        {
            c.ScheduleType = st;
            c.StreakMode = sm;
            c.StreakRecovery = sr;
            if (weeklyTarget is { } wt) c.ScheduleWeeklyTarget = wt;
        });

        var aggr = BuildAggregator(fx, configs);
        return (fx, aggr, habitId);
    }

    private static SchedulingAwareStreakAggregator BuildAggregator(DbFixture fx, IHabitConfigService configs)
    {
        var nodes = new HabitNodeService(
            fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        var completions = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var pauses = new StreakPauseService(
            fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        var scheduler = new ScheduleEvaluator();
        var dailyEval = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        var occEval = new OccurrencesStreakModeEvaluator();
        var bothEval = new BothStreakModeEvaluator(dailyEval, weeklyEval);
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, scheduler,
            dailyEval, weeklyEval, occEval, bothEval, fx.Clock);
    }

    private static async Task SeedCompletionsAsync(DbFixture fx, Guid habitId, DateOnly start, string pattern)
    {
        var svc = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 'X') continue;
            var d = start.AddDays(i).ToDateTime(new TimeOnly(10, 0));
            await svc.RecordAsync(new Completion
            {
                HabitNodeId = habitId,
                CompletedAt = d,
                CompletedAtMs = new DateTimeOffset(d, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                IsRetroactive = false,
            });
        }
    }

    [Fact]
    public async Task Daily_None_continuous_run_returns_one_run()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXXXX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(5, r.Length);
            Assert.Equal(new DateOnly(2026, 4, 1), r.Start);
            Assert.Equal(new DateOnly(2026, 4, 5), r.End);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Daily_None_gap_closes_run_and_starts_new_one()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XX.XX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.Equal(2, runs.Count);
            Assert.Equal(2, runs[0].Length);
            Assert.Equal(StreakBreakReason.Missed, runs[0].BreakReason);
            Assert.Equal(2, runs[1].Length);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Empty_history_returns_empty()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.Empty(runs);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task WeeklyMode_TargetMet_CountsOneWeek()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.WeeklyX, StreakMode.Weekly, StreakRecovery.None, new DateOnly(2026, 4, 6) /* Monday */,
            weeklyTarget: 3);
        try
        {
            // 3 completions Mon/Wed/Fri
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 6), "X.X.X..");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 12) /* Sunday */);
            var r = Assert.Single(runs);
            Assert.Equal(1, r.Length); // 1 week
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task WeeklyMode_TargetMissed_Breaks()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.WeeklyX, StreakMode.Weekly, StreakRecovery.None, new DateOnly(2026, 4, 6),
            weeklyTarget: 3);
        try
        {
            // only 2 completions — target unmet
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 6), "X.X....");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 12));
            Assert.Empty(runs); // no week ever counted
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task OccurrencesMode_GapDoesNotBreak()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Occurrences, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "X..X.X..");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(3, r.Length);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task BothMode_DailyMissBreaksEvenIfWeeklyMet()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Both, StreakRecovery.None, new DateOnly(2026, 4, 6),
            weeklyTarget: 3);
        try
        {
            // Daily but with a Tuesday gap
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 6), "X.XXXXX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 12));
            Assert.Equal(2, runs.Count);
            Assert.Equal(StreakBreakReason.Missed, runs[0].BreakReason);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Group_returns_empty()
    {
        var fx = new DbFixture();
        try
        {
            var groupId = Guid.NewGuid();
            await using (var db = await fx.Factory.CreateDbContextAsync())
            {
                db.HabitNodes.Add(new HabitNode
                {
                    Id = groupId,
                    NodeType = NodeType.Group,
                    Name = "g",
                });
                await db.SaveChangesAsync();
            }

            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
            var aggr = BuildAggregator(fx, configs);
            var runs = await aggr.GetHistoryAsync(groupId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.Empty(runs);
        }
        finally { await fx.DisposeAsync(); }
    }
}
