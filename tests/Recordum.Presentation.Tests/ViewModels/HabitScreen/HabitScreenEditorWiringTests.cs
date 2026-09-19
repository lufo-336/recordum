using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>
/// The Configura column hosts the screen's own HabitEditorViewModel: it must load the current
/// node when the column opens and when the screen loads with the column already open. This
/// wiring lived in MainWindowViewModel until the workspace shell (fdb6c2e) and was lost in
/// the move — the column rendered a never-loaded editor (empty name, "azione", no sections).
/// </summary>
public class HabitScreenEditorWiringTests
{
    private static Guid AddHabit(FakeHabitNodeService nodes, HabitType type = HabitType.Counter)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "h", Color = "#808080" };
        return id;
    }

    [Fact]
    public async Task Opening_Configura_loads_the_editor_for_the_current_node()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var h = AddHabit(nodes);
        await sut.LoadAsync(h);

        sut.IsConfiguraOpen = true;
        await Task.Delay(50); // the flag-change load is fire-and-forget

        sut.Editor.Node.Should().NotBeNull();
        sut.Editor.Node!.Id.Should().Be(h);
    }

    [Fact]
    public async Task Loading_a_node_with_Configura_already_open_loads_the_editor()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var h = AddHabit(nodes);
        sut.IsConfiguraOpen = true; // e.g. restored open before the subject loads

        await sut.LoadAsync(h);

        sut.Editor.Node.Should().NotBeNull();
        sut.Editor.Node!.Id.Should().Be(h);
    }

    [Fact]
    public async Task Switching_node_with_Configura_open_reloads_the_editor()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var a = AddHabit(nodes);
        var b = AddHabit(nodes, HabitType.State);
        await sut.LoadAsync(a);
        sut.IsConfiguraOpen = true;
        await Task.Delay(50);

        await sut.LoadAsync(b);

        sut.Editor.Node!.Id.Should().Be(b);
    }
}
