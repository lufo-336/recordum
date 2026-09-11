using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

/// <summary>
/// La fascia "Oggi" e la riga Home ricevono il confine di giornata dallo screen invece di
/// ricalcolarlo (spec R9 §3.3). Prima erano tre formule diverse fra questi cinque file — e
/// <see cref="ResistanceTodayContent"/> era l'unico a calcolare "oggi" in <b>UTC</b>.
/// <para>Ogni test qui usa un confine che <b>non coincide</b> con l'ora locale nuda dell'orologio
/// (reset 04:00, oppure fuso che sposta il giorno): è l'unico modo di distinguere "il pannello usa
/// il confine che gli è stato dato" da "il pannello se lo ricalcola e gli viene uguale".</para>
/// </summary>
public class TodayStripDayBoundaryTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    /// <summary>Confine con reset alle 04:00: alle 02:40 locali si è ancora nel giorno prima.</summary>
    private static DayBoundary LateReset => new(Plus2, new TimeOnly(4, 0));

    /// <summary>02:40 locali del 19 agosto in fuso +2 — dentro la finestra 00:00-04:00 dove il
    /// confine con reset tardivo e l'ora locale nuda dicono giorni diversi.</summary>
    private static DateTimeOffset EarlyMorningOf19 => new(2026, 8, 19, 0, 40, 0, TimeSpan.Zero);

    private static HabitNode Habit(HabitType type) =>
        new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = type, Name = "h", Color = "#808080" };

    [Fact]
    public async Task Action_today_follows_the_reset_time_it_is_given()
    {
        // Orologio alle 02:40 locali del 19 con reset 04:00: il giorno corrente e' ancora il 18,
        // quindi una registrazione del 18 pomeriggio conta come "fatto oggi". Con l'ora locale nuda
        // il pannello guardava il 19, non trovava nulla e diceva "non fatto".
        var clock = new FakeClock(EarlyMorningOf19, Plus2);
        var completions = new FakeCompletionService();
        var node = Habit(HabitType.Action);
        var at = new DateTimeOffset(2026, 8, 18, 14, 0, 0, TimeSpan.Zero);
        completions.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id,
            CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
        };
        var sut = new ActionTodayContent(
            completions, new FakeStreakAggregator(), new FakeHourlyDistributionQueryService(), clock);

        await sut.LoadAsync(node, LateReset);

        sut.IsDoneToday.Should().BeTrue();
    }

    [Fact]
    public async Task Resistance_today_uses_the_same_today_as_its_siblings()
    {
        // A53: era l'unico dei cinque a calcolare "oggi" in UTC. Orologio alle 22:40Z del 18 in fuso
        // +2 = 19 alle 00:40 locali, quindi la finestra dei tentativi 90g parte dal 21 maggio, non
        // dal 20: il tentativo del 20 maggio resta fuori e ne conta uno solo.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 40, 0, TimeSpan.Zero), Plus2);
        var abstinence = new FakeAbstinenceService();
        var node = Habit(HabitType.Resistance);
        abstinence.Periods[node.Id] =
        [
            Period(node.Id, new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero)),
            Period(node.Id, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)),
        ];
        var sut = new ResistanceTodayContent(abstinence, clock);

        await sut.LoadAsync(node, new DayBoundary(Plus2, TimeOnly.MinValue));

        sut.AttemptsLast90Days.Should().Be(1);
    }

    [Fact]
    public async Task A_04_00_reset_moves_todays_counter_total_to_the_previous_day()
    {
        // Registrazione alle 02:40 locali con reset 04:00: il totale di "oggi" e' quello del 18.
        var clock = new FakeClock(EarlyMorningOf19, Plus2);
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var node = Habit(HabitType.Counter);
        var metric = new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "glasses",
            Type = MetricType.Decimal, SortOrder = 0,
        };
        metrics.Store[metric.Id] = metric;
        completions.DailySumByKey[(node.Id, metric.Id)] =
            [new DailyValue(new DateOnly(2026, 8, 18), 3m)];
        var sut = new CounterTodayContent(
            completions, metrics, new FakeReferenceData(), new FakeGoalService(), clock);

        await sut.LoadAsync(node, LateReset);

        sut.TodayTotal.Should().Be(3m);
    }

    /// <summary>A54/A55/B15. Il quinto pannello della fascia — quello che la prima stesura di questo
    /// file NON copriva, ed è esattamente il buco da cui il difetto è passato: <see cref="StateTodayContent"/>
    /// riceveva il confine e non lo assegnava, quindi leggeva (e cancellava) sul giorno UTC.</summary>
    [Fact]
    public async Task State_reads_todays_value_through_the_reset_time_it_is_given()
    {
        // 02:40 locali del 19 con reset 04:00: "oggi" è ancora il 18, e il valore registrato il 18
        // dev'essere quello che lo slider ripropone. Con il confine UTC il pannello cercava il 19,
        // non trovava nulla e mostrava lo slider al minimo come se la giornata fosse vuota.
        var clock = new FakeClock(EarlyMorningOf19, Plus2);
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var refData = new FakeReferenceData();
        var node = Habit(HabitType.State);
        // Etichette esplicite: la scala del fake non ha MinValue/MaxValue, quindi senza etichette
        // avrebbe un solo gradino (Max = 0) e il Clamp schiaccerebbe a 0 qualunque indice letto.
        metrics.Store[Guid.NewGuid()] = new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id, SortOrder = 0,
            ScaleTemplateId = refData.Scales[0].Id, Name = "mood",
            LabelsJson = """["1","2","3","4","5"]""",
        };
        completions.MagnitudeByHabit[node.Id] = [new DailyMagnitude(new DateOnly(2026, 8, 18), 3m)];
        var sut = new StateTodayContent(completions, metrics, refData, clock);

        await sut.LoadAsync(node, LateReset);

        sut.LastValueLabel.Should().NotBeNull();
        sut.SliderValue.Should().Be(2);   // magnitudine 3 = indice 2 (il valore memorizzato è 1-based)
    }

    [Fact]
    public async Task Home_row_streak_asks_for_the_window_of_the_boundary_it_is_given()
    {
        // Le Runs del fake tornano sempre uguali: cio' che si puo' provare — e che conta — e' che la
        // riga abbia chiesto la finestra del giorno GIUSTO. Con reset 04:00 alle 02:40 locali del 19,
        // "oggi" e' il 18.
        var clock = new FakeClock(EarlyMorningOf19, Plus2);
        var aggregator = new FakeStreakAggregator();
        aggregator.Runs.Add(new StreakRun(new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 18), 2, IsCurrent: true));
        var id = Guid.NewGuid();
        var row = new HomeRowViewModel(
            new HomeTreeRow(id, null, "h", "#000000", HabitType.Action, null, 0, false, PinScope.None, 0, false,
                ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty),
            0, new FakeHabitNodeService(), new FakePurgeService(),
            uiPrefs: null, streaks: aggregator, clock: clock);

        await row.LoadKeyFigureAsync(LateReset);

        aggregator.LastRange.Should().NotBeNull();
        aggregator.LastRange!.Value.To.Should().Be(new DateOnly(2026, 8, 18));
    }

    private static AbstinencePeriod Period(Guid habitId, DateTimeOffset startedAt) => new()
    {
        Id = Guid.NewGuid(),
        HabitNodeId = habitId,
        StartedAtMs = startedAt.ToUnixTimeMilliseconds(),
        EndedAtMs = startedAt.AddDays(1).ToUnixTimeMilliseconds(),
    };
}
