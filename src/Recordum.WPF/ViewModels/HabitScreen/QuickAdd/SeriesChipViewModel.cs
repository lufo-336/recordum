using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>One named series chip in the repeatable counter quick-add (U8): "Notte", "Riposino 1", …
/// Index = the series row it fronts; renames are committed by the parent VM (persistence lives there).</summary>
public sealed partial class SeriesChipViewModel(int index, string name) : ObservableObject
{
    public int Index { get; } = index;
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _pendingName = string.Empty;
}
