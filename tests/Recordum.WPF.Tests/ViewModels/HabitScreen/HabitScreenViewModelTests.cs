using FluentAssertions;
using Microsoft.Web.WebView2.Core;
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
    {
        nodes = new FakeHabitNodeService();
        completions = new FakeCompletionService();
        timers = new FakeActiveTimerService();
        metrics = new FakeMetricDefinitionService();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        bus = new FakeEventBus();
        noteSvc = new FakeNoteService();
        return new HabitScreenViewModel(
            nodes, completions, timers, metrics, new FakeReferenceData(), new FakeHabitConfigService(),
            noteSvc, new FakeGoalService(), new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            clock, bus, new FakeWebView2Availability(true));
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
    [InlineData(HabitType.Counter, typeof(MeasuredQuickAddViewModel))]
    [InlineData(HabitType.Resistance, typeof(ResistanceQuickAddViewModel))]
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
    public async Task Markers_resistance_number_hidden_when_one_series()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _, out _, out var noteSvc);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "NoSmoke", Color = "#3366cc" };

        // Clock is 2026-05-21 → calendar loads May 2026.
        var dayA = new DateOnly(2026, 5, 10);
        var dayB = new DateOnly(2026, 5, 12);
        completions.MagnitudeByHabit[id] = new[]
        {
            new DailyMagnitude(dayA, 3m),
            new DailyMagnitude(dayB, 1m),
        };
        noteSvc.Store[Guid.NewGuid()] = new Note { Id = Guid.NewGuid(), HabitNodeId = id, BodyMd = "a", UpdatedAtMs = 1, TargetDate = dayB };
        noteSvc.Store[Guid.NewGuid()] = new Note { Id = Guid.NewGuid(), HabitNodeId = id, BodyMd = "b", UpdatedAtMs = 2, TargetDate = dayB };

        await sut.LoadAsync(id);

        var cellA = sut.Calendar.Cells.Single(c => c.Date == dayA);
        cellA.Number.Should().Be("3");
        cellA.TintStep.Should().BeGreaterThanOrEqualTo(1);

        var cellB = sut.Calendar.Cells.Single(c => c.Date == dayB);
        cellB.Number.Should().BeNull(); // resistance magnitude == 1 → number hidden
        cellB.NoteCount.Should().Be(2);
        cellB.HasNote.Should().BeTrue();
        cellB.ShowNoteCount.Should().BeTrue();
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
}
