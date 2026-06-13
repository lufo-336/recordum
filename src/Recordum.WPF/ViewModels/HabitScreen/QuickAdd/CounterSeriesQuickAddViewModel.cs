using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class CounterSeriesQuickAddViewModel : QuickAddViewModelBase
{
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IActiveTimerService _timers;
    private List<MetricDefinition> _orderedMetrics = [];
    private MetricCellFactory _factory = new(Array.Empty<UnitOfMeasure>(), Array.Empty<ScaleTemplate>());

    public CounterSeriesQuickAddViewModel(
        ICompletionService completions,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
        IClock clock,
        IActiveTimerService timers) : base(completions, clock)
    {
        _metrics = metrics;
        _refData = refData;
        _timers = timers;
        // Keep the per-row delete button's enabled state in sync with the "at least one series" rule.
        // Also notify ValidSeriesCount whenever the collection changes (add/remove rows).
        Series.CollectionChanged += (_, _) =>
        {
            RemoveSeriesCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ValidSeriesCount));
        };
    }

    public ObservableCollection<SeriesRowViewModel> Series { get; } = [];

    [ObservableProperty] private string? _notes;

    /// <summary>Number of rows with at least one value (spec D6 implicit-check rule).
    /// Drives "Registra N serie" button label.</summary>
    public int ValidSeriesCount => Series.Count(r => r.IsValid);

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

    private SeriesRowViewModel NewRow()
    {
        var row = new SeriesRowViewModel(_orderedMetrics.Select(_factory.Create));
        // Re-notify ValidSeriesCount whenever any cell changes in this row.
        row.PropertyChanged += OnRowPropertyChanged;
        return row;
    }

    private void OnRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(ValidSeriesCount));

    [RelayCommand]
    private void AddSeries()
    {
        if (_orderedMetrics.Count == 0) return;
        Series.Add(NewRow());
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSeries))]
    private void RemoveSeries(SeriesRowViewModel row)
    {
        if (Series.Count <= 1) return;
        // Unhook the PropertyChanged handler before removing to avoid stale notifications.
        row.PropertyChanged -= OnRowPropertyChanged;
        Series.Remove(row);
    }

    private bool CanRemoveSeries(SeriesRowViewModel row) => Series.Count > 1;

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
            // One active timer per habit (UNIQUE on active_timer): if the habit's own timer is already
            // running, the per-cell stopwatch does not start.
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

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null || _orderedMetrics.Count == 0 || Series.Count == 0) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var validRows = Series.Where(r => r.IsValid).ToList();
            var values = new List<CompletionValue>();
            for (int i = 0; i < validRows.Count; i++)
                foreach (var cell in validRows[i].Cells)
                    if (cell.BuildValue(i) is { } v) values.Add(v);

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
