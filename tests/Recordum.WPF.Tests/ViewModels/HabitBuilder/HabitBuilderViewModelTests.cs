using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitBuilder;

public class HabitBuilderViewModelTests
{
    private static HabitBuilderViewModel NewSut(
        IComposedHabitService? composed = null,
        IHabitTemplateService? templates = null,
        IReferenceDataQueryService? refData = null)
        => new(composed ?? new FakeComposedHabitService(), templates ?? new FakeHabitTemplateService(),
               refData ?? new FakeReferenceData(), new FakeClock());

    [WpfFact]
    public void Adding_count_then_time_then_series_yields_resistance_and_preview()
    {
        var sut = NewSut();
        sut.Name = "Flessioni";
        sut.AddFieldCommand.Execute(FieldNature.Count);
        sut.AddFieldCommand.Execute(FieldNature.Time);
        sut.ToggleSeriesCommand.Execute(true);
        sut.SeriesCount = 3;

        sut.DerivedType.Should().Be(HabitType.Resistance);
        sut.Preview.Rows.Should().HaveCount(3);
        sut.Preview.Rows[0].CellNames.Should().HaveCount(2);
    }

    [WpfFact]
    public void Checking_series_directly_updates_derived_type_and_preview()
    {
        // The Series CheckBox binds SeriesEnabled TwoWay (not the ToggleSeries command), so
        // setting the property directly must still recompute the derived type + preview. (B13)
        var sut = NewSut();
        sut.Name = "Flessioni";
        sut.AddFieldCommand.Execute(FieldNature.Count);
        sut.DerivedType.Should().Be(HabitType.Counter);

        sut.SeriesEnabled = true;

        sut.DerivedType.Should().Be(HabitType.Resistance);
        sut.Preview.Rows.Should().HaveCount(sut.SeriesCount);
    }

    [WpfFact]
    public async Task Launch_after_checking_series_directly_persists_resistance()
    {
        // End-to-end B13: check Series via the property (the binding path), then Launch — the
        // persisted node must be Resistance, not a stale Counter.
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Flessioni";
        sut.AddFieldCommand.Execute(FieldNature.Count);
        sut.SeriesEnabled = true;

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest!.Node.HabitType.Should().Be(HabitType.Resistance);
    }

    [WpfFact]
    public void Text_field_blocks_count_and_makes_journal()
    {
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Text);
        sut.CanAddField(FieldNature.Count).Should().BeFalse();
        sut.DerivedType.Should().Be(HabitType.Journal);
    }

    [WpfFact]
    public async Task Launch_persists_via_composed_service_with_derived_type()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        // Quantity requires a unit for CanLaunch — set one on the field:
        sut.Fields[0].UnitOfMeasureId = Guid.NewGuid();

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.Node.HabitType.Should().Be(HabitType.Counter);
        fake.LastRequest.Metrics.Should().ContainSingle();
        sut.CreatedHabitNodeId.Should().Be(fake.LastRequest.Node.Id);
    }

    [WpfFact]
    public async Task SaveAsTemplate_creates_custom_template_with_roundtrippable_payload()
    {
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Flessioni";
        sut.AddFieldCommand.Execute(FieldNature.Count);
        sut.ToggleSeriesCommand.Execute(true);

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        saved.IsOfficial.Should().BeFalse();
        saved.Name.Should().Be("Flessioni");
        saved.PayloadJson.Should().NotBe("{}").And.Contain(Strings.HabitBuilder_DefaultField_Count); // default Count field name

        // Round-trip: the payload must match the shape HabitTemplateService.InstantiateAsync reads
        // (snake_case keys: node.habit_type + metrics[].name/type). Series → Resistance.
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        var root = doc.RootElement;
        root.GetProperty("node").GetProperty("habit_type").GetString().Should().Be("Resistance");
        var metrics = root.GetProperty("metrics");
        metrics.GetArrayLength().Should().Be(1);
        metrics[0].GetProperty("name").GetString().Should().Be(Strings.HabitBuilder_DefaultField_Count);
        metrics[0].GetProperty("type").GetString().Should().Be("Integer");
    }

    [WpfFact]
    public void Picking_a_unit_re_enables_the_launch_command()
    {
        var sut = NewSut();
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.CanLaunch.Should().BeFalse(); // unit not yet picked
        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.Fields[0].UnitOfMeasureId = Guid.NewGuid();

        fired.Should().BeTrue(); // button must be told to re-query
        sut.LaunchCommand.CanExecute(null).Should().BeTrue();
    }

    [WpfFact]
    public void Picking_a_scale_re_enables_the_launch_command()
    {
        var sut = NewSut();
        sut.Name = "Umore";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.Fields[0].ScaleTemplateId = Guid.NewGuid();

        fired.Should().BeTrue();
        sut.LaunchCommand.CanExecute(null).Should().BeTrue();
    }

    [WpfFact]
    public void CanEnableSeries_is_false_when_scale_field_present()
    {
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Scale);

        sut.CanEnableSeries.Should().BeFalse();
    }

    [WpfFact]
    public async Task InitializeAsync_loads_units_and_scales_for_pickers()
    {
        var refData = new FakeReferenceData();
        var sut = NewSut(refData: refData);

        await sut.InitializeAsync();

        sut.AvailableUnits.Should().BeEquivalentTo(refData.Units);
        sut.AvailableScales.Should().BeEquivalentTo(refData.Scales);
    }

    [WpfFact]
    public async Task Template_payload_writes_unit_code_for_quantity_field()
    {
        var refData = new FakeReferenceData();
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates, refData: refData);
        await sut.InitializeAsync();
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Fields[0].UnitOfMeasureId = refData.Units[0].Id;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        var metric = doc.RootElement.GetProperty("metrics")[0];
        metric.GetProperty("unit").GetString().Should().Be(refData.Units[0].Code);
    }

    [WpfFact]
    public async Task Template_payload_writes_scale_code_for_scale_field()
    {
        var refData = new FakeReferenceData();
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates, refData: refData);
        await sut.InitializeAsync();
        sut.Name = "Umore";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        sut.Fields[0].ScaleTemplateId = refData.Scales[0].Id;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        var metric = doc.RootElement.GetProperty("metrics")[0];
        metric.GetProperty("scale_code").GetString().Should().Be(refData.Scales[0].Code);
    }
}
