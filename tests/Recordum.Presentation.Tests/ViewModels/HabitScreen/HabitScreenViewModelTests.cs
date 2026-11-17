using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class HabitScreenViewModelTests
{

    public static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out bus, out _);

    public static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus,
        out FakeNoteService noteSvc)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out bus, out noteSvc, out _);

    public static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus,
        out FakeNoteService noteSvc,
        out FakeAbstinenceService abstinence)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out bus, out noteSvc, out abstinence, out _);

    public static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus,
        out FakeNoteService noteSvc,
        out FakeAbstinenceService abstinence,
        out FakeHabitUiPrefsService uiPrefs,
        bool editorAvailable = true,
        FakeReferenceData? refData = null)
    {
        refData ??= new FakeReferenceData();
        nodes = new FakeHabitNodeService();
        completions = new FakeCompletionService();
        timers = new FakeActiveTimerService();
        metrics = new FakeMetricDefinitionService();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        bus = new FakeEventBus();
        noteSvc = new FakeNoteService();
        abstinence = new FakeAbstinenceService();
        uiPrefs = new FakeHabitUiPrefsService();
        var configs = new FakeHabitConfigService();
        var goals = new FakeGoalService();
        var settings = new FakeAppSettingService();
        var editor = new Recordum.Presentation.ViewModels.HabitEditor.HabitEditorViewModel(
            nodes, configs, goals, metrics, new FakeNotificationService(), new FakeTagService(),
            settings, refData, new Recordum.Presentation.Services.RecentColorStore(settings),
            new Recordum.Presentation.Services.FavoriteColorStore(settings),
            new FakeScreenColorPicker(),
            uiPrefs,
            new FakeScaleTemplateService());
        return new HabitScreenViewModel(
            nodes, completions, timers, metrics, refData, configs,
            noteSvc, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            abstinence, clock, bus, new FakeEditorAvailability(editorAvailable), settings, editor,
            new FakeHabitTreeQueryService(), new FakeDiaryService(), new FakeConfirmationDialog(),
            new FakeAttachmentService(), new FakePhotoImporter(), new FakeTagService(), new FakeOpenFileDialog(), uiPrefs, new FakeResetTimeResolver());
    }

    public static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out _);

    public static HabitNode AddJournal(FakeHabitNodeService nodes, string name)
    {
        var node = new HabitNode
        { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = name, Color = "#808080" };
        nodes.Store[node.Id] = node;
        return node;
    }

    [Fact]
    public async Task LoadAsync_null_id_clears_state()
    {
        var sut = CreateSut(out _, out _, out _, out _, out _);

        await sut.LoadAsync(null);

        sut.HasNode.Should().BeFalse();
        sut.Name.Should().BeEmpty();
        sut.Node.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_loads_node_and_propagates_to_sub_vms()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Yoga", Color = "#000000" };

        await sut.LoadAsync(id);

        sut.HasNode.Should().BeTrue();
        sut.Name.Should().Be("Yoga");
        sut.CompletionList.NodeId.Should().Be(id);
    }

    [Fact]
    public async Task LoadAsync_also_loads_NotesViewModel()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out var noteSvc);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Diary" };
        noteSvc.Store[Guid.NewGuid()] = new Recordum.Core.Models.Note { Id = Guid.NewGuid(), HabitNodeId = id, BodyMd = "a", UpdatedAtMs = 1 };
        noteSvc.Store[Guid.NewGuid()] = new Recordum.Core.Models.Note { Id = Guid.NewGuid(), HabitNodeId = id, BodyMd = "b", UpdatedAtMs = 2 };

        await sut.LoadAsync(id);

        sut.Notes.Notes.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_unknown_id_clears_node()
    {
        var sut = CreateSut(out _, out _, out _, out _, out _);

        await sut.LoadAsync(Guid.NewGuid());

        sut.HasNode.Should().BeFalse();
    }

    [Fact]
    public async Task Day_summary_for_a_counter_shows_the_primary_metric_sum_not_the_occurrences()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out var metrics, out var clock);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode
        { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "reading" };
        var metric = new MetricDefinition
        { Id = Guid.NewGuid(), HabitNodeId = id, SortOrder = 0, Name = "pages" };
        metrics.Store[metric.Id] = metric;
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        // The metric sum today is 7; the occurrence magnitude is a differing decoy (3) that also drives
        // the strip's presence signal. The pushed summary must read 7, not 3.
        completions.DailySumByKey[(id, metric.Id)] = new List<DailyValue> { new(today, 7m) };
        completions.MagnitudeByHabit[id] = new List<DailyMagnitude> { new(today, 3m) };

        await sut.LoadAsync(id);
        // Drive the public calendar day-selection path for today (re-pushes the day summary).
        sut.Calendar.SelectDateCommand.Execute(sut.Calendar.Cells.Single(c => c.Date == today));

        sut.DayNote.SummaryText.Should().Contain("7");
        sut.DayNote.SummaryText.Should().NotContain("3");
    }

    [Fact]
    public async Task Focus_expands_one_column_and_hides_the_others()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        sut.IsCronologiaOpen = true;
        sut.IsNoteOpen = true;
        sut.ShowCronologia.Should().BeTrue();
        sut.ShowNote.Should().BeTrue();

        sut.ToggleFocusCommand.Execute(HabitColumn.Note);

        sut.IsFocusActive.Should().BeTrue();
        sut.ShowChipBar.Should().BeFalse();
        sut.ShowNote.Should().BeTrue();
        sut.ShowCronologia.Should().BeFalse();

        sut.ExitFocusCommand.Execute(null);
        sut.IsFocusActive.Should().BeFalse();
        sut.ShowCronologia.Should().BeTrue();
    }

    [Fact]
    public async Task OpenColumnsMinWidth_tracks_the_open_applicable_columns()
    {
        // Drives the hybrid fill-then-scroll: the columns Grid MinWidth = open count × 320, so the
        // host ScrollViewer scrolls once the panes no longer fit instead of squeezing them.
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);

        sut.IsCronologiaOpen = true;
        sut.IsDettaglioOpen = true;
        sut.IsNoteOpen = true;
        sut.OpenColumnsMinWidth.Should().Be(3 * HabitScreenViewModel.MinColumnWidth);

        // Focus collapses the visible set to one column → one column's worth of min width.
        sut.ToggleFocusCommand.Execute(HabitColumn.Note);
        sut.OpenColumnsMinWidth.Should().Be(HabitScreenViewModel.MinColumnWidth);
    }

    [Fact]
    public async Task Closing_the_focused_column_exits_focus()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        sut.IsNoteOpen = true;
        sut.ToggleFocusCommand.Execute(HabitColumn.Note);
        sut.IsFocusActive.Should().BeTrue();

        sut.IsNoteOpen = false;

        sut.FocusedColumn.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_populates_CompletionList_rows_from_service()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        completions.Store[Guid.NewGuid()] = new Completion { Id = Guid.NewGuid(), HabitNodeId = id, CompletedAt = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc), CompletedAtMs = 1 };
        completions.Store[Guid.NewGuid()] = new Completion { Id = Guid.NewGuid(), HabitNodeId = id, CompletedAt = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc), CompletedAtMs = 2 };

        await sut.LoadAsync(id);

        sut.CompletionList.Rows.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(HabitType.Action, typeof(ActionQuickAddViewModel))]
    [InlineData(HabitType.Counter, typeof(CounterTapQuickAddViewModel))] // no metrics seeded → bare tap shape
    [InlineData(HabitType.Resistance, typeof(AbstinenceQuickAddViewModel))]
    [InlineData(HabitType.Journal, typeof(JournalQuickAddViewModel))]
    [InlineData(HabitType.State, typeof(MeasuredQuickAddViewModel))]
    public async Task LoadAsync_picks_correct_QuickAdd_VM_for_HabitType(HabitType type, Type expectedVmType)
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "x" };

        await sut.LoadAsync(id);

        sut.QuickAdd.Should().NotBeNull();
        sut.QuickAdd!.GetType().Should().Be(expectedVmType);
    }

    [Fact]
    public async Task LoadAsync_null_id_clears_QuickAdd()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        sut.QuickAdd.Should().NotBeNull();

        await sut.LoadAsync(null);

        sut.QuickAdd.Should().BeNull();
    }

    [Fact]
    public async Task Markers_resistance_colors_days_from_abstinence_periods_never_numbers()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out var abstinence);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "NoSmoke", Color = "#3366cc" };

        // Clock is 2026-05-21 → calendar loads May 2026. A closed period 2026-05-08..2026-05-12
        // makes 05-08..05-11 clean days and 05-12 the lapse day.
        static long Ms(int y, int m, int d) => new DateTimeOffset(new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        abstinence.Periods[id] = new List<AbstinencePeriod>
        {
            new() { Id = Guid.NewGuid(), HabitNodeId = id, StartedAtMs = Ms(2026, 5, 8), EndedAtMs = Ms(2026, 5, 12) },
        };

        await sut.LoadAsync(id);

        var cleanDay = sut.Calendar.Cells.Single(c => c.Date == new DateOnly(2026, 5, 10));
        cleanDay.Number.Should().BeNull(); // Resistance never shows a count
        cleanDay.TintStep.Should().Be(3);

        var lapseDay = sut.Calendar.Cells.Single(c => c.Date == new DateOnly(2026, 5, 12));
        lapseDay.Number.Should().BeNull();
        lapseDay.TintStep.Should().Be(4); // danger, full intensity
    }

    [Fact]
    public async Task Markers_state_intensity_comes_from_the_scale_fraction_and_a_recorded_zero_is_marked()
    {
        var refData = new FakeReferenceData();
        var sut = CreateSut(out var nodes, out var completions, out _, out var metrics, out _, out _, out _, out _, out _, refData: refData);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "Umore", Color = "#3366cc" };
        // -3..+3: 0 sits in the middle (fraction 0.5 -> step 2). Against the month's best it would be the lowest step.
        var scale = refData.Scales[0];
        scale.MinValue = -3; scale.MaxValue = 3;
        metrics.Store[Guid.NewGuid()] = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "Umore", ScaleTemplateId = scale.Id, SortOrder = 0 };
        completions.MagnitudeByHabit[id] =
        [
            new DailyMagnitude(new DateOnly(2026, 5, 10), -3m),
            new DailyMagnitude(new DateOnly(2026, 5, 11), 0m),
            new DailyMagnitude(new DateOnly(2026, 5, 12), 3m),
        ];

        await sut.LoadAsync(id);

        int Step(int d) => sut.Calendar.Cells.Single(c => c.Date == new DateOnly(2026, 5, d)).TintStep;
        Step(10).Should().Be(1);   // fraction 0: still recorded, lowest step
        Step(11).Should().Be(2);   // a recorded 0 is marked, at the scale's middle
        Step(12).Should().Be(4);
        Step(13).Should().Be(0);   // no value, no tint
    }

    [Fact]
    public async Task Markers_resistance_still_shows_note_badges()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out var noteSvc, out var abstinence);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "NoSmoke", Color = "#3366cc" };

        // Clock is 2026-05-21 → calendar loads May 2026. Open period from 05-01 keeps 05-12 a clean day.
        var dayB = new DateOnly(2026, 5, 12);
        abstinence.Periods[id] = new List<AbstinencePeriod>
        {
            new() { Id = Guid.NewGuid(), HabitNodeId = id, StartedAtMs = new DateTimeOffset(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds(), EndedAtMs = null },
        };
        noteSvc.Store[Guid.NewGuid()] = new Note { Id = Guid.NewGuid(), HabitNodeId = id, BodyMd = "a", UpdatedAtMs = 1, TargetDate = dayB };
        noteSvc.Store[Guid.NewGuid()] = new Note { Id = Guid.NewGuid(), HabitNodeId = id, BodyMd = "b", UpdatedAtMs = 2, TargetDate = dayB };

        await sut.LoadAsync(id);

        var cellB = sut.Calendar.Cells.Single(c => c.Date == dayB);
        cellB.Number.Should().BeNull();
        cellB.NoteCount.Should().Be(2);
        cellB.HasNote.Should().BeTrue();
        cellB.ShowNoteCount.Should().BeTrue();
    }

    [Fact]
    public async Task Lapse_via_quickadd_refreshes_records_so_open_card_is_no_longer_current()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out var abstinence);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "NoSmoke" };
        // Open period started before the clock's "today" (2026-05-21) so it intersects today's Giorno view.
        var startMs = new DateTimeOffset(new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds();
        abstinence.Periods[id] = new List<AbstinencePeriod>
        {
            new() { Id = Guid.NewGuid(), HabitNodeId = id, StartedAtMs = startMs, EndedAtMs = null },
        };
        abstinence.States[id] = new AbstinenceState(AbstinenceStatus.Abstaining, startMs, null);

        await sut.LoadAsync(id);
        await sut.Records.ShowDayAsync(new DateOnly(2026, 5, 21)); // ensure the records list is loaded for today

        var list = (AbstinencePeriodListViewModel)sut.Records.CurrentList;
        list.Rows.Should().Contain(r => r.IsCurrent); // "In corso" shown after load

        // Lapse through the quick-add: closes the open period + raises Changed → OnAbstinenceChanged → records reload.
        var quick = (AbstinenceQuickAddViewModel)sut.QuickAdd!;
        await quick.LapseCommand.ExecuteAsync(null);
        await Task.Delay(50); // OnAbstinenceChanged is async void

        list = (AbstinencePeriodListViewModel)sut.Records.CurrentList;
        list.Rows.Any(r => r.IsCurrent).Should().BeFalse(); // the just-lapsed period no longer shows "In corso"
    }

    [Fact]
    public async Task EventBus_CompletionRecorded_for_my_habit_triggers_List_reload()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _, out var bus);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        sut.CompletionList.Rows.Should().BeEmpty();
        // A completion appears after load; only a fresh reload (driven by the event) surfaces it.
        var cid = Guid.NewGuid();
        completions.Store[cid] = new Completion { Id = cid, HabitNodeId = id, CompletedAtMs = 1 };

        bus.Publish(new Recordum.Plugins.Contracts.Events.CompletionRecordedPayload(cid, id, DateTime.UtcNow));
        await Task.Delay(20);

        sut.CompletionList.Rows.Should().NotBeEmpty();
    }

    /// <summary>The bare-counter quick-add keeps its day count in a local field it bumps on each tap,
    /// so a completion deleted anywhere else (the Cronologia "×") leaves it reading high — the very
    /// drift the visual pass caught as "9 oggi" over 5 stored records (W23-VF1).</summary>
    [Fact]
    public async Task EventBus_CompletionDeleted_resyncs_the_tap_quick_add_day_count()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out var clock, out var bus);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "tap" };
        var todayMs = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var (cid, i) in ids.Select((c, i) => (c, i)))
            completions.Store[cid] = new Completion { Id = cid, HabitNodeId = id, CompletedAtMs = todayMs + i };

        await sut.LoadAsync(id);
        var tap = sut.QuickAdd.Should().BeOfType<CounterTapQuickAddViewModel>().Subject;
        tap.DayCount.Should().Be(3);

        completions.Store.Remove(ids[0]); // deleted from the records list, not from the quick-add
        bus.Publish(new Recordum.Plugins.Contracts.Events.CompletionDeletedPayload(ids[0], id, DateTime.UtcNow));
        await Task.Delay(20);

        tap.DayCount.Should().Be(2);
    }

    /// <summary>Switching day moves the quick-add's CompletionDate but used to leave the count showing
    /// the previous day's total, which the next tap would then increment from (W23-VF1).</summary>
    [Fact]
    public async Task Selecting_another_day_resyncs_the_tap_quick_add_day_count()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "tap" };
        // Three on 2020-01-02, none on the day we will select.
        var dayMs = new DateTimeOffset(new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        for (var i = 0; i < 3; i++)
        {
            var cid = Guid.NewGuid();
            completions.Store[cid] = new Completion { Id = cid, HabitNodeId = id, CompletedAtMs = dayMs + i };
        }

        await sut.LoadAsync(id);
        await sut.Calendar.LoadAsync(2020, 1, selected: null, DayBoundary.Utc);
        var tap = sut.QuickAdd.Should().BeOfType<CounterTapQuickAddViewModel>().Subject;

        var busy = sut.Calendar.Cells.First(c => c.InCurrentMonth && c.Date == new DateOnly(2020, 1, 2));
        sut.Calendar.SelectDateCommand.Execute(busy);
        await Task.Delay(20);
        tap.DayCount.Should().Be(3);

        var empty = sut.Calendar.Cells.First(c => c.InCurrentMonth && c.Date == new DateOnly(2020, 1, 5));
        sut.Calendar.SelectDateCommand.Execute(empty);
        await Task.Delay(20);
        tap.DayCount.Should().Be(0);
    }

    [Fact]
    public async Task EventBus_CompletionEvent_for_other_habit_is_ignored()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _, out var bus);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        // A completion exists for a different habit; an event for that other habit must be ignored.
        var otherId = Guid.NewGuid();
        var cid = Guid.NewGuid();
        completions.Store[cid] = new Completion { Id = cid, HabitNodeId = otherId, CompletedAtMs = 1 };

        bus.Publish(new Recordum.Plugins.Contracts.Events.CompletionRecordedPayload(cid, otherId, DateTime.UtcNow));
        await Task.Delay(20);

        sut.CompletionList.Rows.Should().BeEmpty(); // our habit's list untouched
    }

    [Fact]
    public async Task EventBus_drives_reload_after_QuickAdd_Save_in_production()
    {
        // In production CompletionService.RecordAsync publishes CompletionRecorded
        // and HabitScreenViewModel subscribes to refresh. FakeCompletionService doesn't
        // publish, so simulate the event publish path directly.
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out var bus);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        sut.CompletionList.Rows.Should().BeEmpty();

        await sut.QuickAdd!.SaveAsync(); // records into Store
        bus.Publish(new Recordum.Plugins.Contracts.Events.CompletionRecordedPayload(Guid.NewGuid(), id, DateTime.UtcNow));
        await Task.Delay(20);

        sut.CompletionList.Rows.Should().NotBeEmpty();
    }

    private sealed class DisposableStubQuickAdd : QuickAddViewModelBase, IDisposable
    {
        public DisposableStubQuickAdd() : base(new FakeCompletionService(), new FakeClock()) { }
        public bool Disposed { get; private set; }
        public override Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Assigning_a_new_quick_add_disposes_the_previous_one()
    {
        var sut = CreateSut(out _, out _, out _, out _, out _);
        var first = new DisposableStubQuickAdd();
        sut.QuickAdd = first;

        sut.QuickAdd = new DisposableStubQuickAdd(); // swap

        first.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Disposing_the_screen_disposes_the_current_quick_add()
    {
        var sut = CreateSut(out _, out _, out _, out _, out _);
        var current = new DisposableStubQuickAdd();
        sut.QuickAdd = current;

        sut.Dispose();

        current.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_deselect_during_an_in_flight_load_does_not_throw_or_clobber()
    {
        // Regression: selecting then deselecting fires overlapping loads. A stale load that resumes
        // after the deselect must not dereference the now-null Node (NRE) nor re-show the habit.
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out _, out var configs);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run", Color = "#123456" };

        var gate = new TaskCompletionSource();
        configs.GetGate = () => gate.Task; // hold the in-flight load inside Task.WhenAll

        var loading = sut.LoadAsync(id); // sets Node, enters WhenAll, awaits the config gate
        await sut.LoadAsync(null);       // deselect mid-flight: Node = null, generation bumped
        gate.SetResult();                // release the superseded load
        await loading;                   // resumes after WhenAll, sees it lost the race, returns

        sut.Node.Should().BeNull();
        sut.HasNode.Should().BeFalse();
        // The superseded load must not have applied the habit's state (e.g. the calendar color).
        sut.Calendar.CompletedColorHex.Should().NotBe("#123456");
    }

    // ---- Onda 2.4 (U7): the Diario column hosts the quick-note composer OR the markdown editor ----

    [Fact]
    public async Task Quick_note_journal_hosts_the_composer_and_never_the_markdown_editor()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs);
        var node = AddJournal(nodes, "Food diary");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });

        await sut.LoadAsync(node.Id);

        sut.QuickEntryForDiario.Should().NotBeNull();
        sut.QuickAddForDiario.Should().BeNull("WebView2 non deve istanziarsi per un'abitudine a nota veloce");
    }

    [Fact]
    public async Task Diary_journal_still_hosts_the_markdown_editor()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs);
        var node = AddJournal(nodes, "Daily journal");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.Diary });

        await sut.LoadAsync(node.Id);

        sut.QuickAddForDiario.Should().NotBeNull();
        sut.QuickEntryForDiario.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_diary_gesture_journal_opens_the_quick_note_only_when_the_editor_is_unavailable(
        bool editorAvailable, bool expectsQuickNote)
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs, editorAvailable);
        var node = AddJournal(nodes, "Daily journal");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.Diary });

        await sut.LoadAsync(node.Id);

        (sut.QuickEntryForDiario is not null).Should().Be(expectsQuickNote);
        (sut.QuickAddForDiario is not null).Should().Be(!expectsQuickNote);
    }

    [Theory]
    [InlineData(JournalPrimaryGesture.Diary, true)]
    [InlineData(JournalPrimaryGesture.QuickNote, false)]
    public async Task The_composer_is_in_diary_mode_only_for_the_diary_gesture(JournalPrimaryGesture gesture, bool expectsDiaryMode)
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs, editorAvailable: false);
        var node = AddJournal(nodes, "Daily journal");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = gesture });

        await sut.LoadAsync(node.Id);

        sut.JournalQuickEntry.Should().NotBeNull();
        sut.JournalQuickEntry!.IsDiaryMode.Should().Be(expectsDiaryMode);
    }

    [Fact]
    public async Task A_journal_with_no_pref_defaults_to_the_diary_editor()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out _, out _);
        var node = AddJournal(nodes, "Senza prefs");

        await sut.LoadAsync(node.Id);

        sut.QuickAddForDiario.Should().NotBeNull();
    }

    [Fact]
    public async Task The_quick_note_affordance_opens_the_Diario_column()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs);
        var node = AddJournal(nodes, "Food diary");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });
        await sut.LoadAsync(node.Id);
        sut.IsDiarioOpen = false;

        sut.RaiseTodayStripActionForTest(TodayContentAction.OpenQuickNote);

        sut.IsDiarioOpen.Should().BeTrue("il composer vive in Diario, non in Note");
        sut.IsNoteOpen.Should().BeFalse();
    }
}
