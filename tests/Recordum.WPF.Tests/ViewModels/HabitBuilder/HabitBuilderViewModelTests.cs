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
        IHabitTemplateService? templates = null)
        => new(composed ?? new FakeComposedHabitService(), templates ?? new FakeHabitTemplateService(), new FakeClock());

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
    public void CanEnableSeries_is_false_when_scale_field_present()
    {
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Scale);

        sut.CanEnableSeries.Should().BeFalse();
    }
}
