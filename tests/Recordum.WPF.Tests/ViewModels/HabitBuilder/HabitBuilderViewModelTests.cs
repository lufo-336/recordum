using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitBuilder;

public class HabitBuilderViewModelTests
{
    private static HabitBuilderViewModel NewSut(IComposedHabitService? composed = null)
        => new(composed ?? new FakeComposedHabitService(), new FakeHabitTemplateService(), new FakeClock());

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
}
