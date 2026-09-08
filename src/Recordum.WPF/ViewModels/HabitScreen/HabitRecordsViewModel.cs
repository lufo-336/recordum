using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Services;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>Modalità della lista registrazioni: il giorno selezionato sul calendario, o un periodo.</summary>
public enum EntriesRecordsMode { Giorno, Periodo }

/// <summary>Incapsula il pannello "Registrazioni" della colonna Cronologia: modalità (Giorno/Periodo),
/// filtro periodo e la <see cref="CompletionListViewModel"/> interna per habit normali, oppure la
/// <see cref="AbstinencePeriodListViewModel"/> per habit di tipo Resistance. Tiene il concern fuori dalla
/// già-grande HabitScreenViewModel.</summary>
public sealed partial class HabitRecordsViewModel : ObservableObject, IDisposable
{
    private readonly IClock _clock;
    private Guid? _nodeId;
    private DateOnly? _calendarDay;
    private CancellationTokenSource? _reloadCts;
    private bool _isResistance;

    public CompletionListViewModel Records { get; }
    public AbstinencePeriodListViewModel Periods { get; }

    /// <summary>The list currently active: <see cref="AbstinencePeriodListViewModel"/> for Resistance
    /// habits, <see cref="CompletionListViewModel"/> for all others. Bound to the ContentControl in XAML.</summary>
    public object CurrentList => _isResistance ? Periods : Records;

    /// <summary>True when the active list has no rows (drives the empty-state TextBlock visibility).</summary>
    public bool IsEmpty => _isResistance ? Periods.IsEmpty : Records.Rows.Count == 0;

    [ObservableProperty] private EntriesRecordsMode _mode = EntriesRecordsMode.Giorno;
    [ObservableProperty] private EntriesPeriod _selectedPeriod = EntriesPeriod.Mese;
    [ObservableProperty] private DateOnly? _customFrom;
    [ObservableProperty] private DateOnly? _customTo;

    /// <summary>Il confine ricevuto dallo screen. Le registrazioni si ritagliano sul giorno
    /// dell'abitudine, non sulla data UTC.</summary>
    internal DayBoundary DayBoundary { get; set; } = DayBoundary.Utc;

    public HabitRecordsViewModel(CompletionListViewModel records, AbstinencePeriodListViewModel periods, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(periods);
        ArgumentNullException.ThrowIfNull(clock);
        Records = records;
        Periods = periods;
        _clock = clock;
        // Keep IsEmpty in sync when the period list changes (rows added/removed via delete).
        periods.Changed += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public void SetContext(Guid nodeId, bool isJournal, bool isResistance = false, string? seriesLabelsJson = null)
    {
        _nodeId = nodeId;
        Records.IsJournal = isJournal;
        Records.SeriesLabelsJson = seriesLabelsJson;
        _isResistance = isResistance;
        OnPropertyChanged(nameof(CurrentList));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Registra il giorno scelto sul calendario; ricarica la lista solo se si è in modalità Giorno.</summary>
    public async Task ShowDayAsync(DateOnly date, CancellationToken ct = default)
    {
        _calendarDay = date;
        if (Mode == EntriesRecordsMode.Giorno && _nodeId is { } id)
        {
            if (_isResistance) await Periods.LoadDayAsync(id, date, ct).ConfigureAwait(true);
            else await Records.LoadDayAsync(id, date, ct).ConfigureAwait(true);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Carica i completamenti del periodo selezionato (today da IClock, fuso locale).</summary>
    public async Task ApplyPeriodAsync(CancellationToken ct = default)
    {
        if (_nodeId is not { } id) return;
        var today = DayBoundary.Today(_clock);
        var (from, to) = EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo);
        if (_isResistance) await Periods.LoadRangeAsync(id, from, to, ct).ConfigureAwait(true);
        else await Records.LoadRangeAsync(id, from, to, ct).ConfigureAwait(true);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Rilancia la modalità corrente (usato dopo un completamento).</summary>
    public Task ReloadAsync(CancellationToken ct = default) =>
        Mode == EntriesRecordsMode.Giorno
            ? (_calendarDay is { } d ? ShowDayAsync(d, ct) : Task.CompletedTask)
            : ApplyPeriodAsync(ct);

    partial void OnModeChanged(EntriesRecordsMode value) => StartReload(ReloadAsync);
    partial void OnSelectedPeriodChanged(EntriesPeriod value) { if (Mode == EntriesRecordsMode.Periodo) StartReload(ApplyPeriodAsync); }
    partial void OnCustomFromChanged(DateOnly? value) => ReloadIfCustom();
    partial void OnCustomToChanged(DateOnly? value) => ReloadIfCustom();

    private void ReloadIfCustom()
    {
        if (Mode == EntriesRecordsMode.Periodo && SelectedPeriod == EntriesPeriod.Personalizzato)
            StartReload(ApplyPeriodAsync);
    }

    // A rapid Giorno↔Periodo / period toggle fires overlapping fire-and-forget reloads; cancel the
    // previous one so a slower stale query can't overwrite the list with old results (CR19).
    private void StartReload(Func<CancellationToken, Task> work)
    {
        _reloadCts?.Cancel();
        var cts = _reloadCts = new CancellationTokenSource();
        _ = RunGuardedAsync(work, cts.Token);
    }

    private static async Task RunGuardedAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        try { await work(ct).ConfigureAwait(true); }
        catch (OperationCanceledException) { /* superseded by a newer mode/period change */ }
    }

    public void Dispose()
    {
        _reloadCts?.Dispose();
        Periods.Dispose(); // stops any live period-row clock (its DispatcherTimer)
    }
}
