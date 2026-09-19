using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class GoalSectionViewModelTests
{
    private readonly FakeGoalService _goals = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter };

    private GoalSectionViewModel CreateSut()
    {
        var sut = new GoalSectionViewModel(_goals);
        sut.LoadAsync(_node).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task Setting_valid_target_persists_via_UpsertActiveAsync()
    {
        var sut = CreateSut();
        sut.TargetValue = 30m;
        await Task.Delay(20);
        var active = await _goals.GetActiveAsync(_node.Id);
        active!.TargetValue.Should().Be(30m);
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task Negative_target_errors_and_does_not_persist()
    {
        var sut = CreateSut();
        sut.TargetValue = -1m;
        await Task.Delay(20);
        sut.Status.Should().Be(EditorStatus.Error);
        (await _goals.GetActiveAsync(_node.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Rolling_period_requires_RollingDays_above_zero()
    {
        var sut = CreateSut();
        sut.PeriodType = GoalPeriodType.Rolling;
        await Task.Delay(20);
        sut.RollingDays = 0;
        await Task.Delay(20);
        sut.Status.Should().Be(EditorStatus.Error);
    }

    [Fact]
    public async Task Setting_PeriodType_persists()
    {
        var sut = CreateSut();
        sut.PeriodType = GoalPeriodType.Fixed;
        await Task.Delay(20);
        (await _goals.GetActiveAsync(_node.Id))!.PeriodType.Should().Be(GoalPeriodType.Fixed);
    }

    [Fact]
    public void IsApplicable_is_true_for_all_node_types()
    {
        var sut = new GoalSectionViewModel(_goals);
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Group, null).Should().BeTrue();
    }
}
