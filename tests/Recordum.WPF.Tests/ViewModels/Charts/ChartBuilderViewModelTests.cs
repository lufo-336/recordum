using System.Collections.Immutable;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Charts;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Charts;

public sealed class ChartBuilderViewModelTests
{
    private static HomeTreeRow Habit(Guid id, string name, string color = "#123456", HabitType type = HabitType.Action) =>
        new(id, null, name, color, type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray<int>.Empty, ImmutableArray<Guid>.Empty);

    private static ChartBuilderViewModel BuildVm(
        FakeChartService charts, FakeHabitTreeQueryService tree, FakeMetricDefinitionService metrics) =>
        new(charts, tree, metrics, new FakeChartQueryService(), new ExtensionPointRegistry(), null);

    private static ChartSeriesRowViewModel BuildRow(
        FakeMetricDefinitionService metrics, params HomeTreeRow[] habits) =>
        new(habits, (id, ct) => metrics.ListByHabitAsync(id, ct).AsTask());

    // --- adapted regression coverage ---------------------------------------

    [Fact]
    public void AddSeries_before_habit_load_resolves_still_sees_habits_once_it_does()
    {
        // LoadNew() fires the habit-tree load fire-and-forget, so a user can click "Add series"
        // before it resolves. The row must not be stuck with the empty snapshot it was
        // constructed with — ChartBuilderViewModel pushes the loaded list into every existing row.
        var habitId = Guid.NewGuid();
        var tree = new FakeHabitTreeQueryService();
        tree.HomeRows.Add(Habit(habitId, "Reading"));
        tree.HomeGate = new TaskCompletionSource<bool>();
        var vm = BuildVm(new FakeChartService(), tree, new FakeMetricDefinitionService());

        vm.LoadNew(); // starts the load, but it's gated — still pending.
        vm.AddSeriesCommand.Execute(null); // row captures whatever AvailableHabits is right now: [].
        Assert.Empty(vm.Series[0].AvailableHabits);

        tree.HomeGate.SetResult(true); // let the load resolve.

        Assert.Single(vm.Series[0].AvailableHabits);
        Assert.Equal(habitId, vm.Series[0].AvailableHabits[0].HabitNodeId);
    }

    [Fact]
    public void AddSeries_then_RemoveSeries_mutates_collection()
    {
        var vm = BuildVm(new FakeChartService(), new FakeHabitTreeQueryService(), new FakeMetricDefinitionService());
        vm.LoadNew(); // seeds one row (W1a-VF2)
        vm.AddSeriesCommand.Execute(null);
        vm.AddSeriesCommand.Execute(null);
        Assert.Equal(3, vm.Series.Count);
        vm.RemoveSeriesCommand.Execute(vm.Series[0]);
        Assert.Equal(2, vm.Series.Count);
    }

    [Fact]
    public void LoadNew_seeds_one_empty_series_row_so_the_habit_picker_is_visible()
    {
        // W1a-VF2: a brand-new chart used to show no series row at all — the user had no way to
        // see where the habit is picked. LoadNew now seeds one empty row (and re-seeds on reuse).
        var vm = BuildVm(new FakeChartService(), new FakeHabitTreeQueryService(), new FakeMetricDefinitionService());
        vm.LoadNew();
        Assert.Single(vm.Series);
        Assert.Null(vm.Series[0].SelectedHabit);

        vm.AddSeriesCommand.Execute(null);
        vm.LoadNew(); // reused builder: back to exactly one seeded row
        Assert.Single(vm.Series);
    }

    [Fact]
    public async Task Save_persists_chart_and_skips_rows_without_habit()
    {
        var svc = new FakeChartService();
        var habitId = Guid.NewGuid();
        var tree = new FakeHabitTreeQueryService();
        tree.HomeRows.Add(Habit(habitId, "Reading"));
        var vm = BuildVm(svc, tree, new FakeMetricDefinitionService());
        vm.LoadNew();
        vm.Name = "My chart";
        vm.SelectedKind = ChartKind.Bar;
        vm.AddSeriesCommand.Execute(null);   // valid row (habit selected below)
        vm.AddSeriesCommand.Execute(null);   // invalid row (no habit) -> skipped
        vm.Series[0].SelectedHabit = vm.Series[0].AvailableHabits[0];

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(svc.Created);
        Assert.Equal("bar", svc.Created[0].chart.ChartType);
        Assert.Equal("My chart", svc.Created[0].chart.Name);
        Assert.Single(svc.Created[0].series);                       // invalid row skipped
        Assert.Equal(habitId, svc.Created[0].series[0].HabitNodeId);
    }

