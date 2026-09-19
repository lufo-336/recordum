using System.Collections.Specialized;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class MetricsSectionViewModelTests
{
    private readonly FakeMetricDefinitionService _metrics = new();
    private readonly FakeReferenceData _refData = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter };

    private MetricsSectionViewModel CreateSut()
    {
        var sut = new MetricsSectionViewModel(_metrics, _refData);
        sut.LoadAsync(_node).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task AddMetricCommand_persists_and_appends_to_collection()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        sut.Metrics.Should().HaveCount(1);
        _metrics.Store.Should().ContainKey(sut.Metrics[0].Id);
    }

    [Fact]
    public async Task RemoveMetricCommand_removes_from_service_and_collection()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];

        await sut.RemoveMetricCommand.ExecuteAsync(row);

        sut.Metrics.Should().BeEmpty();
        _metrics.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task Renaming_row_persists_via_UpdateAsync()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];

        row.Name = "Reps";
        await Task.Delay(20);

        _metrics.Store[row.Id].Name.Should().Be("Reps");
    }

    [Fact]
    public async Task LoadAsync_orders_metrics_by_SortOrder()
    {
        await _metrics.CreateAsync(new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = _node.Id, Name = "B", SortOrder = 1 });
        await _metrics.CreateAsync(new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = _node.Id, Name = "A", SortOrder = 0 });

        var sut = CreateSut();
        sut.Metrics.Select(m => m.Name).Should().Equal("A", "B");
    }

    [Fact]
    public async Task Reload_with_combobox_coercion_does_not_wipe_metric_units()
    {
        var metricId = Guid.NewGuid();
        var unitId = _refData.Units[0].Id;
        await _metrics.CreateAsync(new MetricDefinition
        {
            Id = metricId, HabitNodeId = _node.Id, Name = "Litri", SortOrder = 0, UnitOfMeasureId = unitId,
        });
        var sut = new MetricsSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(_node);
        var previousRows = sut.Metrics.ToList();

        // Every row's unit ComboBox shares AvailableUnits as ItemsSource: emptying it coerces the
        // live rows' SelectedValue (UnitOfMeasureId) to null through the two-way binding.
        sut.AvailableUnits.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                foreach (var row in previousRows) row.UnitOfMeasureId = null;
        };

        await sut.LoadAsync(_node);
        await Task.Delay(20);

        _metrics.Store[metricId].UnitOfMeasureId.Should().Be(unitId);
    }

    [Fact]
    public void IsApplicable_only_for_Counter_and_Resistance()
    {
        var sut = new MetricsSectionViewModel(_metrics, _refData);
        sut.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeFalse();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeFalse();
    }

    [Fact]
    public async Task Metrics_LoadAsync_populates_available_units()
    {
        var sut = new MetricsSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter });
        sut.AvailableUnits.Should().ContainSingle(u => u.Code == "l");
    }

    [Fact]
    public async Task Switching_a_metric_to_duration_assigns_minutes()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        var minutes = _refData.Units.Single(u => u.Code == "minutes");
        row.UnitOfMeasureId.Should().Be(minutes.Id);
        _metrics.Store[row.Id].UnitOfMeasureId.Should().Be(minutes.Id);
        _metrics.Store[row.Id].Type.Should().Be(MetricType.Seconds);
    }

    [Fact]
    public async Task Switching_to_duration_discards_a_non_time_unit()
    {
        // A metric that was a quantity in litres keeps nothing meaningful when it becomes a duration.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        row.UnitOfMeasureId = _refData.Units.Single(u => u.Code == "l").Id;
        await Task.Delay(20);

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        row.UnitOfMeasureId.Should().Be(_refData.Units.Single(u => u.Code == "minutes").Id);
    }

    [Fact]
    public async Task Switching_to_duration_keeps_a_time_unit_already_chosen()
    {
        // Overwriting "hours" with minutes would silently throw away a choice the user made.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        var hours = _refData.Units.Single(u => u.Code == "hours");
        row.UnitOfMeasureId = hours.Id;
        await Task.Delay(20);

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        row.UnitOfMeasureId.Should().Be(hours.Id);
    }

    [Fact]
    public async Task A_duration_row_offers_only_time_units_and_a_quantity_row_offers_all()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];

        row.Units.Select(u => u.Code).Should().BeEquivalentTo(_refData.Units.Select(u => u.Code));

        row.Type = MetricType.Seconds;

        row.Units.Select(u => u.Code).Should().BeEquivalentTo(new[] { "hours", "minutes", "seconds" });
    }

    [Fact]
    public async Task RebuildUnits_coercion_on_type_change_to_duration_does_not_wipe_the_unit()
    {
        // RebuildUnits() clears the row's own Units collection, which is the ComboBox's live
        // ItemsSource. Simulate WPF's SelectedValue coercion on Reset the same way the existing
        // reload regression test does, but on the row's own Units instead of the section's
        // AvailableUnits — that's the door this finding walks through instead.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        row.Units.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) row.UnitOfMeasureId = null;
        };

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        var minutes = _refData.Units.Single(u => u.Code == "minutes");
        row.UnitOfMeasureId.Should().Be(minutes.Id);
        _metrics.Store[row.Id].UnitOfMeasureId.Should().Be(minutes.Id);
    }

    [Fact]
    public async Task RebuildUnits_coercion_on_type_change_between_non_duration_types_does_not_wipe_the_unit()
    {
        // The insidious half of the finding: a type change with no duration involved at all used to
        // clear the row's Units list too (RebuildUnits repopulates it for every type), coercing away a
        // perfectly good unit that had nothing to do with the type change.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        var litres = _refData.Units.Single(u => u.Code == "l");
        row.UnitOfMeasureId = litres.Id;
        await Task.Delay(20);
        row.Units.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) row.UnitOfMeasureId = null;
        };

        row.Type = MetricType.Decimal; // any non-duration type change away from the Integer default
        await Task.Delay(20);

        row.UnitOfMeasureId.Should().Be(litres.Id);
        _metrics.Store[row.Id].UnitOfMeasureId.Should().Be(litres.Id);
    }

    [Fact]
    public async Task Type_change_to_duration_persists_type_and_unit_in_a_single_UpdateAsync_call()
    {
        // The type+unit write is combined into ONE UpdateAsync specifically to avoid two concurrent
        // read-modify-writes racing on separate DbContext instances (IDbContextFactory) and losing one
        // of the two changes. RebuildUnits()'s own coercion defense must not add a second call.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        var before = _metrics.UpdateCallCount;

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        (_metrics.UpdateCallCount - before).Should().Be(1);
    }

    [Fact]
    public async Task A_legacy_duration_keeps_its_foreign_unit_in_the_list()
    {
        // A duration carrying "l" from before the rule: dropping it from the row's list would leave the
        // ComboBox with a SelectedValue that matches no item, WPF would coerce it to null and the
        // attached row would persist that null — the 4d01f9c data-wipe through another door. No backfill
        // happens on load either: the metric keeps its foreign unit until the user picks a valid one.
        var litres = _refData.Units.Single(u => u.Code == "l");
        var metricId = Guid.NewGuid();
        await _metrics.CreateAsync(new MetricDefinition
        {
            Id = metricId, HabitNodeId = _node.Id, Name = "Vecchia durata",
            Type = MetricType.Seconds, UnitOfMeasureId = litres.Id, SortOrder = 0,
        });

        var sut = CreateSut();
        await Task.Delay(20);
        var row = sut.Metrics.Single();

        row.Units.Should().Contain(u => u.Id == litres.Id);
        row.UnitOfMeasureId.Should().Be(litres.Id);
        _metrics.Store[metricId].UnitOfMeasureId.Should().Be(litres.Id);
    }
}
