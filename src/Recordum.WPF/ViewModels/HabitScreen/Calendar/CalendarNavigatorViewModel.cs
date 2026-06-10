using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;

namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>Presentation-ready marker per un giorno (il host calcola tint/numero/note).</summary>
public sealed record CalendarDayMarker(DateOnly Date, int TintStep, string? Number, int NoteCount);

/// <summary>Day-markers for a date range, supplied by the host.</summary>
public sealed record CalendarMarkers(IReadOnlyList<CalendarDayMarker> Days);

/// <summary>Host-supplied source of markers for an inclusive date range (decouples the calendar from the domain).</summary>
public delegate Task<CalendarMarkers> CalendarMarkerProvider(DateOnly from, DateOnly to, CancellationToken ct);

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
    private bool _isLoading;

    public CalendarNavigatorViewModel(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone);
        _today = DateOnly.FromDateTime(localNow);
        WeekdayHeaders = BuildWeekdayHeaders();
        SelectableYears = Enumerable.Range(_today.Year - 15, 17).ToList(); // 15 back, current, 1 forward
    }

    public CalendarMarkerProvider? MarkerProvider { get; set; }

    [ObservableProperty] private string _completedColorHex = "#808080";
    [ObservableProperty] private string _noteColorHex = "#e0b341";
    [ObservableProperty] private int _displayYear;
    [ObservableProperty] private int _displayMonth;
    [ObservableProperty] private string _headerLabel = string.Empty;
    [ObservableProperty] private DateOnly? _selectedDate;
    [ObservableProperty] private CalendarViewMode _viewMode = CalendarViewMode.Month;
    private DateOnly _weekAnchor;

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
        _weekAnchor = selected ?? _today;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Rebuilds the grid for the current view mode, applies markers for the visible span, and
    /// re-resolves the selection. All navigation/jump/view-mode changes funnel through here.</summary>
    private async Task ReloadAsync(CancellationToken ct)
    {
        _isLoading = true;
        try
        {
            switch (ViewMode)
            {
                case CalendarViewMode.Week: BuildWeekGrid(_weekAnchor); break;
                case CalendarViewMode.Year: BuildYearGrid(DisplayYear); break;
                default: BuildMonthGrid(DisplayYear, DisplayMonth); break;
            }
            HeaderLabel = BuildHeaderLabel();
            await ApplyMarkersAsync(ct).ConfigureAwait(true);
            RefreshSelection();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BuildWeekGrid(DateOnly anchor)
    {
        Cells.Clear();
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)anchor.DayOfWeek - firstDow + 7) % 7;
        var start = anchor.AddDays(-leading);
        for (int i = 0; i < 7; i++)
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date, inCurrentMonth: true, isToday: date == _today, isFuture: date > _today));
        }
    }

    private void BuildYearGrid(int year)
    {
        Cells.Clear();
        var jan1 = new DateOnly(year, 1, 1);
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)jan1.DayOfWeek - firstDow + 7) % 7;
        var start = jan1.AddDays(-leading);
        for (int i = 0; i < 371; i++) // 53 weeks × 7
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date, inCurrentMonth: date.Year == year, isToday: date == _today, isFuture: date > _today));
        }
    }

    private string BuildHeaderLabel()
    {
        var culture = CultureInfo.CurrentCulture;
        return ViewMode switch
        {
            CalendarViewMode.Week => $"{Cells[0].Date.ToString("d MMM", culture)} – {Cells[^1].Date.ToString("d MMM yyyy", culture)}",
            CalendarViewMode.Year => DisplayYear.ToString(culture),
            _ => new DateOnly(DisplayYear, DisplayMonth, 1).ToString("MMMM yyyy", culture),
        };
    }

    private void BuildMonthGrid(int year, int month)
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

    /// <summary>Fetches markers for the visible cell span and assigns tint/number/notes by date.</summary>
    private async Task ApplyMarkersAsync(CancellationToken ct)
    {
        foreach (var cell in Cells)
        {
            cell.TintColorHex = CompletedColorHex;
            cell.TintStep = 0;
            cell.Number = null;
            cell.NoteCount = 0;
        }
        if (MarkerProvider is null || Cells.Count == 0) return;

        var markers = await MarkerProvider(Cells[0].Date, Cells[^1].Date, ct).ConfigureAwait(true);
        var byDate = markers.Days.ToDictionary(d => d.Date);
        foreach (var cell in Cells)
        {
            if (byDate.TryGetValue(cell.Date, out var m))
            {
                cell.TintStep = m.TintStep;
                cell.Number = m.Number;
                cell.NoteCount = m.NoteCount;
            }
        }
    }

    private void RefreshSelection()
    {
        foreach (var cell in Cells)
            cell.IsSelected = SelectedDate is { } d && cell.Date == d;
    }

    [RelayCommand]
    private Task PrevAsync(CancellationToken ct) => StepAsync(-1, ct);

    [RelayCommand]
    private Task NextAsync(CancellationToken ct) => StepAsync(+1, ct);

    private Task StepAsync(int direction, CancellationToken ct)
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Week:
                _weekAnchor = _weekAnchor.AddDays(7 * direction);
                break;
            case CalendarViewMode.Year:
                DisplayYear += direction;
                break;
            default:
                var m = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(direction);
                DisplayYear = m.Year;
                DisplayMonth = m.Month;
                break;
        }
        return ReloadAsync(ct);
    }

    [RelayCommand]
    private Task SetViewModeAsync(CalendarViewMode mode, CancellationToken ct)
    {
        ViewMode = mode;
        if (mode == CalendarViewMode.Week) _weekAnchor = SelectedDate ?? _today;
        return ReloadAsync(ct);
    }

    /// <summary>Reloads the current month (bound to the jump dropdowns). No-op while a load is already in flight,
    /// which prevents a reentrant loop when LoadAsync mutates DisplayMonth/DisplayYear (TwoWay-bound to the ComboBoxes).</summary>
    public Task JumpAsync(CancellationToken ct = default)
        => _isLoading ? Task.CompletedTask : ReloadAsync(ct);

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
