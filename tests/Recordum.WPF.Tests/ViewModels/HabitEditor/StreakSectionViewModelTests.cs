using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class StreakSectionViewModelTests
{
    private readonly FakeHabitConfigService _configs = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    private StreakSectionViewModel CreateSut()
    {
        var sut = new StreakSectionViewModel(_configs);
        sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id }).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task Changing_StreakMode_persists()
    {
        var sut = CreateSut();
        sut.StreakMode = StreakMode.Weekly;
        await Task.Delay(20);
        _configs.Store[_node.Id].StreakMode.Should().Be(StreakMode.Weekly);
    }

    [Fact]
    public async Task Changing_StreakRecovery_persists()
    {
        var sut = CreateSut();
        sut.StreakRecovery = StreakRecovery.GraceDay;
        await Task.Delay(20);
        _configs.Store[_node.Id].StreakRecovery.Should().Be(StreakRecovery.GraceDay);
    }

    [Fact]
    public async Task Tolerance_of_zero_errors_and_does_not_persist()
    {
        var sut = CreateSut();
        sut.StreakToleranceMinutes = 0;
        await Task.Delay(20);
        sut.Status.Should().Be(EditorStatus.Error);
        _configs.Store.TryGetValue(_node.Id, out var cfg).Should().BeFalse();
    }

    [Fact]
    public async Task Tolerance_above_1440_errors()
    {
        var sut = CreateSut();
        sut.StreakToleranceMinutes = 2000;
        await Task.Delay(20);
        sut.Status.Should().Be(EditorStatus.Error);
    }

    [Fact]
    public async Task Tolerance_in_range_persists()
    {
        var sut = CreateSut();
        sut.StreakToleranceMinutes = 60;
        await Task.Delay(20);
        _configs.Store[_node.Id].StreakToleranceMinutes.Should().Be(60);
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public void IsApplicable_is_false_for_group()
    {
        new StreakSectionViewModel(_configs).IsApplicable(NodeType.Group, null).Should().BeFalse();
    }
}
