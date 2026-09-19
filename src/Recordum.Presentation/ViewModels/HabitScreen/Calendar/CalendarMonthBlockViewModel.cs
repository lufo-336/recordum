using System.Collections.ObjectModel;

namespace Recordum.Presentation.ViewModels.HabitScreen.Calendar;

/// <summary>One month block in the Year view: a month label + a compact 6×7 day grid.
/// Out-of-month cells (<see cref="CalendarDayCellViewModel.InCurrentMonth"/> = false) are kept for
/// week alignment but rendered blank by the template.</summary>
public sealed class CalendarMonthBlockViewModel
{
    public int Year { get; }
    public int Month { get; }
    public string MonthName { get; }
    public ObservableCollection<CalendarDayCellViewModel> Cells { get; } = [];

    public CalendarMonthBlockViewModel(int year, int month, string monthName)
    {
        Year = year;
        Month = month;
        MonthName = monthName;
    }
}
