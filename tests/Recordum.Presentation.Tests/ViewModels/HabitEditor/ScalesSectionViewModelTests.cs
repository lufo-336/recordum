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

    private HabitNode _lastNode = null!;

    private ScaleTemplate AddScale(string code, int min, int max, string labelsJson, bool official = false)
    {
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = code, Name = code, MinValue = min, MaxValue = max, LabelsJson = labelsJson, IsOfficial = official };
        _refData.Scales.Add(scale);
        return scale;
    }

    private async Task<(ScalesSectionViewModel Sut, Guid MetricId)> OpenAsync(Guid? scaleId, string? labelsJson = null)
    {
        var metricId = Guid.NewGuid();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State };
        await _metrics.CreateAsync(new MetricDefinition
        {
            Id = metricId, HabitNodeId = node.Id, Name = "Mood", SortOrder = 0, ScaleTemplateId = scaleId, LabelsJson = labelsJson,
        });
        var sut = new ScalesSectionViewModel(_metrics, _refData);
        await sut.LoadAsync(node);
        _lastNode = node;
        return (sut, metricId);
    }

    [Fact]
    public async Task Rows_follow_the_scale_range_and_show_the_metric_override_before_the_scale_labels()
    {
        var five = AddScale("custom-5", 1, 5, "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        var three = AddScale("custom-3", 0, 2, "[\"x\",\"y\",\"z\"]");
        var (sut, _) = await OpenAsync(five.Id);

        sut.LabelsEditor.Rows.Select(r => (r.Value, r.Text)).Should().Equal((1, "a"), (2, "b"), (3, "c"), (4, "d"), (5, "e"));

        sut.SelectedScaleTemplateId = three.Id;
        sut.LabelsEditor.Rows.Select(r => (r.Value, r.Text)).Should().Equal((0, "x"), (1, "y"), (2, "z"));

        var (withOverride, _) = await OpenAsync(five.Id, "[\"1\",\"2\",\"3\",\"4\",\"5\"]");
        withOverride.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("1", "2", "3", "4", "5");
    }

    /// <summary>UX3A-2: senza override l'editor mostra cio' che mostra la schermata (<c>m.LabelsJson ?? tmpl.LabelsJson</c>),
    /// cioe' le etichette seminate, in qualunque lingua: nessun riempimento a ritroso (Review Focus 5).</summary>
    [Fact]
    public async Task An_official_scale_without_override_shows_the_seeded_labels_like_the_screen()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            var mood = AddScale("mood-5", 1, 5, "[\"Very low\",\"Low\",\"Neutral\",\"Good\",\"Very good\"]", official: true);
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            var (it, _) = await OpenAsync(mood.Id);
            it.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("Very low", "Low", "Neutral", "Good", "Very good");

            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var (en, _) = await OpenAsync(mood.Id);
            en.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("Very low", "Low", "Neutral", "Good", "Very good");
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    /// <summary>UX3A-2: passare a una scala ufficiale la fa nascere con le etichette nella lingua di adesso (Decisione 3),
    /// sulla metrica e nello stesso UpdateAsync: la schermata e Configura dicono la stessa cosa.</summary>
    [Fact]
    public async Task Switching_to_an_official_scale_writes_its_labels_in_the_interface_language_in_one_write()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            var custom = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
            var mood = AddScale("mood-5", 1, 5, "[\"Very low\",\"Low\",\"Neutral\",\"Good\",\"Very good\"]", official: true);
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            var (sut, metricId) = await OpenAsync(custom.Id, "[\"x\",\"y\",\"z\"]");
            var before = _metrics.UpdateCallCount;

            sut.SelectedScaleTemplateId = mood.Id;
            await Task.Delay(30);

            const string italian = "[\"Molto basso\",\"Basso\",\"Neutro\",\"Buono\",\"Molto buono\"]";
            _metrics.Store[metricId].ScaleTemplateId.Should().Be(mood.Id);
            _metrics.Store[metricId].LabelsJson.Should().Be(italian);
            _metrics.UpdateCallCount.Should().Be(before + 1);
            sut.LabelsJson.Should().Be(italian);
            sut.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("Molto basso", "Basso", "Neutro", "Buono", "Molto buono");
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    [Fact]
    public async Task Editing_a_label_saves_the_metric_labels()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, metricId) = await OpenAsync(scale.Id);

        sut.LabelsEditor.Rows[1].Text = "Qualità";
        await Task.Delay(20);

        _metrics.Store[metricId].LabelsJson.Should().Be("[\"a\",\"Qualità\",\"c\"]");
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task Loading_does_not_write()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, _) = await OpenAsync(scale.Id, "[\"x\",\"y\",\"z\"]");
        var before = _metrics.UpdateCallCount;

        await sut.LoadAsync(_lastNode);                  // la riapertura di Configura
        sut.SelectedScaleTemplateId = _refData.Scales[0].Id;   // e un cambio di scala: ricostruisce le righe, non scrive i testi
        await Task.Delay(20);

        _metrics.UpdateCallCount.Should().Be(before + 1);   // solo la scelta della scala, mai le etichette
    }

    [Fact]
    public async Task All_empty_labels_clear_the_override()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, metricId) = await OpenAsync(scale.Id, "[\"x\",\"y\",\"z\"]");

        foreach (var row in sut.LabelsEditor.Rows.ToList()) row.Text = "";
        await Task.Delay(50);

        _metrics.Store[metricId].LabelsJson.Should().BeNull();
        sut.LabelsJson.Should().BeNull();
    }

    [Fact]
    public async Task Switching_the_scale_resets_the_custom_labels_in_one_write()
    {
        var five = AddScale("custom-5", 1, 5, "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        var ten = AddScale("custom-10", 1, 10, "[\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\",\"9\",\"ten\"]");
        var (sut, metricId) = await OpenAsync(five.Id, "[\"x\",\"y\",\"z\",\"w\",\"v\"]");
        var before = _metrics.UpdateCallCount;

        sut.SelectedScaleTemplateId = ten.Id;
        await Task.Delay(30);

        _metrics.Store[metricId].ScaleTemplateId.Should().Be(ten.Id);
        _metrics.Store[metricId].LabelsJson.Should().BeNull();
        _metrics.UpdateCallCount.Should().Be(before + 1);
        sut.LabelsJson.Should().BeNull();
        sut.LabelsEditor.Rows.Should().HaveCount(10);
        sut.LabelsEditor.Rows[9].Text.Should().Be("ten");
    }

    [Fact]
    public async Task Clearing_every_row_shows_the_scale_labels_again()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, metricId) = await OpenAsync(scale.Id, "[\"x\",\"y\",\"z\"]");

        foreach (var row in sut.LabelsEditor.Rows.ToList()) row.Text = "";
        await Task.Delay(50);

        _metrics.Store[metricId].LabelsJson.Should().BeNull();
        sut.LabelsEditor.Rows.Select(r => r.Text).Should().Equal("a", "b", "c");
    }
}
