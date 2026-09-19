using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>
/// Represents a single day-of-week toggle in the SpecificDays schedule picker.
/// The day key uses the 3-letter convention ("Mon","Tue","Wed","Thu","Fri","Sat","Sun")
/// that matches ScheduleEvaluator / ScheduleDaysJson.
/// </summary>
public sealed partial class DayToggleViewModel : ObservableObject
{
    /// <summary>3-letter key matching ScheduleDaysJson convention (e.g. "Mon", "Tue").</summary>
    public string DayKey { get; }

    /// <summary>Localized short label shown in the button.</summary>
    public string Label { get; }

    [ObservableProperty] private bool _isSelected;

    /// <summary>Raised after IsSelected changes (used by the parent VM to re-serialize).</summary>
    public event EventHandler? IsSelectedChanged;

    public DayToggleViewModel(string dayKey, string label, bool isSelected = false)
    {
        DayKey = dayKey;
        Label = label;
        _isSelected = isSelected;
    }

    partial void OnIsSelectedChanged(bool value) => IsSelectedChanged?.Invoke(this, EventArgs.Empty);
}
