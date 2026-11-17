using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

[Collection(ProcessGlobalCultureCollection.Name)]
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

    private HabitNode _lastNode = null!;

    private ScaleTemplate AddScale(string code, int min, int max, string labelsJson, bool official = false)
    {
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = code, Name = code, MinValue = min, MaxValue = max, IsOfficial = official, Anchors = System.Text.Json.JsonSerializer.Deserialize<string[]>(labelsJson)!.Select((l, i) => new ScaleAnchor { Value = min + i, Label = l }).ToList() };
        _refData.Scales.Add(scale);
        return scale;
    }

    private async Task<(ScalesSectionViewModel Sut, Guid MetricId)> OpenAsync(Guid? scaleId)
    {
        var metricId = Guid.NewGuid();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State };
        await _metrics.CreateAsync(new MetricDefinition
        {
            Id = metricId, HabitNodeId = node.Id, Name = "Mood", SortOrder = 0, ScaleTemplateId = scaleId,
        });
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(node);
        _lastNode = node;
        return (sut, metricId);
    }

    [Fact]
    public async Task Rows_follow_the_scale_range_and_show_its_anchor_names()
    {
        var five = AddScale("custom-5", 1, 5, "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        var three = AddScale("custom-3", 0, 2, "[\"x\",\"y\",\"z\"]");
        var (sut, _) = await OpenAsync(five.Id);

        sut.LabelsEditor.Rows.Select(r => (r.Value, r.Text)).Should().Equal((1, "a"), (2, "b"), (3, "c"), (4, "d"), (5, "e"));

        sut.SelectedScaleTemplateId = three.Id;
        sut.LabelsEditor.Rows.Select(r => (r.Value, r.Text)).Should().Equal((0, "x"), (1, "y"), (2, "z"));
    }

    /// <summary>GIO-23: l'editor mostra cio' che mostra la schermata, i nomi da <c>ScaleText.Named</c>: per un'ufficiale
    /// quelli tradotti nella lingua di adesso.</summary>
    [Fact]
    public async Task An_official_scale_shows_its_translated_names_like_the_screen()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            var mood = AddScale("mood-5", 1, 5, "[\"Very low\",\"Low\",\"Neutral\",\"Good\",\"Very good\"]", official: true);
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            var (it, _) = await OpenAsync(mood.Id);
            it.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("Molto basso", "Basso", "Neutro", "Buono", "Molto buono");

            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var (en, _) = await OpenAsync(mood.Id);
            en.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("Very low", "Low", "Neutral", "Good", "Very good");
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    [Fact]
    public async Task Loading_does_not_write()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, _) = await OpenAsync(scale.Id);
        var before = _metrics.UpdateCallCount;

        await sut.LoadAsync(_lastNode);                  // la riapertura di Configura
        sut.SelectedScaleTemplateId = _refData.Scales[0].Id;   // e un cambio di scala: ricostruisce le righe, non scrive i testi
        await Task.Delay(20);

        _metrics.UpdateCallCount.Should().Be(before + 1);   // solo la scelta della scala
    }
}
