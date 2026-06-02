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
    public async Task PickCommand_for_blank_habit_requests_builder_instead_of_creating_node()
    {
        var parentId = Guid.NewGuid();
        var sut = CreateSut();
        await sut.LoadAsync(parentId: parentId);

        var blank = sut.Options.Single(o => o.Name == Strings.HabitTemplatePicker_BlankHabit);
        await sut.PickCommand.ExecuteAsync(blank);

        // "Build from scratch" no longer creates a node directly; it signals the host to open the builder.
        sut.OpenBuilderRequested.Should().BeTrue();
        sut.ParentForBuilder.Should().Be(parentId);
        sut.CreatedHabitNodeId.Should().BeNull();
        _nodes.Store.Should().BeEmpty();
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
