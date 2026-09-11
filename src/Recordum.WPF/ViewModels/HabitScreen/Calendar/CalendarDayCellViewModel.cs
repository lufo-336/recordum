using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;

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

    /// <summary>Habit color used as the tint source for this cell (set by the calendar from the host color).
    /// Observable so the tint MultiBinding re-reads it: the navigator assigns it after the cell is bound,
    /// and a non-notifying property would leave the MultiBinding holding the stale default.
    /// <para>This is the habit's own color (a real value, e.g. user-chosen), never a theme token — it stays
    /// populated even on a <see cref="MarkerTint.Danger"/> day. <see cref="Tint"/> is what decides whether
    /// the view renders this color or overrides it with the theme's danger brush.</para></summary>
    [ObservableProperty] private string _tintColorHex = "#808080";

    /// <summary>Which tint the view should render for this cell: the habit color in <see cref="TintColorHex"/>
    /// (<see cref="MarkerTint.Default"/>) or the theme's danger brush (<see cref="MarkerTint.Danger"/>),
    /// resolved by the view against the current theme — never photographed into a hex at load time.
    /// Observable for the same reason as <see cref="TintColorHex"/>.</summary>
    [ObservableProperty] private MarkerTint _tint = MarkerTint.Default;

    public bool HasNote => NoteCount > 0;
    public bool ShowNoteCount => NoteCount > 1;
    public bool HasNumber => !string.IsNullOrEmpty(Number);

    public string TooltipText
    {
        get
        {
            var date = Date.ToString("D", System.Globalization.CultureInfo.CurrentCulture);
            if (!string.IsNullOrEmpty(Number)) return $"{date} · {Number}";
            if (NoteCount > 0) return $"{date} · {NoteCount}";
            return date;
        }
    }

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
        OnPropertyChanged(nameof(TooltipText));
    }

    partial void OnNumberChanged(string? value)
    {
        OnPropertyChanged(nameof(HasNumber));
        OnPropertyChanged(nameof(TooltipText));
    }
}
