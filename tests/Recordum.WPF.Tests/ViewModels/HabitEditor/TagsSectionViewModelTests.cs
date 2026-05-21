using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
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
    public async Task LoadAsync_populates_selected_ids_from_existing_attachments()
    {
        var sut = await CreateSutAsync("a", "b");
        var tagA = sut.AvailableTags.First(t => t.Name == "a");
        await _tags.AttachAsync(_node.Id, tagA.Id);

        // Reload.
        await sut.LoadAsync(_node);

        sut.SelectedTagIds.Should().BeEquivalentTo(new[] { tagA.Id });
    }

    [Fact]
    public async Task ToggleTagCommand_adds_then_removes_via_SetTagsAsync()
    {
        var sut = await CreateSutAsync("a");
        var tag = sut.AvailableTags.Single();

        await sut.ToggleTagCommand.ExecuteAsync(tag);
        sut.SelectedTagIds.Should().Contain(tag.Id);
        _tags.Attachments.Should().Contain((_node.Id, tag.Id));

        await sut.ToggleTagCommand.ExecuteAsync(tag);
        sut.SelectedTagIds.Should().NotContain(tag.Id);
        _tags.Attachments.Should().NotContain((_node.Id, tag.Id));
    }
}
