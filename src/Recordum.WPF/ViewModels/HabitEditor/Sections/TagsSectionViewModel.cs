using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class TagsSectionViewModel(ITagService tags) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Tags;

    private readonly ITagService _tags = tags;
    internal Guid NodeId { get; private set; }

    /// <summary>All available tags as chips; each carries its own IsSelected state.</summary>
    public ObservableCollection<TagChipViewModel> AvailableTags { get; } = new();

    public async Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        AvailableTags.Clear();
        var allTags = await _tags.ListAsync();
        var attached = await _tags.ListByHabitAsync(node.Id);
        var attachedIds = new HashSet<Guid>(attached.Select(t => t.Id));
        foreach (var t in allTags)
            AvailableTags.Add(new TagChipViewModel(t.Id, t.Name, attachedIds.Contains(t.Id)));
    }

    [RelayCommand]
    private async Task ToggleTagAsync(TagChipViewModel chip)
    {
        if (chip is null || NodeId == Guid.Empty) return;
        chip.IsSelected = !chip.IsSelected;
        var selectedIds = AvailableTags
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .ToArray();
        await ApplyAsync(() => _tags.SetTagsAsync(NodeId, selectedIds).AsTask());
    }
}
