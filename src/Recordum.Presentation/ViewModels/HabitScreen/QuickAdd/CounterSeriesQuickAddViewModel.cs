using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

public sealed partial class CounterSeriesQuickAddViewModel : SeriesQuickAddViewModelBase
{
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IHabitConfigService _configs;
    private List<MetricDefinition> _orderedMetrics = [];
    private MetricCellFactory _factory = new(Array.Empty<UnitOfMeasure>(), Array.Empty<ScaleTemplate>());
    private string[] _seriesLabels = [];

    [ObservableProperty] private bool _isRepeatable;
    [ObservableProperty] private int _selectedSeriesIndex;
    public ObservableCollection<SeriesChipViewModel> Chips { get; } = [];

    public CounterSeriesQuickAddViewModel(
        ICompletionService completions,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
        IClock clock,
        IActiveTimerService timers,
        IHabitConfigService configs,
        ITickSourceFactory? ticks = null) : base(completions, clock, timers, ticks)
    {
        _metrics = metrics;
        _refData = refData;
        _configs = configs;
        // Keep the per-row delete button's enabled state in sync with the "at least one series" rule.
        // Also notify ValidSeriesCount whenever the collection changes (add/remove rows).
        Series.CollectionChanged += (_, _) =>
        {
            RemoveSeriesCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ValidSeriesCount));
        };
    }

    /// <summary>Call BEFORE LoadMetricsAsync: fixes the registration mode + the persisted series names
    /// (U8 repeatable counter).</summary>
    public void SetRegistrationConfig(bool repeatable, string? seriesLabelsJson)
    {
        IsRepeatable = repeatable;
        _seriesLabels = ParseSeriesLabels(seriesLabelsJson);
    }

    private static string[] ParseSeriesLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (System.Text.Json.JsonException) { return []; }
    }

    private string LabelFor(int i)
    {
        if (i < _seriesLabels.Length && !string.IsNullOrWhiteSpace(_seriesLabels[i])) return _seriesLabels[i];
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
        return string.Format(CultureInfo.CurrentCulture, Strings.HabitScreen_QuickAdd_SeriesFallback, i + 1);
#pragma warning restore CA1863
    }

    /// <summary>Rebuilds the chip list from the current rows, clamps the selection, and re-applies
    /// row visibility. Chips carry no persist handlers, so clearing here is safe (4d01f9c rule).</summary>
    private void RebuildChips()
    {
        Chips.Clear();
        if (IsRepeatable)
            for (var i = 0; i < Series.Count; i++)
                Chips.Add(new SeriesChipViewModel(i, LabelFor(i)));
        if (SelectedSeriesIndex >= Series.Count) SelectedSeriesIndex = Math.Max(0, Series.Count - 1);
        ApplySelection();
    }

    private void ApplySelection()
    {
        foreach (var chip in Chips) chip.IsSelected = chip.Index == SelectedSeriesIndex;
        for (var i = 0; i < Series.Count; i++)
            Series[i].IsVisibleRow = !IsRepeatable || i == SelectedSeriesIndex;
    }

    partial void OnSelectedSeriesIndexChanged(int value) => ApplySelection();

    [RelayCommand]
    private void SelectSeries(SeriesChipViewModel chip) => SelectedSeriesIndex = chip.Index;

    [RelayCommand]
    private static void BeginRenameChip(SeriesChipViewModel chip)
    {
        chip.PendingName = chip.Name;
        chip.IsEditing = true;
    }

    [RelayCommand]
    private static void CancelRenameChip(SeriesChipViewModel chip) => chip.IsEditing = false;

    [RelayCommand]
    private async Task CommitRenameChipAsync(SeriesChipViewModel chip)
    {
        var name = chip.PendingName?.Trim();
        chip.IsEditing = false;
        if (string.IsNullOrEmpty(name) || name == chip.Name || Node is null) return;
        chip.Name = name;
        var labels = new string[Math.Max(_seriesLabels.Length, chip.Index + 1)];
        for (var i = 0; i < labels.Length; i++) labels[i] = i < _seriesLabels.Length ? _seriesLabels[i] : string.Empty;
        labels[chip.Index] = name;
        _seriesLabels = labels;
        var json = System.Text.Json.JsonSerializer.Serialize(_seriesLabels);
        await _configs.UpsertAsync(Node.Id, c => c.SeriesLabelsJson = json).ConfigureAwait(true);
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
        SelectedSeriesIndex = 0;
        RebuildChips();
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
        RebuildChips();
        SelectedSeriesIndex = Series.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSeries))]
    private void RemoveSeries(SeriesRowViewModel row)
    {
        if (Series.Count <= 1) return;
        var idx = Series.IndexOf(row);
        if (idx < 0) return; // guard: row already gone
        // Unhook the PropertyChanged handler before removing to avoid stale notifications.
        row.PropertyChanged -= OnRowPropertyChanged;
        Series.RemoveAt(idx);
        // Splice the removed row's label out too, so _seriesLabels (positional) stays aligned
        // with Series after removing a non-last row (review finding: a shifted-but-unspliced
        // array mislabels the surviving rows and CommitRenameChipAsync then persists the wrong
        // mapping to series_labels_json).
        if (idx < _seriesLabels.Length)
            _seriesLabels = [.. _seriesLabels[..idx], .. _seriesLabels[(idx + 1)..]];
        RebuildChips();
    }

    private bool CanRemoveSeries(SeriesRowViewModel row) => Series.Count > 1;

    // ChronoRunning and ToggleChrono live in SeriesQuickAddViewModelBase (CR21).

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        // AsyncRelayCommand.ExecuteAsync non consulta CanExecute: la guardia del doppio clic sta qui.
        if (Node is null || _orderedMetrics.Count == 0 || Series.Count == 0 || IsSaving) return;
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
            await Completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(true);
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
