using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>
/// Wraps a Tag for display as a chip button that shows selected/unselected state.
/// </summary>
public sealed partial class TagChipViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Name { get; }

    [ObservableProperty] private bool _isSelected;

    public TagChipViewModel(Guid id, string name, bool isSelected = false)
    {
        Id = id;
        Name = name;
        _isSelected = isSelected;
    }
}
