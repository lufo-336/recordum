using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class ResetTimeSectionViewModelTests
{
    private readonly FakeHabitNodeService _nodes = new();

    private (ResetTimeSectionViewModel sut, HabitNode node) Create(TimeOnly? initial = null)
    {
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", ResetTime = initial };
        _nodes.Store[node.Id] = node;
        var sut = new ResetTimeSectionViewModel(_nodes);
        sut.LoadAsync(node).GetAwaiter().GetResult();
        return (sut, node);
    }

    [Fact]
    public async Task Valid_HHmm_text_persists_to_HabitNode_ResetTime()
    {
        var (sut, node) = Create();
        sut.ResetTimeText = "06:30";
        await Task.Delay(20);
        _nodes.Store[node.Id].ResetTime.Should().Be(new TimeOnly(6, 30));
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task Empty_text_clears_ResetTime()
    {
        var (sut, node) = Create(new TimeOnly(5, 0));
        sut.ResetTimeText = "";
        await Task.Delay(20);
        _nodes.Store[node.Id].ResetTime.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_text_errors_and_does_not_persist()
    {
        var (sut, node) = Create(new TimeOnly(8, 0));
        sut.ResetTimeText = "25:99";
        await Task.Delay(20);
        sut.Status.Should().Be(EditorStatus.Error);
        _nodes.Store[node.Id].ResetTime.Should().Be(new TimeOnly(8, 0));
    }

    [Fact]
    public void LoadAsync_initializes_text_from_existing_ResetTime()
    {
        var (sut, _) = Create(new TimeOnly(7, 5));
        sut.ResetTimeText.Should().Be("07:05");
    }
}
