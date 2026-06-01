using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;

namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>Completed/note day-markers for one month, supplied by the host.</summary>
public sealed record CalendarMonthMarkers(IReadOnlySet<DateOnly> Completed, IReadOnlySet<DateOnly> Notes);

/// <summary>Host-supplied source of month markers (decouples the calendar from the domain).</summary>
public delegate Task<CalendarMonthMarkers> CalendarMarkerProvider(int year, int month, CancellationToken ct);

/// <summary>
/// Reusable month-grid calendar: navigates months/years, renders a 6×7 grid with today/selected
/// highlighting and completed/note markers, and raises <see cref="SelectedDateChanged"/> when a
/// non-future day is picked. Markers come from <see cref="MarkerProvider"/> (set by the host).
/// </summary>
public sealed partial class CalendarNavigatorViewModel : ObservableObject
{
    /// <summary>Snapped once at construction (per-screen lifetime); a calendar left open past midnight
    /// keeps the construction-time "today" until the screen is reloaded. Intentional for day-granularity.</summary>
    private readonly DateOnly _today;

    public CalendarNavigatorViewModel(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone);
        _today = DateOnly.FromDateTime(localNow);
        WeekdayHeaders = BuildWeekdayHeaders();
        SelectableYears = Enumerable.Range(_today.Year - 5, 8).ToList();
    }

    public CalendarMarkerProvider? MarkerProvider { get; set; }

    [ObservableProperty] private string _completedColorHex = "#808080";
    [ObservableProperty] private string _noteColorHex = "#e0b341";
    [ObservableProperty] private int _displayYear;
    [ObservableProperty] private int _displayMonth;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private DateOnly? _selectedDate;

    public ObservableCollection<CalendarDayCellViewModel> Cells { get; } = [];
    public IReadOnlyList<string> WeekdayHeaders { get; }
    public IReadOnlyList<int> SelectableYears { get; }
    public IReadOnlyList<int> SelectableMonths { get; } = Enumerable.Range(1, 12).ToList();

    public event EventHandler<DateOnly>? SelectedDateChanged;

    public async Task LoadAsync(int year, int month, DateOnly? selected, CancellationToken ct = default)
    {
        DisplayYear = year;
        DisplayMonth = month;
        SelectedDate = selected;
        MonthLabel = new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);

        BuildGrid(year, month);
        await ApplyMarkersAsync(year, month, ct).ConfigureAwait(true);
        RefreshSelection();
    }

    private void BuildGrid(int year, int month)
    {
        Cells.Clear();
        var first = new DateOnly(year, month, 1);
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)first.DayOfWeek - firstDow + 7) % 7;
        var start = first.AddDays(-leading);
        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date,
                inCurrentMonth: date.Month == month && date.Year == year,
                isToday: date == _today,
                isFuture: date > _today));
        }
    }

    private async Task ApplyMarkersAsync(int year, int month, CancellationToken ct)
    {
        if (MarkerProvider is null) return;
        var markers = await MarkerProvider(year, month, ct).ConfigureAwait(true);
        foreach (var cell in Cells)
        {
            cell.Completed = markers.Completed.Contains(cell.Date);
            cell.HasNote = markers.Notes.Contains(cell.Date);
        }
    }

    private void RefreshSelection()
    {
        foreach (var cell in Cells)
            cell.IsSelected = SelectedDate is { } d && cell.Date == d;
    }

    [RelayCommand]
    private Task PrevMonthAsync(CancellationToken ct)
    {
        var prev = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(-1);
        return LoadAsync(prev.Year, prev.Month, SelectedDate, ct);
    }

    [RelayCommand]
    private Task NextMonthAsync(CancellationToken ct)
    {
        var next = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(1);
        return LoadAsync(next.Year, next.Month, SelectedDate, ct);
    }

    public Task JumpAsync(CancellationToken ct = default) => LoadAsync(DisplayYear, DisplayMonth, SelectedDate, ct);

    [RelayCommand]
    private void SelectDate(CalendarDayCellViewModel? cell)
    {
        if (cell is null || cell.IsFuture) return;
        SelectedDate = cell.Date;
        RefreshSelection();
        SelectedDateChanged?.Invoke(this, cell.Date);
    }

    private static List<string> BuildWeekdayHeaders()
    {
        var dtf = CultureInfo.CurrentCulture.DateTimeFormat;
        int firstDow = (int)dtf.FirstDayOfWeek;
        var names = new List<string>(7);
        for (int i = 0; i < 7; i++)
            names.Add(dtf.ShortestDayNames[(firstDow + i) % 7]);
        return names;
    }
}
