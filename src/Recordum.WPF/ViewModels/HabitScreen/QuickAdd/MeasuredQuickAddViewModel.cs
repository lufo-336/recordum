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
    IActiveTimerService timers) : SeriesQuickAddViewModelBase(completions, clock, timers)
{
    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;

    [ObservableProperty] private SeriesRowViewModel _row = new(Array.Empty<MetricDefinition>());
    [ObservableProperty] private string? _notes;

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
            var id = await Completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(true);
            OnSaved(id);
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
