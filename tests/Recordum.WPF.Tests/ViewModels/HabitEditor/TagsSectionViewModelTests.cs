using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class TagsSectionViewModelTests
{
    private readonly FakeTagService _tags = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    private async Task<TagsSectionViewModel> CreateSutAsync(params string[] availableTagNames)
    {
        foreach (var n in availableTagNames)
            await _tags.CreateAsync(new Tag { Id = Guid.NewGuid(), Name = n });
        var sut = new TagsSectionViewModel(_tags);
        await sut.LoadAsync(_node);
        return sut;
    }

    [Fact]
    public async Task LoadAsync_populates_available_tags()
    {
        var sut = await CreateSutAsync("alpha", "beta");
        sut.AvailableTags.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_sets_IsSelected_for_already_attached_tag()
    {
        // Create two tags, attach one to the node before load.
        var tagA = new Tag { Id = Guid.NewGuid(), Name = "a" };
        var tagB = new Tag { Id = Guid.NewGuid(), Name = "b" };
        await _tags.CreateAsync(tagA);
        await _tags.CreateAsync(tagB);
        await _tags.AttachAsync(_node.Id, tagA.Id);

        var sut = new TagsSectionViewModel(_tags);
        await sut.LoadAsync(_node);

        var chipA = sut.AvailableTags.Single(c => c.Name == "a");
        var chipB = sut.AvailableTags.Single(c => c.Name == "b");
        chipA.IsSelected.Should().BeTrue();
        chipB.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_populates_selected_ids_from_existing_attachments()
    {
        // Legacy-style test: verify the chip for "a" is selected after attach.
        var sut = await CreateSutAsync("a", "b");
        var chipA = sut.AvailableTags.First(c => c.Name == "a");
        await _tags.AttachAsync(_node.Id, chipA.Id);

        // Reload.
        await sut.LoadAsync(_node);

        sut.AvailableTags.Single(c => c.Name == "a").IsSelected.Should().BeTrue();
        sut.AvailableTags.Single(c => c.Name == "b").IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleTagCommand_selects_unselected_chip_and_calls_SetTagsAsync()
    {
        // Arrange: two tags, tag "a" attached, tag "b" not.
        var tagA = new Tag { Id = Guid.NewGuid(), Name = "a" };
        var tagB = new Tag { Id = Guid.NewGuid(), Name = "b" };
        await _tags.CreateAsync(tagA);
        await _tags.CreateAsync(tagB);
        await _tags.AttachAsync(_node.Id, tagA.Id);

        var sut = new TagsSectionViewModel(_tags);
        await sut.LoadAsync(_node);

        var chipB = sut.AvailableTags.Single(c => c.Name == "b");
        chipB.IsSelected.Should().BeFalse();

        // Act: toggle "b" on.
        await sut.ToggleTagCommand.ExecuteAsync(chipB);

        // Assert: chip is now selected, both ids persisted.
        chipB.IsSelected.Should().BeTrue();
        _tags.Attachments.Should().Contain((_node.Id, tagA.Id));
        _tags.Attachments.Should().Contain((_node.Id, tagB.Id));
    }

    [Fact]
    public async Task ToggleTagCommand_adds_then_removes_via_SetTagsAsync()
    {
        var sut = await CreateSutAsync("a");
        var chip = sut.AvailableTags.Single();

        await sut.ToggleTagCommand.ExecuteAsync(chip);
        chip.IsSelected.Should().BeTrue();
        _tags.Attachments.Should().Contain((_node.Id, chip.Id));

        await sut.ToggleTagCommand.ExecuteAsync(chip);
        chip.IsSelected.Should().BeFalse();
        _tags.Attachments.Should().NotContain((_node.Id, chip.Id));
    }
}
