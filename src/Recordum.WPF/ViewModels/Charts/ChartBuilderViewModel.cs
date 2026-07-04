using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Charts;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.Charts;

public sealed partial class ChartBuilderViewModel(
    IChartService charts, IHabitTreeQueryService tree, IMetricDefinitionService metrics) : ObservableObject
{
    private Guid? _editingId;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ChartKind _selectedKind = ChartKind.Line;
    [ObservableProperty] private string _range = "90d";
    [ObservableProperty] private string _bucket = "daily";
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<ChartSeriesRowViewModel> Series { get; } = [];
    public IReadOnlyList<ChartKind> AvailableKinds { get; } = Enum.GetValues<ChartKind>();

    /// <summary>Habits the user can chart (groups excluded). Populated by <see cref="EnsureHabitsAsync"/>
    /// from <see cref="LoadNew"/>/<see cref="LoadAsync"/> and handed to every row so it can offer a picker.</summary>
    public IReadOnlyList<HomeTreeRow> AvailableHabits { get; private set; } = [];

    /// <summary>Raised after a successful save, carrying the persisted chart id.</summary>
    public event EventHandler<Guid>? Saved;

    /// <summary>Raised whenever the series list or a row's content changes. Internal signal for the live preview (Task 12).</summary>
    public event EventHandler? SeriesChanged;

    /// <summary>Gates <see cref="SaveCommand"/>: a non-blank name and at least one row with a habit selected.</summary>
    public bool CanSave => !string.IsNullOrWhiteSpace(Name) && Series.Any(r => r.SelectedHabit is not null);

    public void LoadNew()
    {
        _editingId = null;
        Name = "";
        SelectedKind = ChartKind.Line;
        Range = "90d";
        Bucket = "daily";
        ErrorMessage = null;
        Series.Clear();
        _ = EnsureHabitsAsync(default);
    }

    public async ValueTask LoadAsync(Guid chartId, CancellationToken ct = default)
    {
        var chart = await charts.GetAsync(chartId, ct).ConfigureAwait(true);
        if (chart is null) { LoadNew(); return; }
        await EnsureHabitsAsync(ct).ConfigureAwait(true);

        _editingId = chart.Id;
        Name = chart.Name;
        SelectedKind = ChartKindParser.Parse(chart.ChartType);
        var cfg = ChartConfig.Parse(chart.ConfigJson);
        Range = cfg.Range;
        Bucket = cfg.Bucket;
        ErrorMessage = null;
        Series.Clear();

        foreach (var s in chart.Series.OrderBy(s => s.SortOrder))
        {
            var row = new ChartSeriesRowViewModel(AvailableHabits, LoadMetricsAsync);
            AddRow(row);
            // Reconstruct SelectedHabit/SelectedMetric from the persisted ids against the loaded
            // reference data, then restore the persisted Label/Color verbatim (they may be
            // hand-edited) and lock them in so re-selecting habit/metric later won't clobber them.
            row.SelectedHabit = AvailableHabits.FirstOrDefault(h => h.HabitNodeId == s.HabitNodeId);
            await row.WhenMetricsLoaded.ConfigureAwait(true);
            row.SelectedMetric = row.Metrics.FirstOrDefault(m => m.Id == s.MetricDefinitionId) ?? row.Metrics.FirstOrDefault();
            row.Label = s.Label;
            row.Color = s.Color;
            row.MarkLabelTouched();
        }
    }

    // Task-returning (not ValueTask) so LoadNew() can fire-and-forget it with a plain discard —
    // CA2012 forbids discarding an un-awaited ValueTask.
    private async Task EnsureHabitsAsync(CancellationToken ct)
    {
        var all = await tree.GetHomeTreeAsync(ct).ConfigureAwait(true);
        AvailableHabits = all.Where(r => r.HabitType is not null).ToList();
    }

    private Task<IReadOnlyList<MetricDefinition>> LoadMetricsAsync(Guid habitNodeId, CancellationToken ct) =>
        metrics.ListByHabitAsync(habitNodeId, ct).AsTask();

    [RelayCommand]
    private void AddSeries()
    {
        var row = new ChartSeriesRowViewModel(AvailableHabits, LoadMetricsAsync);
        AddRow(row);
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveSeries(ChartSeriesRowViewModel row)
    {
        row.Changed -= OnRowChanged;
        Series.Remove(row);
        SaveCommand.NotifyCanExecuteChanged();
        RaiseSeriesChanged();
    }

    private void AddRow(ChartSeriesRowViewModel row)
    {
        row.Changed += OnRowChanged;
        Series.Add(row);
    }

    private void OnRowChanged(object? sender, EventArgs e)
    {
        SaveCommand.NotifyCanExecuteChanged();
        RaiseSeriesChanged();
    }

    private void RaiseSeriesChanged() => SeriesChanged?.Invoke(this, EventArgs.Empty);

    partial void OnNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        var configJson = new ChartConfig(Range: Range, Bucket: Bucket).ToJson();
        var rows = Series.Select((r, i) => r.ToChartSeries(i)).Where(s => s is not null).Cast<ChartSeries>().ToList();

        try
        {
            Guid id;
            if (_editingId is { } existing)
            {
                await charts.UpdateAsync(existing, c =>
                {
                    c.Name = Name;
                    c.ChartType = ChartKindParser.ToToken(SelectedKind);
                    c.ConfigJson = configJson;
                }, ct).ConfigureAwait(true);
                await charts.ReplaceSeriesAsync(existing, rows, ct).ConfigureAwait(true);
                id = existing;
            }
            else
            {
                id = await charts.CreateAsync(new Chart
                {
                    Name = Name,
                    ChartType = ChartKindParser.ToToken(SelectedKind),
                    ConfigJson = configJson,
                }, rows, ct).ConfigureAwait(true);
                _editingId = id;
            }
            Saved?.Invoke(this, id);
        }
        catch (Exception)
        {
            ErrorMessage = Strings.Charts_Builder_SaveError;
        }
    }
}
