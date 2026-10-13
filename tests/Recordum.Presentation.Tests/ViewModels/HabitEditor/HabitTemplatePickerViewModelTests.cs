using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.Resources;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class HabitTemplatePickerViewModelTests
{
    private readonly FakeHabitTemplateService _templates = new();
    private readonly FakeHabitNodeService _nodes = new();

    private readonly FakeConfirmationDialog _confirm = new();

    private HabitTemplatePickerViewModel CreateSut() => new(_templates, _nodes, _confirm);

    [Fact]
    public async Task LoadAsync_separates_pinned_actions_from_official_templates()
    {
        for (var i = 0; i < 8; i++)
            await _templates.CreateAsync(new HabitTemplate { Name = $"t{i}", IsOfficial = true });
        var sut = CreateSut();

        await sut.LoadAsync();

        // The two primary actions live in their own pinned collection, out of the collapsible sections.
        sut.Actions.Select(a => a.Name).Should().Equal(
            Strings.HabitTemplatePicker_FromScratch, Strings.HabitTemplatePicker_BlankGroup);
        sut.Actions.Should().OnlyContain(a => a.TemplateId == null);

        // Official templates only — no action entries mixed in.
        sut.OfficialTemplates.Should().HaveCount(8);
        sut.OfficialTemplates.Should().OnlyContain(o => o.TemplateId != null && !o.IsCustom);
        sut.CustomOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_splits_custom_templates_into_their_own_section()
    {
        await _templates.CreateAsync(new HabitTemplate { Name = "official", IsOfficial = true });
        await _templates.CreateAsync(new HabitTemplate { Name = "mine", IsOfficial = false });
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.OfficialTemplates.Select(o => o.Name).Should().Contain("official").And.NotContain("mine");
        sut.CustomOptions.Should().ContainSingle(o => o.Name == "mine" && o.IsCustom);
    }

    [Fact]
    public async Task RenameCustom_renames_template_and_reloads()
    {
        var id = await _templates.CreateAsync(new HabitTemplate { Name = "old", IsOfficial = false });
        var sut = CreateSut();
        await sut.LoadAsync();
        var option = sut.CustomOptions.Single();

        await sut.RenameCustomCommand.ExecuteAsync((option, "new"));

        _templates.Store[id].Name.Should().Be("new");
        sut.CustomOptions.Should().ContainSingle(o => o.Name == "new");
    }

    [Fact]
    public async Task DeleteCustom_deletes_template_and_reloads()
    {
        var id = await _templates.CreateAsync(new HabitTemplate { Name = "doomed", IsOfficial = false });
        var sut = CreateSut();
        await sut.LoadAsync();
        var option = sut.CustomOptions.Single();

        await sut.DeleteCustomCommand.ExecuteAsync(option);

        _confirm.Calls.Should().Be(1);
        _templates.Store.Should().NotContainKey(id);
        sut.CustomOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task PickCommand_for_template_calls_InstantiateAsync_with_parentId()
    {
        var parentId = Guid.NewGuid();
        var tId = await _templates.CreateAsync(new HabitTemplate { Name = "x", IsOfficial = true });
        var sut = CreateSut();
        await sut.LoadAsync(parentId: parentId);

        var option = sut.OfficialTemplates.Single(o => o.TemplateId == tId);
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

        var blank = sut.Actions.Single(o => o.Name == Strings.HabitTemplatePicker_FromScratch);
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

        var blank = sut.Actions.Single(o => o.Name == Strings.HabitTemplatePicker_BlankGroup);
        await sut.PickCommand.ExecuteAsync(blank);

        _nodes.Store[sut.CreatedHabitNodeId!.Value].NodeType.Should().Be(NodeType.Group);
        _nodes.Store[sut.CreatedHabitNodeId.Value].HabitType.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCustom_asks_first_and_cancelling_keeps_the_template()
    {
        var id = await _templates.CreateAsync(new HabitTemplate { Name = "mine", IsOfficial = false });
        var sut = CreateSut();
        await sut.LoadAsync();
        _confirm.Result = false;

        await sut.DeleteCustomCommand.ExecuteAsync(sut.CustomOptions.Single());

        _confirm.Calls.Should().Be(1);
        _templates.Store.Should().ContainKey(id);
        sut.CustomOptions.Should().ContainSingle();
    }

    private sealed class GatedTemplates(FakeHabitTemplateService inner) : IHabitTemplateService
    {
        public TaskCompletionSource Gate { get; } = new();
        public int Instantiations;
        public async ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Instantiations);
            await Gate.Task;
            return await inner.InstantiateAsync(templateId, parentId, ct);
        }
        public ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default) => inner.CreateAsync(draft, ct);
        public ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default) => inner.UpdateAsync(id, mutate, ct);
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default) => inner.GetAsync(id, ct);
        public ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
    }

    [Fact]
    public async Task A_second_pick_while_the_first_is_in_flight_creates_nothing_more()
    {
        var gated = new GatedTemplates(_templates);
        await _templates.CreateAsync(new HabitTemplate { Name = "t", IsOfficial = true });
        var sut = new HabitTemplatePickerViewModel(gated, _nodes, _confirm);
        await sut.LoadAsync();
        var option = sut.OfficialTemplates.Single();

        var first = sut.PickCommand.ExecuteAsync(option);
        var second = sut.PickCommand.ExecuteAsync(option);
        gated.Gate.SetResult();
        await Task.WhenAll(first, second);

        gated.Instantiations.Should().Be(1);
        _templates.InstantiateCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_failed_pick_releases_the_guard_so_the_user_can_retry()
    {
        await _templates.CreateAsync(new HabitTemplate { Name = "t", IsOfficial = true });
        var sut = CreateSut();
        await sut.LoadAsync();
        var option = sut.OfficialTemplates.Single();

        _templates.NextException = new InvalidOperationException("boom");
        try { await sut.PickCommand.ExecuteAsync(option); } catch (InvalidOperationException) { }
        await sut.PickCommand.ExecuteAsync(option);

        sut.CreatedHabitNodeId.Should().NotBeNull();
        _templates.InstantiateCalls.Should().HaveCount(1);
    }
}
