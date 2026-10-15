using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Data.Services;
using Recordum.Data.Services.Aggregators;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Services.Policies;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Export;
using Xunit;

namespace Recordum.Data.Tests.Integration;

/// <summary>
/// La prova che il punto di verità è uno. Ogni superficie che dice "questo è successo il giorno X"
/// deve dire lo stesso X sugli stessi dati: se una diverge, R9 è tornato.
/// <para>Il caso è quello della spec §7: una registrazione alle 00:30 locali. Con reset 00:00 è di
/// oggi, con reset 04:00 è ancora di ieri — e le cinque letture devono seguire il reset, tutte.</para>
/// </summary>
public class DayBoundaryParityTests
{
    private static readonly TimeZoneInfo Plus2 = TestTimeZones.Fixed(2);

    /// <summary>2026-08-18T22:30Z = 19 agosto, 00:30 locali in fuso +2.</summary>
    private static readonly DateTimeOffset HalfPastMidnight = new(2026, 8, 18, 22, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "2026-08-19")]   // reset 00:00: le 00:30 locali del 19 sono del 19
    [InlineData(4, "2026-08-18")]   // reset 04:00: le stesse 00:30 locali sono ancora del 18
    public async Task All_five_surfaces_agree_on_the_day_of_one_completion(int resetHour, string expected)
    {
        var expectedDay = DateOnly.Parse(expected, CultureInfo.InvariantCulture);
        var otherDay = expectedDay == new DateOnly(2026, 8, 19)
            ? new DateOnly(2026, 8, 18)
            : new DateOnly(2026, 8, 19);
        await using var fx = new DbFixture(
            clock: new FakeClock(new DateTimeOffset(2026, 8, 19, 8, 0, 0, TimeSpan.Zero), Plus2));
        var habitId = await SeedDailyActionAsync(
            fx, new TimeOnly(resetHour, 0), new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero));
        await RecordAsync(fx, habitId, HalfPastMidnight);

        var from = new DateOnly(2026, 8, 15);
        var to = new DateOnly(2026, 8, 20);

        // 1. heatmap
        var heatmap = await Completions(fx).GetHeatmapAsync(habitId, from, to);
        heatmap.Should().ContainSingle().Which.Date.Should().Be(expectedDay);

        // 2. calendario (magnitudine giornaliera)
        var mags = await Completions(fx).GetDailyMagnitudeAsync(habitId, 2026, 8);
        mags.Should().ContainSingle().Which.Date.Should().Be(expectedDay);

        // 3. streak
        var runs = await Aggregator(fx).GetHistoryAsync(habitId, from, to);
        runs.Should().ContainSingle();
        runs[0].Start.Should().Be(expectedDay);
        runs[0].End.Should().Be(expectedDay);

        // 4. andamento
        var trend = await Trends(fx).GetTrendAsync(habitId, from, to, TrendGranularity.Daily);
        trend.Where(p => p.Count > 0).Should().ContainSingle().Which.Bucket.Should().Be(expectedDay);

        // 5. export: la finestra di un giorno solo pesca la registrazione dal giorno giusto, e
        //    dall'altro no. È la stessa domanda posta all'unica superficie che non espone un giorno.
        (await ExportedRowsAsync(fx, habitId, expectedDay)).Should().Be(1);
        (await ExportedRowsAsync(fx, habitId, otherDay)).Should().Be(0);
    }

    /// <summary>Il 29 marzo 2026 dura 23 ore. Cinque registrazioni, una per giorno, restano cinque
    /// giorni distinti in tutte le letture: nessun doppione, nessun buco.</summary>
    [Fact]
    public async Task The_surfaces_also_agree_across_a_daylight_saving_change()
    {
        var rome = TestTimeZones.RomeLike;
        await using var fx = new DbFixture(
            clock: new FakeClock(new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero), rome));
        var habitId = await SeedDailyActionAsync(
            fx, new TimeOnly(4, 0), new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.Zero));

        var days = Enumerable.Range(0, 5).Select(i => new DateOnly(2026, 3, 27).AddDays(i)).ToList();
        var boundary = await fx.ResetTimes.GetDayBoundaryAsync(habitId);
        foreach (var d in days) await RecordAsync(fx, habitId, boundary.NoonOf(d));

        var from = new DateOnly(2026, 3, 25);
        var to = new DateOnly(2026, 4, 1);

        var heatmap = await Completions(fx).GetHeatmapAsync(habitId, from, to);
        heatmap.Select(c => c.Date).OrderBy(d => d).Should().Equal(days);

        var trend = await Trends(fx).GetTrendAsync(habitId, from, to, TrendGranularity.Daily);
        trend.Where(p => p.Count > 0).Select(p => p.Bucket).OrderBy(d => d).Should().Equal(days);

        var runs = await Aggregator(fx).GetHistoryAsync(habitId, from, to);
        runs.Should().ContainSingle().Which.Length.Should().Be(5);
    }

    // ---- helpers: i servizi veri, sullo stesso DB --------------------------------

    private static CompletionService Completions(DbFixture fx) => new(
        fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);

    private static TrendQueryService Trends(DbFixture fx) => new(
        fx.Factory,
        new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus()),
        fx.ResetTimes);

    private static SchedulingAwareStreakAggregator Aggregator(DbFixture fx)
    {
        var bus = new RecordingEventBus();
        var dailyEval = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        return new SchedulingAwareStreakAggregator(
            new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance, new RecordingEventBus()),
            new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus()),
            Completions(fx),
            new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance),
            new ScheduleEvaluator(), dailyEval, weeklyEval,
            new OccurrencesStreakModeEvaluator(), new BothStreakModeEvaluator(dailyEval, weeklyEval),
            new NoneStreakRecoveryPolicy(), new GraceDayStreakRecoveryPolicy(),
            new ManualStreakRecoveryPolicy(), new RetroactiveStreakRecoveryPolicy(),
            new AbstinenceService(fx.Factory, fx.Clock, bus, fx.ResetTimes), fx.Clock, fx.ResetTimes);
    }

    private static async Task<int> ExportedRowsAsync(DbFixture fx, Guid habitId, DateOnly day)
    {
        var bus = new RecordingEventBus();
        var export = new ExportQueryService(
            fx.Factory, fx.Clock, Completions(fx),
            new NoteService(fx.Factory, fx.Clock, NullLogger<NoteService>.Instance, bus, fx.ResetTimes),
            new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus()),
            new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus()),
            new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus()),
            new IntensityRampResolver(), Aggregator(fx),
            new AbstinenceService(fx.Factory, fx.Clock, bus, fx.ResetTimes),
            new AttachmentService(fx.Factory, fx.Clock, fx.PluginContext,
                Path.Combine(Path.GetTempPath(), "recordum-parity-" + Guid.NewGuid().ToString("N"))),
            new ReferenceDataQueryService(fx.Factory), fx.ResetTimes);
        var model = await export.BuildAsync(
            new ExportRequest(ExportSubject.SingleHabit, habitId, day, day, new ExportLayout()));
        return model.Habits.Single().Completions.Count;
    }

    private static async Task<Guid> SeedDailyActionAsync(DbFixture fx, TimeOnly reset, DateTimeOffset createdAt)
    {
        // CreatedAt lo scrive il TimestampInterceptor dall'orologio: per far nascere l'abitudine PRIMA
        // dei giorni misurati si sposta il clock, non si assegna la colonna (il motore streak considera
        // non-programmato tutto ciò che precede la creazione, e senza questo la storia sarebbe vuota).
        var clock = (FakeClock)fx.Clock;
        var original = clock.UtcNow;
        clock.Set(createdAt);
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action,
                Name = "Run", ResetTime = reset,
            });
            await db.SaveChangesAsync();
        }
        await new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus())
            .UpsertAsync(habitId, c =>
            {
                c.ScheduleType = ScheduleType.Daily;
                c.StreakMode = StreakMode.Daily;
                c.StreakRecovery = StreakRecovery.None;
            });
        clock.Set(original);
        return habitId;
    }

    private static async Task RecordAsync(DbFixture fx, Guid habitId, DateTimeOffset at)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.Completions.Add(new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habitId,
            CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
        });
        await db.SaveChangesAsync();
    }
}
