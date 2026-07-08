using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.Charts;

/// <summary>A pickable metric for a chart series. A null <see cref="Id"/> means the habit's completion count.</summary>
public sealed record MetricOption(Guid? Id, string Name);

/// <summary>
/// One row of the chart builder's series editor: a habit picker plus a metric picker, with a
/// label and colour that are derived automatically until the user edits them. Producing a
/// <see cref="ChartSeries"/> requires a chosen habit — a row with no habit yields <c>null</c> from
/// <see cref="ToChartSeries"/> and is dropped at save time. This is what closes F0-1: the old row
/// persisted <c>HabitNodeId = Guid.Empty</c>, which failed the FK and crashed the builder.
/// </summary>
public sealed partial class ChartSeriesRowViewModel : ObservableObject
{
    private readonly Func<Guid, CancellationToken, Task<IReadOnlyList<MetricDefinition>>> _loadMetrics;
    private bool _labelTouched;
    private Task _metricLoad = Task.CompletedTask;

    public ChartSeriesRowViewModel(
        IReadOnlyList<HomeTreeRow> availableHabits,
        Func<Guid, CancellationToken, Task<IReadOnlyList<MetricDefinition>>> loadMetrics)
    {
        ArgumentNullException.ThrowIfNull(availableHabits);
        ArgumentNullException.ThrowIfNull(loadMetrics);
        AvailableHabits = availableHabits;
        _loadMetrics = loadMetrics;
        // Seed the Count option up front so Metrics is never empty — including for a row whose
        // SelectedHabit is assigned null while it's already null (e.g. LoadAsync resolving a
        // persisted series against a deleted habit), which CommunityToolkit's generated setter
        // treats as a no-op (old == new) and so never runs OnHabitChangedAsync at all.
        Metrics.Add(new MetricOption(null, Strings.Charts_Builder_CountMetric));
    }

    /// <summary>Habits the user can chart (groups excluded). Bound by the picker.</summary>
    public IReadOnlyList<HomeTreeRow> AvailableHabits { get; }

    /// <summary>Metric options for the selected habit: the count option first, then the real metrics.</summary>
    public ObservableCollection<MetricOption> Metrics { get; } = [];

    /// <summary>Completes when the in-flight metric load for the current habit has finished.</summary>
    public Task WhenMetricsLoaded => _metricLoad;

    /// <summary>Raised whenever something that affects the persisted / previewed series changes.</summary>
    public event EventHandler? Changed;

    [ObservableProperty] private HomeTreeRow? _selectedHabit;
    [ObservableProperty] private MetricOption? _selectedMetric;
    [ObservableProperty] private string? _label;
    [ObservableProperty] private string? _color;

    partial void OnSelectedHabitChanged(HomeTreeRow? value) => _metricLoad = OnHabitChangedAsync(value);
    partial void OnSelectedMetricChanged(MetricOption? value) { AutoLabel(); Changed?.Invoke(this, EventArgs.Empty); }
    partial void OnColorChanged(string? value) => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Called by the UI when the user edits the label by hand, so auto-labelling stops overwriting it.</summary>
    public void MarkLabelTouched() => _labelTouched = true;

    private async Task OnHabitChangedAsync(HomeTreeRow? habit)
    {
        Metrics.Clear();
        Metrics.Add(new MetricOption(null, Strings.Charts_Builder_CountMetric));
        if (habit is not null)
        {
            foreach (var m in await _loadMetrics(habit.HabitNodeId, default).ConfigureAwait(true))
                Metrics.Add(new MetricOption(m.Id, m.Name));
            SelectedMetric = Metrics.Count > 1 ? Metrics[1] : Metrics[0];
            if (string.IsNullOrWhiteSpace(Color)) Color = habit.ColorHex;
        }
        AutoLabel();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AutoLabel()
    {
        if (_labelTouched || SelectedHabit is null) return;
        Label = SelectedMetric?.Id is null ? SelectedHabit.Name : $"{SelectedHabit.Name} — {SelectedMetric.Name}";
    }

    /// <summary>Materialises the row into a persistable series, or <c>null</c> when no habit is chosen.</summary>
    public ChartSeries? ToChartSeries(int sortOrder) => SelectedHabit is null ? null : new ChartSeries
    {
        Id = Guid.NewGuid(),
        HabitNodeId = SelectedHabit.HabitNodeId,
        MetricDefinitionId = SelectedMetric?.Id,
        Label = Label,
        Color = Color,
        SortOrder = sortOrder,
    };
}