    [Fact]
    public async Task Save_existing_updates_and_replaces_series()
    {
        var svc = new FakeChartService();
        var existing = new Chart { Id = Guid.NewGuid(), Name = "Old", ChartType = "line" };
        svc.Store[existing.Id] = existing;
        var vm = BuildVm(svc, new FakeHabitTreeQueryService(), new FakeMetricDefinitionService());
        await vm.LoadAsync(existing.Id);
        vm.Name = "New";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("New", svc.Store[existing.Id].Name);
        Assert.Contains(existing.Id, svc.SeriesReplaced);
    }

    // --- new: LoadAsync reconstructs SelectedHabit/SelectedMetric from persisted ids ------------

    [Fact]
    public async Task LoadAsync_reconstructs_selected_habit_and_metric_from_persisted_series()
    {
        var habitId = Guid.NewGuid();
        var lettura = Habit(habitId, "Lettura", "#8a64e0", HabitType.Journal);
        var otherId = Guid.NewGuid();
        var other = Habit(otherId, "Other", "#334455");
        var tree = new FakeHabitTreeQueryService();
        tree.HomeRows.Add(lettura);
        tree.HomeRows.Add(other);

        var metricsSvc = new FakeMetricDefinitionService();
        var pagineId = Guid.NewGuid();
        metricsSvc.Store[pagineId] = new MetricDefinition { Id = pagineId, HabitNodeId = habitId, Name = "Pagine" };

        var svc = new FakeChartService();
        var existing = new Chart { Id = Guid.NewGuid(), Name = "Old", ChartType = "line" };
        existing.Series.Add(new ChartSeries
        {
            Id = Guid.NewGuid(),
            ChartId = existing.Id,
            HabitNodeId = habitId,
            MetricDefinitionId = pagineId,
            Label = "Custom label",
            Color = "#ff0000",
            SortOrder = 0,
        });
        svc.Store[existing.Id] = existing;
        var vm = BuildVm(svc, tree, metricsSvc);

        await vm.LoadAsync(existing.Id);

        Assert.Single(vm.Series);
        var row = vm.Series[0];
        Assert.Equal(habitId, row.SelectedHabit?.HabitNodeId);
        Assert.Equal(pagineId, row.SelectedMetric?.Id);
        Assert.Equal("Custom label", row.Label);   // persisted label kept, not re-derived as "Lettura — Pagine"
        Assert.Equal("#ff0000", row.Color);        // persisted color kept, not re-derived from habit's #8a64e0

        // The restored label is locked in: re-selecting a different habit afterward must not clobber it.
        row.SelectedHabit = row.AvailableHabits.First(h => h.HabitNodeId == otherId);
        await row.WhenMetricsLoaded;
        Assert.Equal("Custom label", row.Label);
    }

    [Fact]
    public async Task LoadAsync_handles_a_persisted_series_whose_habit_no_longer_exists()
    {
        var deletedHabitId = Guid.NewGuid();
        var tree = new FakeHabitTreeQueryService(); // the habit is gone: not present in the tree
        var svc = new FakeChartService();
        var existing = new Chart { Id = Guid.NewGuid(), Name = "Old", ChartType = "line" };
        existing.Series.Add(new ChartSeries
        {
            Id = Guid.NewGuid(),
            ChartId = existing.Id,
            HabitNodeId = deletedHabitId,
            MetricDefinitionId = null,
            Label = "Ghost",
            Color = "#000000",
            SortOrder = 0,
        });
        svc.Store[existing.Id] = existing;
        var vm = BuildVm(svc, tree, new FakeMetricDefinitionService());

        // Must not throw even though the habit referenced by the persisted series is gone.
        await vm.LoadAsync(existing.Id);

        Assert.Single(vm.Series);
        Assert.Null(vm.Series[0].SelectedHabit);
        Assert.Null(vm.Series[0].ToChartSeries(0)); // orphaned row is dropped, never resurrected with Guid.Empty
    }

    // --- new: habit picker fills metrics + auto label/color -----------------

