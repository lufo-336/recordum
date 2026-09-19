using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class ScalesSectionViewModelTests
{
    private readonly FakeMetricDefinitionService _metrics = new();
    private readonly FakeReferenceData _refData = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State };

    [Fact]
    public async Task Selecting_a_scale_template_updates_primary_metric()
    {
        var metricId = Guid.NewGuid();
        await _metrics.CreateAsync(new MetricDefinition { Id = metricId, HabitNodeId = _node.Id, Name = "Mood", SortOrder = 0 });
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(_node);

        var scaleId = Guid.NewGuid();
        sut.SelectedScaleTemplateId = scaleId;
        await Task.Delay(20);

        _metrics.Store[metricId].ScaleTemplateId.Should().Be(scaleId);
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task LabelsJson_override_persists_to_primary_metric()
    {
        var metricId = Guid.NewGuid();
        await _metrics.CreateAsync(new MetricDefinition { Id = metricId, HabitNodeId = _node.Id, Name = "Mood", SortOrder = 0 });
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(_node);

        sut.LabelsJson = "[\"Bad\",\"Meh\",\"Good\"]";
        await Task.Delay(20);

        _metrics.Store[metricId].LabelsJson.Should().Be("[\"Bad\",\"Meh\",\"Good\"]");
    }

    [Fact]
    public async Task Reload_with_combobox_coercion_does_not_wipe_scale_template()
    {
        var metricId = Guid.NewGuid();
        var scaleId = _refData.Scales[0].Id;
        await _metrics.CreateAsync(new MetricDefinition
        {
            Id = metricId, HabitNodeId = _node.Id, Name = "Umore", SortOrder = 0, ScaleTemplateId = scaleId,
        });
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(_node);

        // A live ComboBox coerces SelectedValue to null the instant its ItemsSource empties;
        // mimic that view behaviour on the reload the Configura host issues when the column reopens.
        sut.AvailableScales.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                sut.SelectedScaleTemplateId = null;
        };

        await sut.LoadAsync(_node);
        await Task.Delay(20);

        _metrics.Store[metricId].ScaleTemplateId.Should().Be(scaleId);
    }

    [Fact]
    public void IsApplicable_only_for_State()
    {
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeFalse();
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public async Task Scales_LoadAsync_populates_available_scales()
    {
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State });
        sut.AvailableScales.Should().ContainSingle(s => s.Code == "mood-5");
    }
}
