using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class AiSharingSectionViewModelTests
{
    private readonly FakeHabitNodeService _nodes = new();

    private AiSharingSectionViewModel CreateSut(HabitNode node)
    {
        _nodes.Store[node.Id] = node;
        var sut = new AiSharingSectionViewModel(_nodes);
        sut.LoadAsync(node).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task Changing_value_persists_via_HabitNodeService()
    {
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", AiDataSharing = AiDataSharing.None };
        var sut = CreateSut(node);

        sut.AiDataSharing = AiDataSharing.LocalOnly;
        await Task.Delay(20);

        _nodes.Store[node.Id].AiDataSharing.Should().Be(AiDataSharing.LocalOnly);
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task LoadAsync_reflects_current_node_value()
    {
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", AiDataSharing = AiDataSharing.ApiAllowed };
        var sut = CreateSut(node);
        sut.AiDataSharing.Should().Be(AiDataSharing.ApiAllowed);
    }
}
