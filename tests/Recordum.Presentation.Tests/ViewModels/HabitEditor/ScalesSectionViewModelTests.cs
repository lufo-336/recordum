using FluentAssertions;
using Recordum.Core.Abstractions.Services;
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
    public ScalesSectionViewModelTests() => _scales = new FakeScaleTemplateService(_refData, _metrics);

    private ScalesSectionViewModel Make() => new(_metrics, _refData, _scales);

    private readonly FakeMetricDefinitionService _metrics = new();
    private readonly FakeReferenceData _refData = new();
    private readonly FakeScaleTemplateService _scales;
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State };

    [Fact]
    public async Task Selecting_a_scale_template_updates_primary_metric()
    {
        var metricId = Guid.NewGuid();
        await _metrics.CreateAsync(new MetricDefinition { Id = metricId, HabitNodeId = _node.Id, Name = "Mood", SortOrder = 0 });
        var sut = Make();
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
        var sut = Make();
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
        var sut = Make();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeFalse();
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public async Task Scales_LoadAsync_populates_available_scales()
    {
        var sut = Make();
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
        var sut = Make();
        await sut.LoadAsync(node);
        _lastNode = node;
        return (sut, metricId);
    }

    [Fact]
    public async Task The_editor_follows_the_picked_scale_with_its_anchor_names()
    {
        var five = AddScale("custom-5", 1, 5, "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        var three = AddScale("custom-3", 0, 2, "[\"x\",\"y\",\"z\"]");
        var (sut, _) = await OpenAsync(five.Id);

        sut.Editor.Rows.Select(r => (r.Value, r.Label)).Should().Equal((1, "a"), (2, "b"), (3, "c"), (4, "d"), (5, "e"));

        sut.SelectedScaleTemplateId = three.Id;
        sut.Editor.Rows.Select(r => (r.Value, r.Label)).Should().Equal((0, "x"), (1, "y"), (2, "z"));
    }

    /// <summary>GIO-23: l'editor mostra cio' che mostra la schermata: per un'ufficiale i nomi tradotti, in sola lettura.</summary>
    [Fact]
    public async Task An_official_scale_shows_its_translated_names_read_only()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            var mood = AddScale("mood-5", 1, 5, "[\"Very low\",\"Low\",\"Neutral\",\"Good\",\"Very good\"]", official: true);
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            var (it, _) = await OpenAsync(mood.Id);
            it.Editor.Rows.Select(r => r.Label).Should().Equal("Molto basso", "Basso", "Neutro", "Buono", "Molto buono");
            it.Editor.IsReadOnly.Should().BeTrue();
            it.CanDuplicate.Should().BeTrue();
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    [Fact]
    public async Task A_scale_used_by_two_habits_says_so()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
            _scales.Usage[scale.Id] = new ScaleUsage(2, null, null, false);
            var (sut, _) = await OpenAsync(scale.Id);

            sut.Editor.UsageText.Should().Be("Usata da 2 abitudini");
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    [Fact]
    public async Task A_confirmed_edit_of_a_users_scale_is_saved_and_an_invalid_one_is_not()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, _) = await OpenAsync(scale.Id);

        sut.Editor.Rows[0].Label = "uno";
        await Task.Delay(20);
        _scales.Updates.Should().ContainSingle().Which.Draft.Anchors[0].Label.Should().Be("uno");
        sut.Status.Should().Be(EditorStatus.Saved);

        sut.Editor.Rows[1].Value = 3;       // due scaglioni sullo stesso valore: non si salva
        await Task.Delay(20);
        _scales.Updates.Should().HaveCount(1);
    }

    [Fact]
    public async Task Removing_a_mark_is_saved_without_that_anchor()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, _) = await OpenAsync(scale.Id);

        sut.Editor.RemoveRowCommand.Execute(sut.Editor.Rows[1]);
        await Task.Delay(20);

        _scales.Updates.Should().ContainSingle().Which.Draft.Anchors.Select(a => a.Value).Should().Equal(1, 3);
    }

    [Fact]
    public async Task Adding_a_mark_and_naming_every_value_are_saved()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\"]");
        var (sut, _) = await OpenAsync(scale.Id);

        sut.Editor.FillEveryValueCommand.Execute(null);
        await Task.Delay(20);

        _scales.Updates.Should().ContainSingle().Which.Draft.Anchors.Select(a => a.Value).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Saving_updates_the_picker_object_in_place_so_the_summary_follows()
    {
        var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
        var (sut, _) = await OpenAsync(scale.Id);

        sut.Editor.Name = "Nuovo nome";
        await Task.Delay(20);

        sut.Summary.Should().Be("Nuovo nome");
        sut.AvailableScales.Should().Contain(scale);
        sut.SelectedScaleTemplateId.Should().Be(scale.Id);
    }

    [Fact]
    public async Task Duplicate_and_edit_copies_an_official_scale_repoints_the_metric_and_shows_the_copy_editable()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            var nrs = AddScale("nrs-pain", 0, 10, "[\"No pain\"]", official: true);
            var (sut, metricId) = await OpenAsync(nrs.Id);
            sut.Editor.IsReadOnly.Should().BeTrue();

            await sut.DuplicateAndEditCommand.ExecuteAsync(null);

            var call = _scales.Duplicates.Should().ContainSingle().Subject;
            call.SourceId.Should().Be(nrs.Id);
            call.MetricId.Should().Be(metricId);
            call.Name.Should().Be(Recordum.Presentation.Templates.OfficialTexts.ScaleName(nrs));
            call.Anchors.Should().Contain(a => a.Value == 0 && a.Label == "Nessun dolore");
            sut.SelectedScaleTemplateId.Should().NotBe(nrs.Id).And.Be(_metrics.Store[metricId].ScaleTemplateId);
            sut.Editor.IsReadOnly.Should().BeFalse();
            sut.AvailableScales.Should().HaveCount(_refData.Scales.Count);
        }
        finally { Recordum.Presentation.Resources.Strings.Culture = previous; }
    }

    // GIO-23 review finale: cambiare scala con valori registrati che non ci stanno si rifiuta (il servizio lo dice col codice):
    // il picker torna alla scala di prima e l'errore e' la frase tradotta, non il codice.
    [Fact]
    public async Task Picking_a_scale_that_cannot_hold_the_recorded_values_puts_the_previous_one_back_with_the_reason()
    {
        var previous = (Recordum.Presentation.Resources.Strings.Culture, System.Globalization.CultureInfo.CurrentCulture);
        try
        {
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var wide = AddScale("custom-100", 0, 100, "[]");
            var narrow = AddScale("custom-5", 1, 5, "[]");
            var (sut, metricId) = await OpenAsync(wide.Id);
            _metrics.NextException = new InvalidOperationException("narrow-max") { Data = { ["recorded"] = 80m } };

            sut.SelectedScaleTemplateId = narrow.Id;
            await Task.Delay(20);

            sut.SelectedScaleTemplateId.Should().Be(wide.Id);
            _metrics.Store[metricId].ScaleTemplateId.Should().Be(wide.Id);
            sut.Status.Should().Be(EditorStatus.Error);
            sut.ErrorMessage.Should().Be("Values up to 80 are already recorded");
            sut.Editor.Name.Should().Be(wide.Name);
            sut.AvailableScales.Should().Contain([wide, narrow]);
        }
        finally { (Recordum.Presentation.Resources.Strings.Culture, System.Globalization.CultureInfo.CurrentCulture) = previous; }
    }

    [Fact]
    public async Task A_refused_scale_edit_shows_the_translated_reason_not_the_code()
    {
        var previous = Recordum.Presentation.Resources.Strings.Culture;
        try
        {
            Recordum.Presentation.Resources.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var scale = AddScale("custom-3", 1, 3, "[\"a\",\"b\",\"c\"]");
            var (sut, _) = await OpenAsync(scale.Id);
            _scales.NextException = new InvalidOperationException("precision");

            sut.Editor.Rows[0].Label = "uno";
            await Task.Delay(20);

            sut.Status.Should().Be(EditorStatus.Error);
            sut.ErrorMessage.Should().Be("Values with decimals are already recorded");
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
