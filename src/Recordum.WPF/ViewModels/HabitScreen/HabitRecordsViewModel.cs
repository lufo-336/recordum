using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>Modalità della lista registrazioni: il giorno selezionato sul calendario, o un periodo.</summary>
public enum EntriesRecordsMode { Giorno, Periodo }

/// <summary>Incapsula il pannello "Registrazioni" della colonna Cronologia: modalità (Giorno/Periodo),
/// filtro periodo e la <see cref="CompletionListViewModel"/> interna. Tiene il concern fuori dalla
/// già-grande HabitScreenViewModel.</summary>
public sealed partial class HabitRecordsViewModel : ObservableObject, IDisposable
{
    private readonly IClock _clock;
    private Guid? _nodeId;
    private DateOnly? _calendarDay;
    private CancellationTokenSource? _reloadCts;

    public CompletionListViewModel Records { get; }

    [ObservableProperty] private EntriesRecordsMode _mode = EntriesRecordsMode.Giorno;
    [ObservableProperty] private EntriesPeriod _selectedPeriod = EntriesPeriod.Mese;
    [ObservableProperty] private DateOnly? _customFrom;
    [ObservableProperty] private DateOnly? _customTo;

    public HabitRecordsViewModel(CompletionListViewModel records, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(clock);
        Records = records;
        _clock = clock;
    }

    public void SetContext(Guid nodeId, bool isJournal)
    {
        _nodeId = nodeId;
        Records.IsJournal = isJournal;
    }

    /// <summary>Registra il giorno scelto sul calendario; ricarica la lista solo se si è in modalità Giorno.</summary>
    public async Task ShowDayAsync(DateOnly date, CancellationToken ct = default)
    {
        _calendarDay = date;
        if (Mode == EntriesRecordsMode.Giorno && _nodeId is { } id)
            await Records.LoadDayAsync(id, date, ct).ConfigureAwait(true);
    }

    /// <summary>Carica i completamenti del periodo selezionato (today da IClock, fuso locale).</summary>
    public async Task ApplyPeriodAsync(CancellationToken ct = default)
    {
        if (_nodeId is not { } id) return;
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone));
        var (from, to) = EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo);
        await Records.LoadRangeAsync(id, from, to, ct).ConfigureAwait(true);
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

    public void Dispose() => _reloadCts?.Dispose();
}