    [Fact]
    public async Task Selecting_habit_fills_metrics_label_and_color()
    {
        var habitId = Guid.NewGuid();
        var lettura = Habit(habitId, "Lettura", "#8a64e0", HabitType.Journal);
        var metrics = new FakeMetricDefinitionService();
        var pagineId = Guid.NewGuid();
        metrics.Store[pagineId] = new MetricDefinition { Id = pagineId, HabitNodeId = habitId, Name = "Pagine" };
        var row = BuildRow(metrics, lettura);

        row.SelectedHabit = lettura;
        await row.WhenMetricsLoaded;

        // Count option first (null id), then the real metric.
        Assert.Equal(2, row.Metrics.Count);
        Assert.Null(row.Metrics[0].Id);
        Assert.Equal(pagineId, row.Metrics[1].Id);
        Assert.Equal("Pagine", row.Metrics[1].Name);
        // Auto-selected the real metric, auto-labelled, auto-coloured.
        Assert.Equal(pagineId, row.SelectedMetric?.Id);
        Assert.Equal("Lettura — Pagine", row.Label);
        Assert.Equal("#8a64e0", row.Color);
    }

    [Fact]
    public async Task Selecting_habit_without_metrics_auto_selects_count()
    {
        var habitId = Guid.NewGuid();
        var walking = Habit(habitId, "Walking", "#22aa55");
        var row = BuildRow(new FakeMetricDefinitionService(), walking);

        row.SelectedHabit = walking;
        await row.WhenMetricsLoaded;

        Assert.Single(row.Metrics);
        Assert.Null(row.Metrics[0].Id);            // only the Count option
        Assert.Same(row.Metrics[0], row.SelectedMetric);
        Assert.Equal("Walking", row.Label);        // no metric -> habit name only
        Assert.Equal("#22aa55", row.Color);
    }

    // --- new: hand-edited label is preserved --------------------------------

    [Fact]
    public async Task Hand_edited_label_is_preserved()
    {
        var lettura = Habit(Guid.NewGuid(), "Lettura", "#8a64e0");
        var other = Habit(Guid.NewGuid(), "Other", "#112233");
        var row = BuildRow(new FakeMetricDefinitionService(), lettura, other);

        row.SelectedHabit = lettura;
        await row.WhenMetricsLoaded;
        Assert.Equal("Lettura", row.Label);        // auto-labelled before the user touches it

        row.Label = "mio";
        row.MarkLabelTouched();                     // UI signals a hand edit

        row.SelectedHabit = other;
        await row.WhenMetricsLoaded;
        Assert.Equal("mio", row.Label);             // no longer overwritten
    }

    // --- new: CanSave gating -------------------------------------------------

    [Fact]
    public void CanSave_requires_name_and_valid_series()
    {
        var tree = new FakeHabitTreeQueryService();
        tree.HomeRows.Add(Habit(Guid.NewGuid(), "Reading"));
        var vm = BuildVm(new FakeChartService(), tree, new FakeMetricDefinitionService());
        vm.LoadNew();

        Assert.False(vm.CanSave);                   // no name, no series

        vm.Name = "My chart";
        Assert.False(vm.CanSave);                   // name present, but no valid series

        vm.AddSeriesCommand.Execute(null);
        Assert.False(vm.CanSave);                   // series present, but no habit selected

        vm.Series[0].SelectedHabit = vm.Series[0].AvailableHabits[0];
        Assert.True(vm.CanSave);                    // both satisfied

        vm.Name = "   ";
        Assert.False(vm.CanSave);                   // whitespace name fails again
    }

    // --- new: save is safe when the service throws --------------------------

    [Fact]
    public async Task Save_skips_invalid_rows_and_reports_errors()
    {
        var svc = new FakeChartService { NextException = new InvalidOperationException("boom") };
        var tree = new FakeHabitTreeQueryService();
        tree.HomeRows.Add(Habit(Guid.NewGuid(), "Reading"));
        var vm = BuildVm(svc, tree, new FakeMetricDefinitionService());
        vm.LoadNew();
        vm.Name = "My chart";
        vm.AddSeriesCommand.Execute(null);   // invalid (no habit) -> skipped
        vm.AddSeriesCommand.Execute(null);   // valid
        vm.Series[1].SelectedHabit = vm.Series[1].AvailableHabits[0];

        // Must not throw even though CreateAsync throws.
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Save_existing_reports_error_when_update_throws()
    {
        var svc = new FakeChartService();
        var existing = new Chart { Id = Guid.NewGuid(), Name = "Old", ChartType = "line" };
        svc.Store[existing.Id] = existing;
        var vm = BuildVm(svc, new FakeHabitTreeQueryService(), new FakeMetricDefinitionService());
        await vm.LoadAsync(existing.Id);
        vm.Name = "New";
        svc.NextException = new InvalidOperationException("boom"); // throws from UpdateAsync this time

        // Must not throw even though UpdateAsync throws.
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal("Old", svc.Store[existing.Id].Name); // update never applied
    }
}
