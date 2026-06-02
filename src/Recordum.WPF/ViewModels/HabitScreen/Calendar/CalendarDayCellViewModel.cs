using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>One day cell in the <see cref="CalendarNavigatorViewModel"/> month grid.</summary>
public sealed partial class CalendarDayCellViewModel : ObservableObject
{
    public DateOnly Date { get; }
    public int DayNumber => Date.Day;
    public bool InCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsFuture { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _tintStep;
    [ObservableProperty] private string? _number;
    [ObservableProperty] private int _noteCount;

    /// <summary>Habit color used as the tint source for this cell (set by the calendar from the host color).</summary>
    public string TintColorHex { get; set; } = "#808080";

    public bool HasNote => NoteCount > 0;
    public bool ShowNoteCount => NoteCount > 1;
    public bool HasNumber => !string.IsNullOrEmpty(Number);

    public CalendarDayCellViewModel(DateOnly date, bool inCurrentMonth, bool isToday, bool isFuture)
    {
        Date = date;
        InCurrentMonth = inCurrentMonth;
        IsToday = isToday;
        IsFuture = isFuture;
    }

    partial void OnNoteCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(ShowNoteCount));
    }

    partial void OnNumberChanged(string? value) => OnPropertyChanged(nameof(HasNumber));
}
