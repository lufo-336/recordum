using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;

namespace Recordum.Presentation.ViewModels.HabitScreen.History;

/// <summary>La mappa della Cronologia, che cambia forma col periodo (spec §6.1): settimana = 7 caselle, mese = calendario,
/// 3 mesi / anno / intervallo = heatmap, tutto = una heatmap per anno. I marker vengono dalla stessa sorgente del
/// calendario di oggi (<see cref="CalendarMarkerProvider"/>), così colori e note restano una cosa sola.</summary>
public sealed partial class HistoryMapViewModel : ObservableObject
{
    private readonly CalendarMarkerProvider _markers;
    private readonly List<HistoryDayCell> _all = [];
    private int _generation;

    [ObservableProperty] private MapShape _shape = MapShape.Month;

    /// <summary>Le celle di Settimana e Mese.</summary>
    public ObservableCollection<HistoryDayCell> Cells { get; } = [];

    /// <summary>Le colonne di settimana della heatmap.</summary>
    public ObservableCollection<IReadOnlyList<HistoryDayCell>> Weeks { get; } = [];

    /// <summary>Le heatmap d'anno di "Tutto", dalla più recente.</summary>
    public ObservableCollection<HistoryYear> Years { get; } = [];

    public event EventHandler<DateOnly>? DaySelected;

    public HistoryMapViewModel(CalendarMarkerProvider markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        _markers = markers;
    }

    public async Task LoadAsync(DateOnly from, DateOnly to, MapShape shape, DateOnly today, DateOnly? selected, CancellationToken ct)
    {
        var generation = ++_generation;
        var cells = new List<HistoryDayCell>();
        var weeks = new List<IReadOnlyList<HistoryDayCell>>();
        var years = new List<HistoryYear>();
        HistoryDayCell Cell(DateOnly d, bool inRange)
        {
            var c = new HistoryDayCell(d, inRange, d == today, d > today) { IsSelected = d == selected };
            cells.Add(c);
            return c;
        }

        var flat = new List<HistoryDayCell>();
        switch (shape)
        {
            case MapShape.Week:
                for (var d = from; d <= to; d = d.AddDays(1)) flat.Add(Cell(d, true));
                break;
            case MapShape.Month:
                var first = Monday(from);
                for (var i = 0; i < 42; i++) { var d = first.AddDays(i); flat.Add(Cell(d, d.Month == from.Month && d.Year == from.Year)); }
                break;
            case MapShape.Heatmap:
                weeks.AddRange(WeekColumns(from, to, d => d >= from && d <= to, Cell));
                break;
            default:
                for (var y = to.Year; y >= from.Year; y--)
                {
                    var year = y;
                    years.Add(new HistoryYear(y, WeekColumns(new DateOnly(y, 1, 1), new DateOnly(y, 12, 31), d => d.Year == year, Cell)));
                }
                break;
        }

        if (cells.Count > 0)
        {
            var markers = await _markers(cells.Min(c => c.Date), cells.Max(c => c.Date), ct).ConfigureAwait(true);
            if (generation != _generation) return; // un caricamento più nuovo ha vinto
            var byDate = cells.ToLookup(c => c.Date);
            foreach (var m in markers.Days)
                foreach (var c in byDate[m.Date]) Apply(c, m);
        }

        Shape = shape;
        _all.Clear();
        _all.AddRange(cells);
        Cells.Clear();
        foreach (var c in flat) Cells.Add(c);
        Weeks.Clear();
        foreach (var w in weeks) Weeks.Add(w);
        Years.Clear();
        foreach (var y in years) Years.Add(y);
    }

    /// <summary>Marker → cella, in un posto solo. L'astinenza dipinge la ricaduta col pericolo a gradino 4 e il buco a
    /// gradino 1: qui la ricaduta diventa una cella vuota segnata (contorno e ×, GIO-9) e il buco una cella vuota.</summary>
    private static void Apply(HistoryDayCell c, CalendarDayMarker m)
    {
        var lapse = m.Tint == MarkerTint.Danger && m.TintStep == 4;
        var gap = m.Tint == MarkerTint.Danger && m.TintStep == 1;
        c.IsLapse = lapse && c.InRange; // fuori mese resta il solo numero attenuato
        c.Step = gap || lapse ? 0 : m.TintStep;
        c.NoteCount = m.NoteCount;
        c.Value = m.Number;
    }

    public void SetSelected(DateOnly? day)
    {
        foreach (var c in _all) c.IsSelected = c.Date == day;
    }

    [RelayCommand]
    private void Select(HistoryDayCell? cell)
    {
        if (cell is null || cell.IsFuture) return;
        DaySelected?.Invoke(this, cell.Date);
    }

    private static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static List<IReadOnlyList<HistoryDayCell>> WeekColumns(
        DateOnly from, DateOnly to, Func<DateOnly, bool> inRange, Func<DateOnly, bool, HistoryDayCell> cell)
    {
        var columns = new List<IReadOnlyList<HistoryDayCell>>();
        for (var monday = Monday(from); monday <= to; monday = monday.AddDays(7))
        {
            var week = new HistoryDayCell[7];
            for (var i = 0; i < 7; i++) { var d = monday.AddDays(i); week[i] = cell(d, inRange(d)); }
            columns.Add(week);
        }
        return columns;
    }
}
