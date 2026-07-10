using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

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
}
