using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>Presentation-ready marker per un giorno (il host calcola tint/numero/note).
/// <para><see cref="TintColorHex"/> overrides the navigator's <see cref="CalendarNavigatorViewModel.CompletedColorHex"/>
/// for this specific day (e.g. danger red on a lapse day). Null = fall back to the habit color.</para></summary>
public sealed record CalendarDayMarker(DateOnly Date, int TintStep, string? Number, int NoteCount, string? TintColorHex = null);

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
    /// <summary>Il giorno corrente secondo il confine, impostato da <see cref="LoadAsync"/> e tenuto
    /// per la durata dello screen: un calendario lasciato aperto oltre la mezzanotte conserva quel
    /// giorno finché non si ricarica. Intenzionale, la granularità è il giorno.
    /// <para>Prima del primo caricamento vale <c>default</c>: non si deriva un "oggi" provvisorio
    /// dall'ora locale nuda, che è esattamente il secondo modo di calcolarlo che R9 elimina — e le
    /// celle nascono solo dentro LoadAsync, quindi nessuno lo legge prima.</para></summary>
    private DateOnly _today;
    private readonly IClock _clock;
    private bool _isLoading;

    public CalendarNavigatorViewModel(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        // SelectableYears resta costruito QUI dall'anno locale del clock, non dentro LoadAsync: il
        // confine puo' cambiare l'anno solo a cavallo del 31 dicembre, la lista copre 15 anni indietro
        // e uno avanti, e ricostruire una ObservableCollection bindata a ogni caricamento e' come in
        // questo repo si perdono i dati (4d01f9c, 1d96967). "_today" invece lo rifa' LoadAsync col
        // confine ricevuto.
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone);
        WeekdayHeaders = BuildWeekdayHeaders();
        SelectableYears = Enumerable.Range(localNow.Year - 15, 17).ToList(); // 15 back, current, 1 forward
    }

    public CalendarMarkerProvider? MarkerProvider { get; set; }

    [ObservableProperty] private string _completedColorHex = "#808080";
    [ObservableProperty] private string _noteColorHex = "#e0b341";
    [ObservableProperty] private int _displayYear;
    [ObservableProperty] private int _displayMonth;
    [ObservableProperty] private string _headerLabel = string.Empty;
    [ObservableProperty] private DateOnly? _selectedDate;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellSize))]
    private CalendarViewMode _viewMode = CalendarViewMode.Month;

    /// <summary>Fixed day-cell edge in px (excludes the 2px margin) for the Week + Month grids; Week is
    /// roomier. Single source of truth so the weekday header and the day grid derive the same column
    /// width (7 × (CellSize+4)) and stay aligned, without view-mode-dependent width triggers.</summary>
    public double CellSize => ViewMode == CalendarViewMode.Week ? 56 : 40;

    /// <summary>Year view only: false = twelve mini-months (default), true = GitHub-style heatmap.</summary>
    [ObservableProperty] private bool _yearAsHeatmap;
    private DateOnly _weekAnchor;

    /// <summary>Flat day cells: the Week + Month grids, and (when <see cref="YearAsHeatmap"/>) the Year
    /// heatmap's 378 cells. Empty in the Year mini-months view.</summary>
    public ObservableCollection<CalendarDayCellViewModel> Cells { get; } = [];

    /// <summary>The twelve month blocks of the Year mini-months view. Empty otherwise.</summary>
    public ObservableCollection<CalendarMonthBlockViewModel> MonthBlocks { get; } = [];

    /// <summary>The Year heatmap grouped into 54 week-columns (same cell instances as <see cref="Cells"/>),
    /// so a horizontal stack of vertical columns renders a GitHub-style grid. Empty unless Year + heatmap.</summary>
    public ObservableCollection<IReadOnlyList<CalendarDayCellViewModel>> YearWeeks { get; } = [];

    /// <summary>Every currently-rendered day cell, regardless of view mode — the surface that markers
    /// and selection are applied to.</summary>
    private IEnumerable<CalendarDayCellViewModel> VisibleCells =>
        ViewMode == CalendarViewMode.Year && !YearAsHeatmap ? MonthBlocks.SelectMany(b => b.Cells) : Cells;

    public IReadOnlyList<string> WeekdayHeaders { get; }
    public IReadOnlyList<int> SelectableYears { get; }
    public IReadOnlyList<int> SelectableMonths { get; } = Enumerable.Range(1, 12).ToList();

    public event EventHandler<DateOnly>? SelectedDateChanged;

    public async Task LoadAsync(int year, int month, DateOnly? selected, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(day);
        _today = day.Today(_clock);
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
                case CalendarViewMode.Year when YearAsHeatmap: BuildYearHeatmap(DisplayYear); break;
                case CalendarViewMode.Year: BuildYearBlocks(DisplayYear); break;
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
        MonthBlocks.Clear();
        YearWeeks.Clear();
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

    /// <summary>Year view: twelve mini-month blocks, each a 6×7 grid with leading/trailing days of
    /// adjacent months kept (blank in the template) so weekday columns line up.</summary>
    private void BuildYearBlocks(int year)
    {
        Cells.Clear();
        YearWeeks.Clear();
        MonthBlocks.Clear();
        var dtf = CultureInfo.CurrentCulture.DateTimeFormat;
        int firstDow = (int)dtf.FirstDayOfWeek;
        for (int month = 1; month <= 12; month++)
        {
            var block = new CalendarMonthBlockViewModel(year, month, dtf.GetMonthName(month));
            var first = new DateOnly(year, month, 1);
            int leading = ((int)first.DayOfWeek - firstDow + 7) % 7;
            var start = first.AddDays(-leading);
            for (int i = 0; i < 42; i++)
            {
                var date = start.AddDays(i);
                block.Cells.Add(new CalendarDayCellViewModel(
                    date,
                    inCurrentMonth: date.Month == month && date.Year == year,
                    isToday: date == _today,
                    isFuture: date > _today));
            }
            MonthBlocks.Add(block);
        }
    }

    /// <summary>Year heatmap: 378 cells (54 weeks × 7) from the week boundary on/before Jan 1, grouped into
    /// week-columns for a GitHub-style render. 54 (not 53) weeks so Dec 31 is always covered: when Jan 1
    /// falls on the last weekday of the column (leading == 6) in a leap year, 53 weeks stop at Dec 30 and
    /// drop the 31st (CR22). <see cref="Cells"/> and <see cref="YearWeeks"/> share instances.</summary>
    private void BuildYearHeatmap(int year)
    {
        MonthBlocks.Clear();
        YearWeeks.Clear();
        Cells.Clear();
        var jan1 = new DateOnly(year, 1, 1);
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)jan1.DayOfWeek - firstDow + 7) % 7;
        var start = jan1.AddDays(-leading);
        for (int w = 0; w < 54; w++)
        {
            var week = new List<CalendarDayCellViewModel>(7);
            for (int d = 0; d < 7; d++)
            {
                var date = start.AddDays(w * 7 + d);
                var cell = new CalendarDayCellViewModel(
                    date,
                    inCurrentMonth: date.Year == year,
                    isToday: date == _today,
                    isFuture: date > _today);
                Cells.Add(cell);
                week.Add(cell);
            }
            YearWeeks.Add(week);
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
        MonthBlocks.Clear();
        YearWeeks.Clear();
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
        var cells = VisibleCells.ToList();
        foreach (var cell in cells)
        {
            cell.TintColorHex = CompletedColorHex;
            cell.TintStep = 0;
            cell.Number = null;
            cell.NoteCount = 0;
        }
        if (MarkerProvider is null || cells.Count == 0) return;

        var markers = await MarkerProvider(cells.Min(c => c.Date), cells.Max(c => c.Date), ct).ConfigureAwait(true);
        var byDate = markers.Days.ToDictionary(d => d.Date);
        foreach (var cell in cells)
        {
            if (byDate.TryGetValue(cell.Date, out var m))
            {
                cell.TintColorHex = m.TintColorHex ?? CompletedColorHex;
                cell.TintStep = m.TintStep;
                cell.Number = m.Number;
                cell.NoteCount = m.NoteCount;
            }
        }
    }

    private void RefreshSelection()
    {
        foreach (var cell in VisibleCells)
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

    [RelayCommand]
    private Task SetYearHeatmapAsync(bool heatmap, CancellationToken ct)
    {
        if (YearAsHeatmap == heatmap) return Task.CompletedTask;
        YearAsHeatmap = heatmap;
        return ViewMode == CalendarViewMode.Year ? ReloadAsync(ct) : Task.CompletedTask;
    }

    /// <summary>Reloads the current month (bound to the jump dropdowns). No-op while a load is already in flight,
    /// which prevents a reentrant loop when LoadAsync mutates DisplayMonth/DisplayYear (TwoWay-bound to the ComboBoxes).
    /// Also a no-op before the first <see cref="LoadAsync"/> (DisplayMonth still 0): completion/note events can fire
    /// this on a tab whose calendar hasn't loaded yet, and BuildMonthGrid(0,0) would throw.</summary>
    public Task JumpAsync(CancellationToken ct = default)
        => _isLoading || DisplayMonth == 0 ? Task.CompletedTask : ReloadAsync(ct);

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
