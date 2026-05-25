using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Aggregators;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Services.Policies;
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
        var noneRec = new NoneStreakRecoveryPolicy();
        var graceRec = new GraceDayStreakRecoveryPolicy();
        var manualRec = new ManualStreakRecoveryPolicy();
        var retroRec = new RetroactiveStreakRecoveryPolicy();
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, scheduler,
            dailyEval, weeklyEval, occEval, bothEval,
            noneRec, graceRec, manualRec, retroRec, fx.Clock);
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
    public async Task GraceDay_AbsorbsOneMissPerWindow()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.GraceDay, new DateOnly(2026, 4, 1));
        try
        {
            // 10 days: 1 miss on day 3 should be absorbed.
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XX.XXXXXXX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(9, r.Length); // 9 completed days
            Assert.Equal(1, r.RecoveriesUsed);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task GraceDay_SecondMissInWindowBreaks()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.GraceDay, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "X.X.XXXX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.True(runs.Count >= 2);
            Assert.Equal(StreakBreakReason.Missed, runs[0].BreakReason);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Retroactive_BackfillReopensRun()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.Retroactive, new DateOnly(2026, 4, 1));
        try
        {
            // 4 completions; day 3 is retroactive backfill — counts even though it's "added later"
            var completions = new CompletionService(
                fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
            DateTime At(int i) => new DateOnly(2026, 4, 1).AddDays(i).ToDateTime(new TimeOnly(10, 0));
            long Ms(DateTime t) => new DateTimeOffset(t, TimeSpan.Zero).ToUnixTimeMilliseconds();
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(0), CompletedAtMs = Ms(At(0)), IsRetroactive = false });
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(1), CompletedAtMs = Ms(At(1)), IsRetroactive = false });
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(2), CompletedAtMs = Ms(At(2)), IsRetroactive = true });
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(3), CompletedAtMs = Ms(At(3)), IsRetroactive = false });

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(4, r.Length);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Retroactive_BackfillIgnoredWhenRecoveryNone()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            var completions = new CompletionService(
                fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
            DateTime At(int i) => new DateOnly(2026, 4, 1).AddDays(i).ToDateTime(new TimeOnly(10, 0));
            long Ms(DateTime t) => new DateTimeOffset(t, TimeSpan.Zero).ToUnixTimeMilliseconds();
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(0), CompletedAtMs = Ms(At(0)), IsRetroactive = false });
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(1), CompletedAtMs = Ms(At(1)), IsRetroactive = false });
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(2), CompletedAtMs = Ms(At(2)), IsRetroactive = true }); // ignored
            await completions.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = At(3), CompletedAtMs = Ms(At(3)), IsRetroactive = false });

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.Equal(2, runs.Count); // broke at day 3, restarted day 4
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Manual_StreakBrokenAt_ClosesAtCorrectDay()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.Manual, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXXXX");

            // Advance clock to Apr 6 BEFORE MarkBrokenAsync so StreakBrokenAt is persisted as Apr 6.
            ((FakeClock)fx.Clock).Set(new DateTimeOffset(new DateOnly(2026, 4, 6).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
            var streaks = new StreakService(configs, fx.Clock);
            await streaks.MarkBrokenAsync(habitId);

            var aggr2 = BuildAggregator(fx, configs);
            var runs = await aggr2.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(5, r.Length);
            Assert.Equal(StreakBreakReason.Manual, r.BreakReason);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Manual_WithoutBrokenAt_RunStaysOpenAcrossMisses()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.Manual, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XX..XX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(4, r.Length);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Pause_DuringRun_ExtendsButDoesNotIncrement()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            // 5 done, 3-day gap, 5 done — total 13 days.
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXXXX...XXXXX");
            var pauses = new StreakPauseService(
                fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
            await pauses.StartAsync(habitId, new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 8), null);

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(10, r.Length); // 5 + 5 completion days
            Assert.Equal(new DateOnly(2026, 4, 13), r.End);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task Pause_OverlapsMiss_DoesNotConsumeRecovery()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.GraceDay, new DateOnly(2026, 4, 1));
        try
        {
            // 5 done, 1 missing but paused, 5 done — grace day NOT consumed because pause absorbed the gap.
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXXXX.XXXXX");
            var pauses = new StreakPauseService(
                fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
            await pauses.StartAsync(habitId, new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 6), null);

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(10, r.Length);
            Assert.Equal(0, r.RecoveriesUsed);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task ConfigChange_MidRun_DoesNotBreakRun()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXXXXXXXXX");

            // Advance clock and switch ScheduleType mid-run.
            ((FakeClock)fx.Clock).Set(new DateTimeOffset(
                new DateOnly(2026, 4, 6).ToDateTime(new TimeOnly(0, 0)), TimeSpan.Zero));
            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
            await configs.UpsertAsync(habitId, c =>
            {
                c.ScheduleType = ScheduleType.WeeklyX;
                c.ScheduleWeeklyTarget = 3;
            });

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.NotEmpty(runs);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task IsCurrent_True_When_Run_Ends_Today()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 26));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 26), "XXXXX");
            ((FakeClock)fx.Clock).Set(new DateTimeOffset(
                new DateOnly(2026, 4, 30).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 26), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.True(r.IsCurrent);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task IsCurrent_False_When_Run_Ended_Three_Days_Ago()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXXXX....");
            ((FakeClock)fx.Clock).Set(new DateTimeOffset(
                new DateOnly(2026, 4, 10).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 10));
            Assert.False(runs[0].IsCurrent);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task ResetTimeCascade_AppliesViaHabitOverride()
    {
        var (fx, aggr, habitId) = await SeedAsync(
            ScheduleType.Daily, StreakMode.Daily, StreakRecovery.None, new DateOnly(2026, 4, 1));
        try
        {
            // Set the habit's reset time directly via EF (no service ctor change needed).
            await using (var db = await fx.Factory.CreateDbContextAsync())
            {
                var node = await db.HabitNodes.FirstAsync(n => n.Id == habitId);
                node.ResetTime = new TimeOnly(4, 0);
                await db.SaveChangesAsync();
            }
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXX");

            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            var r = Assert.Single(runs);
            Assert.Equal(3, r.Length);
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
