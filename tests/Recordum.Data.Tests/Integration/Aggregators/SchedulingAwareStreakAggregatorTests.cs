using FluentAssertions;
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
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

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

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
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
            fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance, new RecordingEventBus());
        var completions = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
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
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, scheduler,
            dailyEval, weeklyEval, occEval, bothEval,
            noneRec, graceRec, manualRec, retroRec, abstinence, fx.Clock, fx.ResetTimes);
    }

    /// <summary>Seeds a daily habit (default schedule/mode/recovery) with CreatedAt = createdUtc,
    /// restoring the fixture's clock afterward so it doesn't leak into the caller's "now".</summary>
    private static async Task<Guid> SeedDailyHabitAsync(DbFixture fx, DateTimeOffset createdUtc)
    {
        var clock = (FakeClock)fx.Clock;
        var original = clock.UtcNow;
        clock.Set(createdUtc);

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

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        await configs.UpsertAsync(habitId, c =>
        {
            c.ScheduleType = ScheduleType.Daily;
            c.StreakMode = StreakMode.Daily;
            c.StreakRecovery = StreakRecovery.None;
        });

        clock.Set(original);
        return habitId;
    }

    /// <summary>Records a single completion at the given instant (not via a day pattern).</summary>
    private static async Task RecordAtAsync(DbFixture fx, Guid habitId, DateTimeOffset at)
    {
        var svc = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
        await svc.RecordAsync(new Completion
        {
            HabitNodeId = habitId,
            CompletedAt = at.UtcDateTime,
            CompletedAtMs = at.ToUnixTimeMilliseconds(),
            IsRetroactive = false,
        });
    }

    private static async Task SeedCompletionsAsync(DbFixture fx, Guid habitId, DateOnly start, string pattern)
    {
        var svc = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
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
    public async Task A_completion_just_after_local_midnight_counts_for_the_new_day()
    {
        // Habit giornaliero creato il 17. Registrazione unica alle 22:30Z del 18 = 19 alle 00:30 locali.
        // In UTC quella registrazione cadeva il 18 e il 19 risultava mancato: la serie si spezzava.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedDailyHabitAsync(fx, createdUtc: new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero));

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var runs = await BuildAggregator(fx, configs).GetHistoryAsync(
            habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 19));

        runs.Should().ContainSingle().Which.End.Should().Be(new DateOnly(2026, 8, 19));
    }

    [Fact]
    public async Task With_a_04_00_reset_a_late_night_completion_still_counts_for_the_day_before()
    {
        // Chi si corica tardi: registra all'01:30 locale del 19 (23:30Z del 18) con reset 04:00 → è ancora il 18.
        // È il caso d'uso che rende reset_time una promessa, non una decorazione.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedDailyHabitAsync(fx, createdUtc: new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var node = await db.HabitNodes.FirstAsync(n => n.Id == habitId);
            node.ResetTime = new TimeOnly(4, 0);
            await db.SaveChangesAsync();
        }
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 23, 30, 0, TimeSpan.Zero));

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var runs = await BuildAggregator(fx, configs).GetHistoryAsync(
            habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 19));

        runs.Should().ContainSingle().Which.End.Should().Be(new DateOnly(2026, 8, 18));
    }

    [Fact]
    public async Task A_habit_created_after_local_midnight_does_not_count_the_previous_local_day()
    {
        // CreatedAt = 2026-08-17T22:30Z = 18 alle 00:30 locali. Il 17 è prima della nascita
        // dell'abitudine, quindi non è programmato e la sua registrazione non apre la serie.
        // In UTC la nascita cadeva il 17 e quel giorno entrava nella serie: Start 17, lunghezza 3.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedDailyHabitAsync(fx, createdUtc: new DateTimeOffset(2026, 8, 17, 22, 30, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var runs = await BuildAggregator(fx, configs).GetHistoryAsync(
            habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 19));

        var run = runs.Should().ContainSingle().Subject;
        run.Start.Should().Be(new DateOnly(2026, 8, 18));
        run.Length.Should().Be(2);
    }

    [Fact]
    public async Task A_run_is_not_current_when_the_local_today_has_already_moved_on()
    {
        // Serie chiusa il 18. Orologio alle 22:30Z del 19 = 20 alle 00:30 locali: "oggi" è il 20,
        // quindi il 18 è vecchio di due giorni e la serie non è più quella corrente.
        // In UTC "oggi" era ancora il 19 e la serie risultava corrente.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 22, 30, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedDailyHabitAsync(fx, createdUtc: new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero));

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var runs = await BuildAggregator(fx, configs).GetHistoryAsync(
            habitId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 18));

        runs.Should().ContainSingle().Which.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task A_manual_break_marked_after_local_midnight_breaks_on_the_new_day()
    {
        // StreakBrokenAt = 2026-08-18T22:30Z = 19 alle 00:30 locali. Il 18 (mancato) non spezza
        // ancora, quindi il 19 riprende la stessa serie: una sola run 16→19 lunga 3.
        // In UTC la rottura cadeva il 18: la serie si spezzava lì e il 19 ne apriva una seconda.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedDailyHabitAsync(fx, createdUtc: new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));

        // La config va scritta all'istante di creazione: un upsert successivo lascerebbe uno snapshot
        // storico che copre i giorni misurati, e Project() non porta StreakBrokenAt (escluso dallo
        // storico) — la rottura manuale sparirebbe proprio dai giorni che il test guarda.
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        clock.Set(new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));
        await configs.UpsertAsync(habitId, c =>
        {
            c.StreakRecovery = StreakRecovery.Manual;
            c.StreakBrokenAt = new DateTime(2026, 8, 18, 22, 30, 0, DateTimeKind.Utc);
        });
        clock.Set(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero));

        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero));
        await RecordAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        var runs = await BuildAggregator(fx, configs).GetHistoryAsync(
            habitId, new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 19));

        var run = runs.Should().ContainSingle().Subject;
        run.Start.Should().Be(new DateOnly(2026, 8, 16));
        run.End.Should().Be(new DateOnly(2026, 8, 19));
        run.Length.Should().Be(3);
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
                fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
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
                fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
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
            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
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
            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
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

            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
            var aggr = BuildAggregator(fx, configs);
            var runs = await aggr.GetHistoryAsync(groupId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
            Assert.Empty(runs);
        }
        finally { await fx.DisposeAsync(); }
    }

    [Fact]
    public async Task NoConfig_FallsBackToDefaultDaily()
    {
        // F0-2a: A habit with no HabitConfig row should be treated as a default daily habit.
        var clock = new FakeClock(new DateTimeOffset(new DateOnly(2026, 4, 1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var fx = new DbFixture(clock);

        try
        {
            // Create habit WITHOUT calling configs.UpsertAsync (no config row).
            var habitId = Guid.NewGuid();
            await using (var db = await fx.Factory.CreateDbContextAsync())
            {
                db.HabitNodes.Add(new HabitNode
                {
                    Id = habitId,
                    NodeType = NodeType.Habit,
                    HabitType = HabitType.Action,
                    Name = "h_no_config",
                });
                await db.SaveChangesAsync();
            }

            // Seed 3 completions on consecutive days: Apr 1, 2, 3.
            await SeedCompletionsAsync(fx, habitId, new DateOnly(2026, 4, 1), "XXX");

            // Set clock to Apr 3 (today = last completion day).
            ((FakeClock)fx.Clock).Set(new DateTimeOffset(new DateOnly(2026, 4, 3).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

            var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
            var aggr = BuildAggregator(fx, configs);

            // toDate matches "today" (Apr 3) — otherwise Apr 4 reads as a missed scheduled day
            // (StreakRecovery.None absorbs nothing) and closes the run via the Missed branch,
            // which hardcodes IsCurrent=false, instead of exercising the still-open/current branch.
            var runs = await aggr.GetHistoryAsync(habitId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3));
            var r = Assert.Single(runs);
            Assert.Equal(3, r.Length);
            Assert.Equal(new DateOnly(2026, 4, 1), r.Start);
            Assert.Equal(new DateOnly(2026, 4, 3), r.End);
            Assert.True(r.IsCurrent);
            Assert.Null(r.BreakReason);
        }
        finally { await fx.DisposeAsync(); }
    }
}
