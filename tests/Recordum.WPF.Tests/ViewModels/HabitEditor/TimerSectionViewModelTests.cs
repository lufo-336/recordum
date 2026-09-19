using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class TimerSectionViewModelTests
{
    private readonly FakeHabitConfigService _configs = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter };

    private TimerSectionViewModel CreateSut()
    {
        var sut = new TimerSectionViewModel(_configs);
        sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id }).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task Enabling_integrated_timer_persists()
    {
        var sut = CreateSut();
        sut.HasIntegratedTimer = true;
        await Task.Delay(20);
        _configs.Store[_node.Id].HasIntegratedTimer.Should().BeTrue();
    }

    [Fact]
    public async Task Session_mode_without_segments_is_error()
    {
        var sut = CreateSut();
        sut.TimerMode = TimerMode.Session;
        await Task.Delay(20);
        sut.Status.Should().Be(EditorStatus.Error);
        _configs.Store.TryGetValue(_node.Id, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Session_mode_with_segments_sets_SessionBaseTimer_and_persists()
    {
        var sut = CreateSut();
        await sut.AddSegmentCommand.ExecuteAsync(null);

        sut.TimerMode = TimerMode.Session;
        await Task.Delay(20);

        _configs.Store[_node.Id].TimerMode.Should().Be(TimerMode.Session);
        _configs.Store[_node.Id].SessionBaseTimer.Should().BeTrue();
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task AddSegmentCommand_appends_to_collection_and_service()
    {
        var sut = CreateSut();
        await sut.AddSegmentCommand.ExecuteAsync(null);
        sut.Segments.Should().HaveCount(1);
        _configs.Segments.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveSegmentCommand_removes_from_collection_and_service()
    {
        var sut = CreateSut();
        await sut.AddSegmentCommand.ExecuteAsync(null);
        var row = sut.Segments[0];

        await sut.RemoveSegmentCommand.ExecuteAsync(row);

        sut.Segments.Should().BeEmpty();
        _configs.Segments.Should().BeEmpty();
    }

    [Fact]
    public async Task Renaming_segment_calls_UpdateSegmentAsync()
    {
        var sut = CreateSut();
        await sut.AddSegmentCommand.ExecuteAsync(null);
        var row = sut.Segments[0];

        row.Name = "Work";
        await Task.Delay(20);

        _configs.Segments.Single().Name.Should().Be("Work");
    }

    [Fact]
    public void IsApplicable_only_for_Counter_and_Resistance()
    {
        var sut = new TimerSectionViewModel(_configs);
        sut.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeFalse();
        sut.IsApplicable(NodeType.Habit, HabitType.Journal).Should().BeFalse();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeFalse();
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }
}
