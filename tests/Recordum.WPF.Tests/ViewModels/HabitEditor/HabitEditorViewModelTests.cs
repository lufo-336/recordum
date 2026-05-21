using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class HabitEditorViewModelTests
{
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakeHabitConfigService _configs = new();
    private readonly FakeGoalService _goals = new();
    private readonly FakeMetricDefinitionService _metrics = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeTagService _tags = new();
    private readonly FakeAppSettingService _appSettings = new();

    private HabitEditorViewModel CreateSut() => new(_nodes, _configs, _goals, _metrics, _notifications, _tags, _appSettings);

    [Fact]
    public async Task LoadAsync_with_null_clears_node()
    {
        var sut = CreateSut();
        await sut.LoadAsync(null);
        sut.HasNode.Should().BeFalse();
        sut.Node.Should().BeNull();
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
}
