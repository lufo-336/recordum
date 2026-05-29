using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.Resources;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class HabitTemplatePickerViewModelTests
{
    private readonly FakeHabitTemplateService _templates = new();
    private readonly FakeHabitNodeService _nodes = new();

    private HabitTemplatePickerViewModel CreateSut() => new(_templates, _nodes);

    [Fact]
    public async Task LoadAsync_lists_blanks_plus_official_templates()
    {
        for (var i = 0; i < 8; i++)
            await _templates.CreateAsync(new HabitTemplate { Name = $"t{i}", IsOfficial = true });
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.Options.Should().HaveCount(10);
        sut.Options[0].Name.Should().Be(Strings.HabitTemplatePicker_BlankHabit);
        sut.Options[1].Name.Should().Be(Strings.HabitTemplatePicker_BlankGroup);
    }

    [Fact]
    public async Task PickCommand_for_template_calls_InstantiateAsync_with_parentId()
    {
        var parentId = Guid.NewGuid();
        var tId = await _templates.CreateAsync(new HabitTemplate { Name = "x" });
        var sut = CreateSut();
        await sut.LoadAsync(parentId: parentId);

        var option = sut.Options.Single(o => o.TemplateId == tId);
        await sut.PickCommand.ExecuteAsync(option);

        sut.CreatedHabitNodeId.Should().NotBeNull();
        _templates.InstantiateCalls.Should().ContainSingle().Which.Should().Match<(Guid t, Guid? p, Guid n)>(
            x => x.t == tId && x.p == parentId);
    }

    [Fact]
    public async Task PickCommand_for_blank_habit_creates_node_via_HabitNodeService()
    {
        var sut = CreateSut();
        await sut.LoadAsync();

        var blank = sut.Options.Single(o => o.Name == Strings.HabitTemplatePicker_BlankHabit);
        await sut.PickCommand.ExecuteAsync(blank);

        sut.CreatedHabitNodeId.Should().NotBeNull();
        _nodes.Store[sut.CreatedHabitNodeId!.Value].NodeType.Should().Be(NodeType.Habit);
        _nodes.Store[sut.CreatedHabitNodeId.Value].HabitType.Should().Be(HabitType.Action);
    }

    [Fact]
    public async Task PickCommand_for_blank_group_creates_group_node()
    {
        var sut = CreateSut();
        await sut.LoadAsync();

        var blank = sut.Options.Single(o => o.Name == Strings.HabitTemplatePicker_BlankGroup);
        await sut.PickCommand.ExecuteAsync(blank);

        _nodes.Store[sut.CreatedHabitNodeId!.Value].NodeType.Should().Be(NodeType.Group);
        _nodes.Store[sut.CreatedHabitNodeId.Value].HabitType.Should().BeNull();
    }
}
