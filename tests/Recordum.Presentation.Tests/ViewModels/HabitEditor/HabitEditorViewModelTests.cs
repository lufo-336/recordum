using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class HabitEditorViewModelTests
{
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakeHabitConfigService _configs = new();
    private readonly FakeGoalService _goals = new();
    private readonly FakeMetricDefinitionService _metrics = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeTagService _tags = new();
    private readonly FakeAppSettingService _appSettings = new();
    private readonly FakeReferenceData _refData = new();
    private readonly FakeEventBus _events = new();

    private HabitEditorViewModel CreateSut() => new(_nodes, _configs, _goals, _metrics, _notifications, _tags, _appSettings, _refData, new RecentColorStore(_appSettings), new FavoriteColorStore(_appSettings), new FakeScreenColorPicker(), new FakeHabitUiPrefsService(), _events);

    [Fact]
    public async Task LoadAsync_with_null_clears_node()
    {
        var sut = CreateSut();
        await sut.LoadAsync(null);
        sut.HasNode.Should().BeFalse();
        sut.Node.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_deselect_during_an_in_flight_load_does_not_throw_or_clobber()
    {
        // CR14: editing the configured habit and switching fires overlapping loads. A stale load that
        // resumes after a deselect must bail (generation guard) — never re-apply its Node or finalize.
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "A", Color = "#aaaaaa" };
        var gate = new TaskCompletionSource();
        _configs.GetGate = () => gate.Task;
        var sut = CreateSut();

        var loadingA = sut.LoadAsync(id);  // sets Node, then blocks inside _configs.GetAsync
        await sut.LoadAsync(null);         // deselect mid-flight → Node null, generation bumped
        gate.SetResult();                  // release the superseded load
        var act = async () => await loadingA;

        await act.Should().NotThrowAsync();
        sut.Node.Should().BeNull();
        sut.HasNode.Should().BeFalse();
    }

    [Fact]
    public async Task Changing_color_publishes_HabitUpdated_so_views_refresh_live()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run", Color = "#111111" };
        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.Color = "#22aa44";
        await Task.Delay(20); // let the fire-and-forget header autosave flush

        _nodes.Store[id].Color.Should().Be("#22aa44");
        _events.Published.OfType<Recordum.Plugins.Contracts.Events.HabitUpdatedPayload>()
            .Should().Contain(p => p.HabitNodeId == id);
    }

    [Fact]
    public async Task LoadAsync_with_known_id_loads_L1_fields_and_section_node_ids()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode
        {
            Id = id,
            NodeType = NodeType.Habit,
            HabitType = HabitType.Counter,
            Name = "Workout",
            Description = "Session log",
            Color = "#abcdef",
            Icon = "dumbbell",
        };
        _configs.Store[id] = new HabitConfig
        {
            Id = Guid.NewGuid(),
            HabitNodeId = id,
            ScheduleType = ScheduleType.Daily,
            StreakMode = StreakMode.Daily,
            StreakRecovery = StreakRecovery.None,
        };

        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.HasNode.Should().BeTrue();
        sut.Name.Should().Be("Workout");
        sut.Description.Should().Be("Session log");
        sut.Color.Should().Be("#abcdef");
        sut.Icon.Should().Be("dumbbell");
        sut.SelectedNodeType.Should().Be(NodeType.Habit);
        sut.SelectedHabitType.Should().Be(HabitType.Counter);
        sut.Scheduling.NodeId.Should().Be(id);
        sut.Streak.NodeId.Should().Be(id);
        sut.Timer.NodeId.Should().Be(id);
    }

    [Fact]
    public async Task LoadAsync_does_not_trigger_header_autosave_on_initial_field_population()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", Color = "#000000" };

        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.HeaderStatus.Should().Be(EditorStatus.Idle);
    }

    [Fact]
    public async Task Changing_Name_after_load_autosaves_via_HabitNodeService()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", Color = "#000000" };
        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.Name = "renamed";
        await Task.Delay(20);

        _nodes.Store[id].Name.Should().Be("renamed");
        sut.HeaderStatus.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task Empty_Name_sets_HeaderStatus_to_Error_and_skips_persistence()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "ok", Color = "#000000" };
        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.Name = "   ";
        await Task.Delay(20);

        sut.HeaderStatus.Should().Be(EditorStatus.Error);
        _nodes.Store[id].Name.Should().Be("ok");
    }

    [Fact]
    public void StatusText_is_empty_when_idle()
    {
        var sut = CreateSut();
        sut.StatusText.Should().BeEmpty();
    }

    [Fact]
    public void StatusText_reflects_section_Error_with_message_appended()
    {
        var sut = CreateSut();
        sut.Streak.ErrorMessage = "boom";
        sut.Streak.Status = EditorStatus.Error;

        sut.StatusText.Should().Be($"{Strings.Editor_Status_Error}: boom");
    }

    [Fact]
    public void Error_wins_over_Saving_in_aggregate()
    {
        var sut = CreateSut();
        sut.Scheduling.Status = EditorStatus.Saving;
        sut.Streak.ErrorMessage = "boom";
        sut.Streak.Status = EditorStatus.Error;

        sut.StatusText.Should().StartWith(Strings.Editor_Status_Error);
    }

    [Fact]
    public void Saved_shown_when_no_error_and_no_saving()
    {
        var sut = CreateSut();
        sut.Goal.Status = EditorStatus.Saved;

        sut.StatusText.Should().Be(Strings.Editor_Status_Saved);
    }

    [Fact]
    public void StatusText_change_raises_PropertyChanged()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Tags.Status = EditorStatus.Saving;

        raised.Should().Contain(nameof(HabitEditorViewModel.StatusText));
    }

    [Fact]
    public async Task Invalid_color_format_errors_and_does_not_persist()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", Color = "#000000" };
        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.Color = "blue";
        await Task.Delay(20);

        sut.HeaderStatus.Should().Be(EditorStatus.Error);
        _nodes.Store[id].Color.Should().Be("#000000");
    }

    [Fact]
    public async Task Sections_exposes_ordered_render_list_excluding_DataSource()
    {
        var sut = CreateSut();

        // Sections property must not contain DataSource
        sut.Sections.Should().NotContain(s => ReferenceEquals(s, sut.DataSource));
    }

    [Fact]
    public async Task LoadAsync_Action_habit_sets_visibility_correctly_via_IsApplicable()
    {
        var id = Guid.NewGuid();
        _nodes.Store[id] = new HabitNode
        {
            Id = id,
            NodeType = NodeType.Habit,
            HabitType = HabitType.Action,
            Name = "run",
            Color = "#000000",
        };
        _configs.Store[id] = new HabitConfig
        {
            Id = Guid.NewGuid(),
            HabitNodeId = id,
            ScheduleType = ScheduleType.Daily,
            StreakMode = StreakMode.Daily,
            StreakRecovery = StreakRecovery.None,
        };

        var sut = CreateSut();
        await sut.LoadAsync(id);

        sut.Scheduling.IsVisible.Should().BeTrue();   // Habit → applicable
        sut.Metrics.IsVisible.Should().BeFalse();      // Counter/Resistance only
        sut.Scales.IsVisible.Should().BeFalse();       // State only
        sut.Sections.Should().NotContain(s => ReferenceEquals(s, sut.DataSource));
    }
}
