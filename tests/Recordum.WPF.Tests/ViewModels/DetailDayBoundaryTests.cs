using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Events;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

/// <summary>
/// Il Dettaglio, il calendario, le note e i marker ricevono il confine di giornata dallo screen
/// invece di ricalcolarlo (spec R9 §3.3, piano Task 9). Chiude <b>R10</b>: dopo questi test non
/// resta un secondo modo di calcolare "oggi".
/// <para>Ogni test qui mette l'orologio in una finestra dove il giorno del confine e la data UTC
/// <b>non coincidono</b> (22:40Z in fuso +2 = 00:40 del giorno dopo): è l'unico modo di distinguere
/// "il pannello usa il confine che gli è stato dato" da "se lo ricalcola e gli viene uguale".</para>
/// </summary>
public class DetailDayBoundaryTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static DayBoundary Boundary => new(Plus2, TimeOnly.MinValue);

    /// <summary>22:40Z del 18 agosto = 00:40 del <b>19</b> in fuso +2. In UTC è ancora il 18.</summary>
    private static DateTimeOffset LateOn18 => new(2026, 8, 18, 22, 40, 0, TimeSpan.Zero);

    private static readonly DateOnly The19th = new(2026, 8, 19);

    private static HabitNode Habit(HabitType type, DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        NodeType = NodeType.Habit,
        HabitType = type,
        Name = "h",
        Color = "#808080",
        CreatedAt = createdAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    // ---- i quattro casi del piano ------------------------------------------------

    /// <summary>R10. La griglia del calendario e la colorazione dell'astinenza erano due sorgenti
    /// diverse di "oggi": il navigatore in locale, i marker in UTC. Alle 00:40 del 19 la griglia
    /// evidenziava un giorno e la colorazione ne colorava un altro.</summary>
    [Fact]
    public async Task Abstinence_markers_use_the_same_today_as_the_calendar()
    {
        var (sut, nodes, abstinence) = CreateScreen(new FakeClock(LateOn18, Plus2), Boundary);
        var node = Habit(HabitType.Resistance);
        nodes.Store[node.Id] = node;
        abstinence.Periods[node.Id] =
        [
            new AbstinencePeriod
            {
                Id = Guid.NewGuid(), HabitNodeId = node.Id,
                StartedAtMs = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                EndedAtMs = null,
            },
        ];

        await sut.LoadAsync(node.Id);

        // Due superfici indipendenti: la cella marcata "oggi" dalla griglia, e il giorno con cui la
        // colorazione ha chiuso il periodo aperto. Devono nominare lo stesso giorno.
        var todayCell = sut.Calendar.Cells.Single(c => c.IsToday);
        todayCell.Date.Should().Be(The19th);
        sut.LastAbstinenceMarkersToday.Should().Be(The19th);
    }

    /// <summary>L'ancora della griglia anno è il lunedì della settimana di "oggi": il 16 agosto
    /// (domenica, in UTC) e il 17 (lunedì, secondo il confine) cadono in settimane diverse, quindi
    /// la griglia scorre di sette giorni a seconda di chi ha ragione.</summary>
    [Fact]
    public async Task Year_heatmap_anchor_follows_the_local_today()
    {
        // 22:40Z di domenica 16 = lunedì 17 alle 00:40 in fuso +2.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 16, 22, 40, 0, TimeSpan.Zero), Plus2);
        var sut = new YearHeatmapViewModel(
            new FakeCompletionService(), new FakeMetricDefinitionService(),
            new IntensityRampResolver(), new FakeAbstinenceService(), clock);

        await sut.LoadAsync(Habit(HabitType.Action), config: null, activeGoal: null, Boundary);

        // Ultima colonna = la settimana di "oggi", che finisce domenica 23. Con "oggi" = domenica 16
        // la griglia finirebbe quel giorno stesso.
        sut.Cells.Max(c => c.Date).Should().Be(new DateOnly(2026, 8, 23));
    }

    /// <summary>B11 + A28. La finestra rolling finisce sul giorno del confine <b>e</b> pesca gli
    /// istanti di quel giorno: prima era ritagliata su mezzanotte UTC, quindi una registrazione
    /// dell'una di notte cadeva fuori dalla propria giornata.</summary>
    [Fact]
    public async Task Goal_rolling_window_ends_on_the_local_today()
    {
        var clock = new FakeClock(LateOn18, Plus2);
        var completions = new FakeCompletionService();
        var goals = new FakeGoalService();
        var node = Habit(HabitType.Action);
        // 23:00Z del 18 = 01:00 del 19 in locale: appartiene al 19, il giorno che il confine chiama "oggi".
        SeedCompletion(completions, node.Id, new DateTimeOffset(2026, 8, 18, 23, 0, 0, TimeSpan.Zero));
        var goal = new Goal
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "g", IsClosed = false,
            TargetValue = 1m, PeriodType = GoalPeriodType.Rolling, RollingDays = 1,
        };
        goals.Store[goal.Id] = goal;
        var sut = new GoalProgressCardViewModel(
            goals, completions, new FakeMetricDefinitionService(), new FakeReferenceData(), clock);

        await sut.LoadAsync(node, Boundary);

        sut.Actual.Should().Be(1m);
    }

    /// <summary>A46. L'ancora di una nota è la data dichiarata, altrimenti il giorno di creazione —
    /// secondo il confine. Scritta alle 00:40 del 19, appartiene al 19, non al 18 UTC.</summary>
    [Fact]
    public async Task A_note_written_after_local_midnight_is_anchored_to_the_new_day()
    {
        var notes = new FakeNoteService();
        var node = Habit(HabitType.Journal);
        var note = new Note
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id, BodyMd = "dopo mezzanotte",
            CreatedAt = LateOn18.UtcDateTime, UpdatedAtMs = LateOn18.ToUnixTimeMilliseconds(),
            TargetDate = null, Category = NoteCategory.Veloce,
        };
        notes.Store[note.Id] = note;
        var sut = BuildNotes(notes, new FakeClock(LateOn18, Plus2));

        await sut.LoadAsync(node.Id, Boundary);
        sut.Mode = EntriesRecordsMode.Giorno;
        sut.SetCalendarDay(The19th);

        sut.Notes.Should().ContainSingle(r => r.Id == note.Id);
    }

    // ---- i tre parametri che il WIP riceveva e buttava via ------------------------

    /// <summary>A34. <see cref="TrendChartViewModel"/> riceveva il confine e non lo assegnava: la
    /// finestra restava ancorata alla data UTC. Il fake torna sempre gli stessi punti — ciò che si
    /// prova è che il pannello abbia chiesto i giorni giusti.</summary>
    [Fact]
    public async Task Trend_window_ends_on_the_day_of_the_boundary()
    {
        var trends = new FakeTrendQueryService();
        var sut = new TrendChartViewModel(trends, new FakeClock(LateOn18, Plus2));

        await sut.LoadAsync(Habit(HabitType.Counter), Boundary);

        trends.LastTo.Should().Be(The19th);
    }

    /// <summary>A48. Stesso difetto in <see cref="AbstinenceQuickAddViewModel"/>: il giorno della
    /// ricaduta si nominava in UTC anche col confine in mano.</summary>
    [Fact]
    public async Task Abstinence_quick_add_names_the_relapse_day_from_the_boundary()
    {
        var abstinence = new FakeAbstinenceService();
        var node = Habit(HabitType.Resistance);
        abstinence.States[node.Id] = new AbstinenceState(
            AbstinenceStatus.Relapsed, null, LateOn18.ToUnixTimeMilliseconds());
        var sut = new AbstinenceQuickAddViewModel(new FakeCompletionService(), abstinence, new FakeClock(LateOn18, Plus2))
        {
            Node = node,
        };

        await sut.LoadAsync(Boundary);

        sut.RelapsedOn.Should().Be(The19th);
    }

    /// <summary>B12. La lista delle registrazioni di un giorno ritagliava su mezzanotte UTC: la
    /// registrazione dell'una di notte finiva nel giorno prima, e il pannello del 19 restava vuoto.</summary>
    [Fact]
    public async Task Records_of_a_day_include_the_hours_after_local_midnight()
    {
        var completions = new FakeCompletionService();
        var node = Habit(HabitType.Action);
        SeedCompletion(completions, node.Id, new DateTimeOffset(2026, 8, 18, 23, 0, 0, TimeSpan.Zero));
        var sut = new CompletionListViewModel(
            completions, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService())
        {
            DayBoundary = Boundary,
        };

        await sut.LoadDayAsync(node.Id, The19th);

        sut.Rows.Should().ContainSingle();
    }

    /// <summary>Il confine che lo screen risolve scende ai due figli del pannello Registrazioni: se
    /// si fermasse al padre, il periodo verrebbe calcolato su un giorno e ritagliato su un altro.</summary>
    [Fact]
    public void The_records_panel_hands_its_boundary_to_both_lists()
    {
        var records = new CompletionListViewModel(
            new FakeCompletionService(), new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());
        var periods = new AbstinencePeriodListViewModel(
            new FakeAbstinenceService(), new FakeConfirmationDialog(), new FakeClock(LateOn18, Plus2));
        var sut = new HabitRecordsViewModel(records, periods, new FakeClock(LateOn18, Plus2));

        sut.DayBoundary = Boundary;

        records.DayBoundary.Should().Be(Boundary);
        periods.DayBoundary.Should().Be(Boundary);
    }

    // ---- lacune trovate dalle sonde "rosso prima" sulla VM (2026-09-11) --------------

    /// <summary>Il <see cref="DetailViewModel"/> conserva il confine solo per i reload scatenati dal bus
    /// (registrazione, modifica, eliminazione): senza <c>_day = day</c> il primo caricamento resta giusto
    /// — i pannelli ricevono il confine per parametro — ma il reload dopo una registrazione torna in UTC.
    /// Nessun test percorreva quel ramo: la sonda che toglieva l'assegnazione restava verde.</summary>
    [Fact]
    public async Task Detail_reload_after_a_completion_event_keeps_the_boundary()
    {
        var trends = new FakeTrendQueryService();
        var bus = new FakeEventBus();
        var sut = new DetailViewModel(
            new FakeGoalService(), new FakeStreakAggregator(), trends,
            new FakeHourlyDistributionQueryService(), new FakeCompletionService(),
            new FakeMetricDefinitionService(), new FakeReferenceData(), new IntensityRampResolver(),
            new FakeHabitConfigService(), new FakeAbstinenceService(), new FakeClock(LateOn18, Plus2), bus);
        var node = Habit(HabitType.Counter);
        await sut.LoadAsync(node, Boundary);
        var callsAfterLoad = trends.Calls;

        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), node.Id, LateOn18.UtcDateTime));
        await Task.Yield();

        trends.Calls.Should().BeGreaterThan(callsAfterLoad);   // il reload c'è stato: il test non passa a vuoto
        trends.LastTo.Should().Be(The19th);
    }

    /// <summary>La modifica a mano dell'orario di una registrazione: l'utente legge e scrive un orario
    /// di parete nel fuso dell'abitudine. La riga riceve il confine dalla lista; senza, EditedDate
    /// mostrerebbe l'ora UTC e il salvataggio sposterebbe l'istante di due ore. L'editor inline oggi non
    /// ha superficie XAML (R9-M8): per questo nessuno se ne sarebbe accorto.</summary>
    [Fact]
    public async Task Inline_edit_reads_and_writes_wall_time_in_the_habit_zone()
    {
        var completions = new FakeCompletionService();
        var at = new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero);   // 00:00 del 19 in fuso +2
        var completion = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(),
            CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
        };
        completions.Store[completion.Id] = completion;
        var sut = new CompletionRowViewModel(completion, completions, dayBoundary: Boundary);

        sut.EditedDate.Should().Be(new DateTime(2026, 8, 19, 0, 0, 0));

        sut.EditedDate = new DateTime(2026, 8, 19, 1, 30, 0);
        await sut.SaveEditCommand.ExecuteAsync(null);

        var expected = new DateTimeOffset(2026, 8, 18, 23, 30, 0, TimeSpan.Zero);
        completion.CompletedAt.Should().Be(expected.UtcDateTime);
        completion.CompletedAtMs.Should().Be(expected.ToUnixTimeMilliseconds());
    }

    /// <summary>I1. Il confine si risolve PRIMA di qualunque pannello, ramo gruppo compreso: la
    /// Panoramica veniva caricata sopra la riga che lo risolve, quindi riceveva il confine del
    /// soggetto precedente — o <see cref="DayBoundary.Utc"/> al primo caricamento.</summary>
    [Fact]
    public async Task A_group_gets_its_own_boundary_not_the_one_of_the_habit_selected_before()
    {
        var resetTimes = new FakeResetTimeResolver();
        var (sut, nodes, _) = CreateScreen(new FakeClock(LateOn18, Plus2), resetTimes);
        var habit = Habit(HabitType.Action);
        var group = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Group, Name = "g", Color = "#808080",
        };
        nodes.Store[habit.Id] = habit;
        nodes.Store[group.Id] = group;
        var habitBoundary = new DayBoundary(Plus2, new TimeOnly(4, 0));
        var groupBoundary = new DayBoundary(Plus2, TimeOnly.MinValue);
        resetTimes.PerHabit[habit.Id] = habitBoundary;
        resetTimes.PerHabit[group.Id] = groupBoundary;

        await sut.LoadAsync(habit.Id);
        await sut.LoadAsync(group.Id);

        sut.Panoramica.LastLoadedBoundary.Should().Be(groupBoundary);
    }

    // ---- helpers ------------------------------------------------------------------

    private static void SeedCompletion(FakeCompletionService completions, Guid habitId, DateTimeOffset when)
    {
        var id = Guid.NewGuid();
        completions.Store[id] = new Completion
        {
            Id = id, HabitNodeId = habitId,
            CompletedAt = when.UtcDateTime, CompletedAtMs = when.ToUnixTimeMilliseconds(),
        };
    }

    private static NotesViewModel BuildNotes(FakeNoteService notes, FakeClock clock) => new(
        notes, new FakeEventBus(), clock, new FakeWebView2Availability(available: true),
        new FakeAttachmentService(), new FakeOpenFileDialog(), new FakeConfirmationDialog());

    /// <summary>Screen su un orologio e un confine scelti dal chiamante — il factory condiviso li
    /// fissa entrambi, e con quelli "oggi" del confine e data UTC coincidono sempre.</summary>
    private static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes, FakeAbstinenceService Abstinence)
        CreateScreen(FakeClock clock, DayBoundary boundary)
        => CreateScreen(clock, new FakeResetTimeResolver(boundary));

    private static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes, FakeAbstinenceService Abstinence)
        CreateScreen(FakeClock clock, FakeResetTimeResolver resetTimes)
    {
        var nodes = new FakeHabitNodeService();
        var metrics = new FakeMetricDefinitionService();
        var configs = new FakeHabitConfigService();
        var goals = new FakeGoalService();
        var settings = new FakeAppSettingService();
        var uiPrefs = new FakeHabitUiPrefsService();
        var abstinence = new FakeAbstinenceService();
        var editor = new Recordum.WPF.ViewModels.HabitEditor.HabitEditorViewModel(
            nodes, configs, goals, metrics, new FakeNotificationService(), new FakeTagService(),
            settings, new FakeReferenceData(), new Recordum.WPF.Services.RecentColorStore(settings),
            new Recordum.WPF.Services.FavoriteColorStore(settings), new FakeScreenColorPicker(), uiPrefs);
        var vm = new HabitScreenViewModel(
            nodes, new FakeCompletionService(), new FakeActiveTimerService(), metrics, new FakeReferenceData(), configs,
            new FakeNoteService(), goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            abstinence, clock, new FakeEventBus(), new FakeWebView2Availability(available: true), settings, editor,
            new FakeHabitTreeQueryService(), new FakeDiaryService(), new FakeConfirmationDialog(),
            new FakeAttachmentService(), new FakeOpenFileDialog(), uiPrefs, resetTimes);
        return (vm, nodes, abstinence);
    }
}
