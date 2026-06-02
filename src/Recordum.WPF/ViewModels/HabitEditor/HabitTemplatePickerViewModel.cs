using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

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

    /// <summary>User-saved templates (<see cref="HabitTemplate.IsOfficial"/> == false), shown in a
    /// separate "Your templates" section with rename/delete affordances.</summary>
    public ObservableCollection<PickerOption> CustomOptions { get; } = new();

    [ObservableProperty] private Guid? _createdHabitNodeId;

    /// <summary>Set when the "build from scratch" option is picked: the host closes the picker
    /// and opens the block-first builder instead of creating a node directly.</summary>
    [ObservableProperty] private bool _openBuilderRequested;

    /// <summary>Parent for the node the builder will create (carried from <see cref="LoadAsync"/>).</summary>
    public Guid? ParentForBuilder { get; private set; }

    public async Task LoadAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        ParentForBuilder = parentId;
        Options.Clear();
        CustomOptions.Clear();
        Options.Add(new PickerOption(Strings.HabitTemplatePicker_FromScratch, Strings.HabitTemplatePicker_BlankHabit_Description, null, NodeType.Habit, parentId));
        Options.Add(new PickerOption(Strings.HabitTemplatePicker_BlankGroup, Strings.HabitTemplatePicker_BlankGroup_Description, null, NodeType.Group, parentId));
        foreach (var t in await _templates.ListAsync(ct))
        {
            var option = new PickerOption(t.Name, t.Description ?? "", t.Id, NodeType.Habit, parentId, !t.IsOfficial);
            if (t.IsOfficial)
                Options.Add(option);
            else
                CustomOptions.Add(option);
        }
    }

    /// <summary>Renames a custom template, then reloads so both sections reflect the change.</summary>
    [RelayCommand]
    private async Task RenameCustomAsync((PickerOption Option, string NewName) args)
    {
        if (args.Option?.TemplateId is not { } id || !args.Option.IsCustom) return;
        if (string.IsNullOrWhiteSpace(args.NewName)) return;
        var name = args.NewName.Trim();
        await _templates.UpdateAsync(id, t => t.Name = name);
        await LoadAsync(ParentForBuilder);
    }

    /// <summary>Deletes a custom template, then reloads the "Your templates" section.</summary>
    [RelayCommand]
    private async Task DeleteCustomAsync(PickerOption option)
    {
        if (option?.TemplateId is not { } id || !option.IsCustom) return;
        await _templates.DeleteAsync(id);
        await LoadAsync(ParentForBuilder);
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
        if (option.NodeType == NodeType.Habit)
        {
            // "Build from scratch": don't create a node here — signal the host to open the builder.
            ParentForBuilder = option.ParentId;
            OpenBuilderRequested = true;
            return;
        }
        var draft = new HabitNode
        {
            ParentId = option.ParentId,
            NodeType = option.NodeType,
            HabitType = option.NodeType == NodeType.Habit ? HabitType.Action : null,
            Name = option.NodeType == NodeType.Habit ? Strings.Editor_Default_NewHabit : Strings.Editor_Default_NewGroup,
            Color = "#808080",
        };
        CreatedHabitNodeId = await _nodes.CreateAsync(draft);
    }
}

public sealed record PickerOption(string Name, string Description, Guid? TemplateId, NodeType NodeType, Guid? ParentId, bool IsCustom = false);
