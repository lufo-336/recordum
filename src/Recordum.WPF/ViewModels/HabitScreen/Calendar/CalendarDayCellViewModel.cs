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
    [ObservableProperty] private bool _completed;
    [ObservableProperty] private bool _hasNote;

    public CalendarDayCellViewModel(DateOnly date, bool inCurrentMonth, bool isToday, bool isFuture)
    {
        Date = date;
        InCurrentMonth = inCurrentMonth;
        IsToday = isToday;
        IsFuture = isFuture;
    }
}
