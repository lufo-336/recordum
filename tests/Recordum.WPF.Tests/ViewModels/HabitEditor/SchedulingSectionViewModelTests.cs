using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class SchedulingSectionViewModelTests
{
    private readonly FakeHabitConfigService _configs = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    private SchedulingSectionViewModel CreateSut()
    {
        var sut = new SchedulingSectionViewModel(_configs);
        sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id, ScheduleType = ScheduleType.Daily }).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task Changing_ScheduleType_persists_via_config_service()
    {
        var sut = CreateSut();
        sut.ScheduleType = ScheduleType.WeeklyX;
        await Task.Delay(20);

        _configs.Store[_node.Id].ScheduleType.Should().Be(ScheduleType.WeeklyX);
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task EveryXDays_with_zero_value_under_EveryXDays_schedule_is_error()
    {
        var sut = CreateSut();
        sut.ScheduleType = ScheduleType.EveryXDays;
        await Task.Delay(20);

        sut.ScheduleEveryXDays = 0;
        await Task.Delay(20);

        sut.Status.Should().Be(EditorStatus.Error);
        _configs.Store[_node.Id].ScheduleEveryXDays.Should().BeNull();
    }

    [Fact]
    public async Task EveryXDays_positive_value_persists()
    {
        var sut = CreateSut();
        sut.ScheduleType = ScheduleType.EveryXDays;
        sut.ScheduleEveryXDays = 3;
        await Task.Delay(20);

        _configs.Store[_node.Id].ScheduleEveryXDays.Should().Be(3);
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task WeeklyTarget_with_zero_under_WeeklyX_schedule_is_error()
    {
        var sut = CreateSut();
        sut.ScheduleType = ScheduleType.WeeklyX;
        await Task.Delay(20);

        sut.ScheduleWeeklyTarget = 0;
        await Task.Delay(20);

        sut.Status.Should().Be(EditorStatus.Error);
    }

    [Fact]
    public void IsApplicable_is_false_for_group()
    {
        var sut = new SchedulingSectionViewModel(_configs);
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public void IsApplicable_is_true_for_any_habit()
    {
        var sut = new SchedulingSectionViewModel(_configs);
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeTrue();
    }
}
