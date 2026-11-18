using Recordum.Plugins.Contracts.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using DayWindow = Recordum.Presentation.Services.CompletionDay;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Multi-metric, single-gesture recorder used for Resistance and State habits.
/// Renders one kind-aware cell per metric (measure+unit, scale buttons/slider, text) in one row.</summary>
public sealed partial class MeasuredQuickAddViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IReferenceDataQueryService refData,
    IClock clock,
    IActiveTimerService timers,
    ITickSourceFactory? ticks = null,
    IEventBus? events = null) : SeriesQuickAddViewModelBase(completions, clock, timers, ticks, events)
{
    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Steps), nameof(HasSteps))]
    private SeriesRowViewModel _row = new(Array.Empty<MetricDefinition>());
    [ObservableProperty] private string? _notes;

    private const int MaxSteps = 10;

    /// <summary>I gradini del comando in testa (direzione visiva §6.2): solo con una cella scala sola e al più
    /// <see cref="MaxSteps"/> gradini; altrimenti vuoto e la testa mostra le celle con "Registra".</summary>
    public IReadOnlyList<ScalePoint> Steps =>
        Row.Cells is [{ Kind: MetricInputKind.Scale, StepCount: > 0 and <= MaxSteps } cell] ? cell.Points : [];

    public bool HasSteps => Steps.Count > 0;

    /// <summary>Un clic su un gradino registra quel valore per il giorno scelto.</summary>
    [RelayCommand]
    private Task RecordStep(ScalePoint? step, CancellationToken ct)
    {
        if (step is null || !Steps.Contains(step)) return Task.CompletedTask;
        Row.Cells[0].SliderValue = (double)step.Value;
        return SaveAsync(ct);
    }

    // _timers, ChronoRunning and ToggleChrono live in SeriesQuickAddViewModelBase (CR21).

    public async Task LoadMetricsAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        var ordered = (await _metrics.ListByHabitAsync(Node.Id, ct).ConfigureAwait(true))
            .OrderBy(m => m.SortOrder).ToList();
        var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
        var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
        var factory = new MetricCellFactory(units, scales);
        Row = new SeriesRowViewModel(ordered.Select(factory.Create));
    }

    /// <summary>Accende il gradino registrato nel giorno di <see cref="QuickAddViewModelBase.CompletionDate"/>: l'ultimo
    /// valore della scala in quel giorno (come la mappa: una registrazione più recente senza valore non lo spegne).</summary>
    public override async Task LoadDayAsync(CancellationToken ct = default)
    {
        if (Node is null || !HasSteps)
        {
            foreach (var step in Steps) step.IsCurrent = false;
            return;
        }
        var day = CompletionDay;
        var list = await DayWindow.ListAsync(Completions, Node.Id, DayBoundary, day, ct).ConfigureAwait(true);
        // Durante l'attesa il giorno o la scala possono essere cambiati: vale la lettura partita per ultima.
        if (day != CompletionDay || !HasSteps) return;
        var metricId = Row.Cells[0].Metric.Id;
        var value = list.OrderByDescending(c => c.CompletedAtMs).SelectMany(c => c.Values)
            .FirstOrDefault(v => v.MetricDefinitionId == metricId)?.ValueDecimal;
        foreach (var step in Steps) step.IsCurrent = step.Value == value;
    }

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        // AsyncRelayCommand.ExecuteAsync non consulta CanExecute: la guardia del doppio clic sta qui.
        if (Node is null || IsSaving) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var values = Row.Cells.Select(c => c.BuildValue(0)).Where(v => v is not null).Select(v => v!).ToList();
            // Only State must carry a value (it records a current state). Resistance records abstinence
            // lapses — a bare relapse (e.g. "Stop smoking", zero metrics) is valid (spec multivalue §4.1).
            if (values.Count == 0 && Node.HabitType == HabitType.State)
            {
                ErrorMessage = Strings.QuickAdd_Error_ValueRequired;
                return;
            }

            var (ts, ms, retro) = ResolveCompletionTimestamp();
            var draft = new Completion
            {
                HabitNodeId = Node.Id,
                CompletedAt = ts,
                CompletedAtMs = ms,
                IsRetroactive = retro,
                Notes = Notes,
            };
            await Completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(true);
            await LoadDayAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetUnexpectedError(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
