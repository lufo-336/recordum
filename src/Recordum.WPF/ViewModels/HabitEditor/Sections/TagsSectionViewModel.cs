using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class TagsSectionViewModel(ITagService tags) : SectionViewModelBase
{
    private readonly ITagService _tags = tags;
    internal Guid NodeId { get; private set; }

    public ObservableCollection<Tag> AvailableTags { get; } = new();
    [ObservableProperty] private ImmutableHashSet<Guid> _selectedTagIds = ImmutableHashSet<Guid>.Empty;

    public async Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        AvailableTags.Clear();
        foreach (var t in await _tags.ListAsync()) AvailableTags.Add(t);
        var attached = await _tags.ListByHabitAsync(node.Id);
        SelectedTagIds = attached.Select(t => t.Id).ToImmutableHashSet();
    }

    [RelayCommand]
    private async Task ToggleTagAsync(Tag tag)
    {
        if (tag is null || NodeId == Guid.Empty) return;
        SelectedTagIds = SelectedTagIds.Contains(tag.Id)
            ? SelectedTagIds.Remove(tag.Id)
            : SelectedTagIds.Add(tag.Id);
        await ApplyAsync(() => _tags.SetTagsAsync(NodeId, SelectedTagIds.ToArray()).AsTask());
    }

    public bool IsTagSelected(Guid tagId) => SelectedTagIds.Contains(tagId);
}
