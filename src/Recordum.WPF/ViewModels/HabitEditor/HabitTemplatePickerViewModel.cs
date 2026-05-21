using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor;

/// <summary>
/// Modal picker shown by the Home "+ New" button. Lists official templates plus two
/// blank options (blank habit / blank group). Picking an option instantiates the entity
/// (cloning the template via <see cref="IHabitTemplateService.InstantiateAsync"/> or creating an
/// empty node) and surfaces the new id via <see cref="CreatedHabitNodeId"/> for the modal host
/// to close itself and load it into the editor.
/// </summary>
public sealed partial class HabitTemplatePickerViewModel(
    IHabitTemplateService templates,
    IHabitNodeService nodes) : ObservableObject
{
    private readonly IHabitTemplateService _templates = templates;
    private readonly IHabitNodeService _nodes = nodes;

    public ObservableCollection<PickerOption> Options { get; } = new();
    [ObservableProperty] private Guid? _createdHabitNodeId;

    public async Task LoadAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        Options.Clear();
        Options.Add(new PickerOption("Blank habit", "Start with no template — an action habit.", null, NodeType.Habit, parentId));
        Options.Add(new PickerOption("Blank group", "Create an empty group to organise habits.", null, NodeType.Group, parentId));
        foreach (var t in await _templates.ListAsync(ct))
            Options.Add(new PickerOption(t.Name, t.Description ?? "", t.Id, NodeType.Habit, parentId));
    }

    [RelayCommand]
    private async Task PickAsync(PickerOption option)
    {
        if (option is null) return;
        if (option.TemplateId is { } tid)
        {
            CreatedHabitNodeId = await _templates.InstantiateAsync(tid, option.ParentId);
            return;
        }
        var draft = new HabitNode
        {
            ParentId = option.ParentId,
            NodeType = option.NodeType,
            HabitType = option.NodeType == NodeType.Habit ? HabitType.Action : null,
            Name = option.NodeType == NodeType.Habit ? "New habit" : "New group",
            Color = "#808080",
        };
        CreatedHabitNodeId = await _nodes.CreateAsync(draft);
    }
}

public sealed record PickerOption(string Name, string Description, Guid? TemplateId, NodeType NodeType, Guid? ParentId);
