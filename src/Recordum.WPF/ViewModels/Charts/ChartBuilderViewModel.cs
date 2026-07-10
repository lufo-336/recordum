using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.Charts;

public sealed partial class ChartBuilderViewModel : ObservableObject
{
    private readonly IChartService _charts;
    private readonly IHabitTreeQueryService _tree;
    private readonly IMetricDefinitionService _metrics;
    private readonly IChartQueryService _query;
    private readonly IExtensionPointRegistry _registry;

    // Debounces the live preview: coalesces a burst of edits into a single re-render ~300ms after
    // the last one. Created only under a running WPF app — null in headless tests, which call
    // RefreshPreviewAsync directly. A stopped DispatcherTimer is not rooted by the dispatcher.
    private readonly DispatcherTimer? _previewTimer;

    private Guid? _editingId;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ChartKind _selectedKind = ChartKind.Line;
    [ObservableProperty] private string _range = "90d";
    [ObservableProperty] private string _bucket = "daily";
    [ObservableProperty] private string? _errorMessage;

    /// <summary>The rendered live-preview view (a WPF FrameworkElement in Fase 3), or null when there
    /// is nothing to preview / the render failed. Consumed by the builder panel's preview pane.</summary>
    [ObservableProperty] private object? _previewView;

    /// <summary>True when there are no valid series to preview (drives the preview pane's empty state).</summary>
    [ObservableProperty] private bool _previewEmpty = true;

    public ChartBuilderViewModel(
        IChartService charts, IHabitTreeQueryService tree, IMetricDefinitionService metrics,
        IChartQueryService query, IExtensionPointRegistry registry)
    {
        _charts = charts;
        _tree = tree;
        _metrics = metrics;
        _query = query;
        _registry = registry;

        // Dispatcher unavailable in headless unit-test environments (Application.Current is null).
        if (System.Windows.Application.Current is not null)
        {
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _previewTimer.Tick += OnPreviewTick;
        }
    }

    /// <summary>Theme flag pushed in by the workspace when it opens the builder, so the live preview themes correctly.</summary>
    public bool IsDarkTheme { get; set; }

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
        PreviewView = null;
        PreviewEmpty = true;
        Series.Clear();
        // Start with one empty series row: without it the form shows no habit picker at all and
        // the user has no clue where to choose the habit (wave-1a visual finding W1a-VF2).
        AddSeries();
        _ = LoadHabitsFireAndForgetAsync();
    }

    // LoadNew can't await the habit load (it's a sync entry point invoked from the workspace),
    // so this wraps the fire-and-forget call with the same error surface SaveAsync uses instead
    // of letting a failure vanish as an unobserved task exception.
    private async Task LoadHabitsFireAndForgetAsync()
    {
        try
        {
            await EnsureHabitsAsync(default).ConfigureAwait(true);
        }
        catch (Exception)
        {
            ErrorMessage = Strings.Charts_Builder_HabitsLoadError;
        }
    }

    public async ValueTask LoadAsync(Guid chartId, CancellationToken ct = default)
    {
        var chart = await _charts.GetAsync(chartId, ct).ConfigureAwait(true);
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
        var all = await _tree.GetHomeTreeAsync(ct).ConfigureAwait(true);
        AvailableHabits = all.Where(r => r.HabitType is not null).ToList();
        // Rows created before this load resolved (LoadNew fires it fire-and-forget, so a user can
        // click "Add series" first) captured the empty list at construction time and have no other
        // way to see the real one — push it into every existing row now. ChartSeriesRowViewModel's
        // AvailableHabits is an [ObservableProperty] specifically so the picker's binding reacts.
        foreach (var row in Series) row.AvailableHabits = AvailableHabits;
    }

    private Task<IReadOnlyList<MetricDefinition>> LoadMetricsAsync(Guid habitNodeId, CancellationToken ct) =>
        _metrics.ListByHabitAsync(habitNodeId, ct).AsTask();

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
        // Removing a row changes the previewed series set, so re-render (removal doesn't fire row.Changed).
        RaisePreviewRequested();
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
        RaisePreviewRequested();
    }

    private void RaiseSeriesChanged() => SeriesChanged?.Invoke(this, EventArgs.Empty);

    partial void OnNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnSelectedKindChanged(ChartKind value) => RaisePreviewRequested();
    partial void OnRangeChanged(string value) => RaisePreviewRequested();
    // Bucket feeds ChartConfig in RefreshPreviewAsync, so a bucket change must re-render too.
    partial void OnBucketChanged(string value) => RaisePreviewRequested();

    // (Re)starts the debounce: each edit pushes the single pending re-render ~300ms into the future,
    // so a burst of edits collapses to one refresh. No-op in headless tests (timer is null there).
    private void RaisePreviewRequested()
    {
        if (_previewTimer is null) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        _previewTimer?.Stop();   // one-shot: fire once per debounce window, not repeatedly
        _ = RefreshPreviewAsync();
    }

    /// <summary>Rebuilds the live preview from the current (unsaved) form state, without persisting
    /// anything. Public so tests can invoke it directly, bypassing the debounce timer.</summary>
    public async Task RefreshPreviewAsync(CancellationToken ct = default)
    {
        var rows = Series.Select((r, i) => r.ToChartSeries(i)).Where(s => s is not null).Cast<ChartSeries>().ToList();
        if (rows.Count == 0)
        {
            PreviewView = null;
            PreviewEmpty = true;
            return;
        }
        PreviewEmpty = false;
        var chart = new Chart
        {
            Name = Name,
            ChartType = ChartKindParser.ToToken(SelectedKind),
            ConfigJson = new ChartConfig(Range: Range, Bucket: Bucket).ToJson(),
        };
        try
        {
            var model = await _query.BuildModelAsync(chart, rows, IsDarkTheme, ct).ConfigureAwait(true);
            PreviewView = ChartRendererLocator.CreateView(_registry, model);
        }
        catch (Exception)
        {
            // The preview must never crash the builder; leave PreviewEmpty=false (there WERE rows).
            PreviewView = null;
        }
    }

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
                await _charts.UpdateAsync(existing, c =>
                {
                    c.Name = Name;
                    c.ChartType = ChartKindParser.ToToken(SelectedKind);
                    c.ConfigJson = configJson;
                }, ct).ConfigureAwait(true);
                await _charts.ReplaceSeriesAsync(existing, rows, ct).ConfigureAwait(true);
                id = existing;
            }
            else
            {
                id = await _charts.CreateAsync(new Chart
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
