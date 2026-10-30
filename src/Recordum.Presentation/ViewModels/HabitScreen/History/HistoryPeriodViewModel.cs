using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Time;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.History;

/// <summary>Un selettore di periodo che comanda mappa e Cronologia insieme (spec §6.1): Settimana (da lunedì) · Mese ·
/// 3 mesi · Anno · Tutto, ‹ › per spostarsi, "Dal… al…", "Oggi". I periodi sono di calendario; › si ferma su quello che
/// contiene oggi.</summary>
public sealed partial class HistoryPeriodViewModel : ObservableObject
{
    private readonly IClock _clock;
    private DayBoundary _day = DayBoundary.Utc;
    private DateOnly _created;
    private DateOnly _anchor;

    [ObservableProperty] private HistoryPeriod _period = HistoryPeriod.Month;
    [ObservableProperty] private DateOnly _from;
    [ObservableProperty] private DateOnly _to;
    [ObservableProperty] private string _label = string.Empty;

    /// <summary>Dopo ogni spostamento (periodo, ‹ ›, Oggi, intervallo libero).</summary>
    public event EventHandler? Changed;

    public HistoryPeriodViewModel(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    private DateOnly CurrentDay => _day.Today(_clock);

    /// <summary>Un soggetto nuovo: riparte dal periodo che contiene oggi. Non alza <see cref="Changed"/>: chi carica il
    /// soggetto ricarica da sé.</summary>
    public void SetContext(DayBoundary day, DateOnly habitCreated, HistoryPeriod? reset = null)
    {
        _day = day;
        _created = habitCreated;
        _anchor = CurrentDay;
        if (reset is { } forced) SetPeriodSilently(forced);
        else if (Period == HistoryPeriod.Custom) SetPeriodSilently(HistoryPeriod.Month);
        Recompute();
    }

    private bool _silent;

    private void SetPeriodSilently(HistoryPeriod value)
    {
        _silent = true;
        try { Period = value; }
        finally { _silent = false; }
    }

    public void SetCustom(DateOnly a, DateOnly b)
    {
        (var from, var to) = a <= b ? (a, b) : (b, a);
        SetPeriodSilently(HistoryPeriod.Custom);
        From = from;
        To = to;
        Label = RangeLabel(from, to);
        NotifyCommands();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPeriodChanged(HistoryPeriod value)
    {
        if (_silent || value == HistoryPeriod.Custom) return; // Custom si entra solo da SetCustom
        Recompute();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool CanStep => Period is not (HistoryPeriod.All or HistoryPeriod.Custom);
    private bool CanPrevious() => CanStep;
    private bool CanNext() => CanStep && To < CurrentDay;

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private void Previous() => Step(-1);

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next() => Step(1);

    [RelayCommand]
    private void Today()
    {
        _anchor = CurrentDay;
        if (Period == HistoryPeriod.Custom) SetPeriodSilently(HistoryPeriod.Month);
        Recompute();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Step(int direction)
    {
        if (!CanStep) return;
        _anchor = Period switch
        {
            HistoryPeriod.Week => _anchor.AddDays(7 * direction),
            HistoryPeriod.Month => _anchor.AddMonths(direction),
            HistoryPeriod.ThreeMonths => _anchor.AddMonths(3 * direction),
            _ => _anchor.AddYears(direction),
        };
        Recompute();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Recompute()
    {
        var a = _anchor;
        (From, To) = Period switch
        {
            HistoryPeriod.Week => (Monday(a), Monday(a).AddDays(6)),
            HistoryPeriod.Month => (new DateOnly(a.Year, a.Month, 1), new DateOnly(a.Year, a.Month, 1).AddMonths(1).AddDays(-1)),
            HistoryPeriod.ThreeMonths => (new DateOnly(a.Year, a.Month, 1).AddMonths(-2), new DateOnly(a.Year, a.Month, 1).AddMonths(1).AddDays(-1)),
            HistoryPeriod.Year => (new DateOnly(a.Year, 1, 1), new DateOnly(a.Year, 12, 31)),
            HistoryPeriod.All => (new DateOnly(Math.Min(_created.Year, CurrentDay.Year), 1, 1), CurrentDay),
            _ => (From, To),
        };
        Label = Period switch
        {
            HistoryPeriod.Week => RangeLabel(From, To),
            HistoryPeriod.Month => To.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            HistoryPeriod.ThreeMonths => From.Year == To.Year
                ? $"{From.ToString("MMM", CultureInfo.CurrentCulture)} – {To.ToString("MMM yyyy", CultureInfo.CurrentCulture)}"
                : $"{From.ToString("MMM yyyy", CultureInfo.CurrentCulture)} – {To.ToString("MMM yyyy", CultureInfo.CurrentCulture)}",
            HistoryPeriod.Year => From.Year.ToString(CultureInfo.CurrentCulture),
            HistoryPeriod.All => Strings.History_Period_All,
            _ => RangeLabel(From, To),
        };
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    private static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>"19–25 ott 2026", "26 ott – 1 nov 2026", "28 dic 2025 – 3 gen 2026".</summary>
    private static string RangeLabel(DateOnly from, DateOnly to)
    {
        var c = CultureInfo.CurrentCulture;
        if (from.Year == to.Year && from.Month == to.Month)
            return $"{from.Day.ToString(c)}–{to.ToString("d MMM yyyy", c)}";
        if (from.Year == to.Year)
            return $"{from.ToString("d MMM", c)} – {to.ToString("d MMM yyyy", c)}";
        return $"{from.ToString("d MMM yyyy", c)} – {to.ToString("d MMM yyyy", c)}";
    }
}
