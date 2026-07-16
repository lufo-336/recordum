using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class RegistrationSectionTests
{
    [Fact]
    public async Task Toggle_persists_repeatable_on_the_config()
    {
        var configs = new FakeHabitConfigService();
        var section = new RegistrationSectionViewModel(configs);
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" };
        await section.LoadAsync(node);

        section.Repeatable = true;
        await Task.Delay(50); // ApplyAsync is fire-and-forget in sections

        (await configs.GetAsync(node.Id))!.Repeatable.Should().BeTrue();
    }

    [Fact]
    public void Applicable_only_to_counter_habits()
    {
        var section = new RegistrationSectionViewModel(new FakeHabitConfigService());
        section.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
        section.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeFalse();
        section.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_does_not_persist_the_loaded_value_back(/* guards against the 4d01f9c data-wipe class of bug */)
    {
        var configs = new FakeHabitConfigService();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" };
        await configs.UpsertAsync(node.Id, c => c.Repeatable = true);
        var section = new RegistrationSectionViewModel(configs);

        await section.LoadAsync(node);

        section.Repeatable.Should().BeTrue();
        await Task.Delay(50);
        // LoadAsync must not have fired a spurious persist while _loading was true — reloading
        // with a fresh section instance should still see the same value, not a clobbered default.
        (await configs.GetAsync(node.Id))!.Repeatable.Should().BeTrue();
    }
}
