using FluentAssertions;
using Microsoft.Web.WebView2.Core;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenViewModelTests
{

    private static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out bus, out _);

    private static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus,
        out FakeNoteService noteSvc)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out bus, out noteSvc, out _);

    private static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock,
        out FakeEventBus bus,
        out FakeNoteService noteSvc,
        out FakeAbstinenceService abstinence)
    {
        nodes = new FakeHabitNodeService();
        completions = new FakeCompletionService();
        timers = new FakeActiveTimerService();
        metrics = new FakeMetricDefinitionService();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        bus = new FakeEventBus();
        noteSvc = new FakeNoteService();
        abstinence = new FakeAbstinenceService();
        var configs = new FakeHabitConfigService();
        var goals = new FakeGoalService();
        var settings = new FakeAppSettingService();
        var editor = new Recordum.WPF.ViewModels.HabitEditor.HabitEditorViewModel(
            nodes, configs, goals, metrics, new FakeNotificationService(), new FakeTagService(),
            settings, new FakeReferenceData(), new Recordum.WPF.Services.RecentColorStore(settings),
            new Recordum.WPF.Services.FavoriteColorStore(settings),
            new FakeScreenColorPicker(),
            new FakeHabitUiPrefsService());
        return new HabitScreenViewModel(
            nodes, completions, timers, metrics, new FakeReferenceData(), configs,
            noteSvc, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            abstinence, clock, bus, new FakeWebView2Availability(true), settings, editor,
            new FakeHabitTreeQueryService(), new FakeDiaryService(), new FakeConfirmationDialog(),
            new FakeAttachmentService(), new FakeOpenFileDialog(), new FakeHabitUiPrefsService());
    }

    private static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock)
        => CreateSut(out nodes, out completions, out timers, out metrics, out clock, out _);

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
    public async Task OpenConfigureCommand_raises_event()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        var fired = 0;
        sut.ConfigureRequested += (_, _) => fired++;

        sut.OpenConfigureCommand.Execute(null);

        fired.Should().Be(1);
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
}
