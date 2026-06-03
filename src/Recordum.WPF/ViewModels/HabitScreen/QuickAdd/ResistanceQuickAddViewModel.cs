using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class ResistanceQuickAddViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IReferenceDataQueryService refData,
    IClock clock) : QuickAddViewModelBase(completions, clock)
{
    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;
    private List<MetricDefinition> _orderedMetrics = [];
    private MetricCellFactory _factory = new(Array.Empty<UnitOfMeasure>(), Array.Empty<ScaleTemplate>());

    public ObservableCollection<SeriesRowViewModel> Series { get; } = [];

    [ObservableProperty] private string? _notes;

    public async Task LoadMetricsAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        var list = await _metrics.ListByHabitAsync(Node.Id, ct).ConfigureAwait(true);
        _orderedMetrics = list.OrderBy(m => m.SortOrder).ToList();
        var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
        var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
        _factory = new MetricCellFactory(units, scales);
        Series.Clear();
        if (_orderedMetrics.Count > 0)
            Series.Add(NewRow());
    }

    private SeriesRowViewModel NewRow() => new(_orderedMetrics.Select(_factory.Create));

    [RelayCommand]
    private void AddSeries()
    {
        if (_orderedMetrics.Count == 0) return;
        Series.Add(NewRow());
    }

    [RelayCommand]
    private void RemoveSeries(SeriesRowViewModel row)
    {
        if (Series.Count > 1) Series.Remove(row);
    }

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null || _orderedMetrics.Count == 0 || Series.Count == 0) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var values = new List<CompletionValue>();
            for (int i = 0; i < Series.Count; i++)
            {
                foreach (var cell in Series[i].Cells)
                {
                    var v = cell.BuildValue(i);
                    if (v is not null) values.Add(v);
                }
            }
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
