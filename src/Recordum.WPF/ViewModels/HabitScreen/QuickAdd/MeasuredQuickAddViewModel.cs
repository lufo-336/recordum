using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Multi-metric, single-gesture recorder used for Resistance and State habits.
/// Renders one kind-aware cell per metric (measure+unit, scale buttons/slider, text) in one row.</summary>
public sealed partial class MeasuredQuickAddViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IReferenceDataQueryService refData,
    IClock clock,
    IActiveTimerService timers) : QuickAddViewModelBase(completions, clock)
{
    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;
    private readonly IActiveTimerService _timers = timers;

    [ObservableProperty] private SeriesRowViewModel _row = new(Array.Empty<MetricDefinition>());
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private bool _chronoRunning;
    private SeriesCellViewModel? _chronoCell;

    /// <summary>Per-cell stopwatch: start runs an active_timer (Stopwatch); stop pours the elapsed
    /// seconds into the duration cell WITHOUT recording a completion (spec multivalue §4.1, §8).</summary>
    [RelayCommand]
    private async Task ToggleChrono(SeriesCellViewModel? cell, CancellationToken ct)
    {
        if (Node is null || cell is null || !cell.IsDuration) return;
        if (!ChronoRunning)
        {
            if (await _timers.GetActiveAsync(Node.Id, ct).ConfigureAwait(true) is not null) return;
            await _timers.StartAsync(Node.Id, TimerMode.Stopwatch, ct).ConfigureAwait(true);
            _chronoCell = cell;
            ChronoRunning = true;
        }
        else
        {
            var secs = await _timers.StopWithoutRecordingAsync(Node.Id, ct).ConfigureAwait(true);
            (_chronoCell ?? cell).SetDurationSeconds(secs);
            _chronoCell = null;
            ChronoRunning = false;
        }
    }

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

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var values = Row.Cells.Select(c => c.BuildValue(0)).Where(v => v is not null).Select(v => v!).ToList();
            if (values.Count == 0)
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
            var id = await Completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(true);
            OnSaved(id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
