using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.QuickAdd;

public class SeriesCellViewModelTests
{
    private static MetricDefinition Measured(string name = "Reps") =>
        new() { Id = Guid.NewGuid(), Name = name, Type = MetricType.Integer };

    private static MetricDefinition Scale(string name = "Mood") =>
        new() { Id = Guid.NewGuid(), Name = name, Type = MetricType.Integer, ScaleTemplateId = Guid.NewGuid() };

    [Fact]
    public void Kind_is_scale_when_metric_has_scale_template()
    {
        SeriesCellViewModel.ResolveKind(Scale()).Should().Be(MetricInputKind.Scale);
        SeriesCellViewModel.ResolveKind(Measured()).Should().Be(MetricInputKind.Measured);
        SeriesCellViewModel.ResolveKind(new MetricDefinition { Type = MetricType.Text }).Should().Be(MetricInputKind.Text);
    }

    [Fact]
    public void Measured_cell_carries_unit_label()
    {
        var cell = new SeriesCellViewModel(Measured(), unitCode: "ml");
        cell.Kind.Should().Be(MetricInputKind.Measured);
        cell.UnitLabel.Should().Be("ml");
        cell.IntegerValue = 30;
        cell.BuildValue(0)!.ValueInteger.Should().Be(30);
    }

    [Fact]
    public void Scale_cell_builds_one_based_value()
    {
        var info = new ScaleInfo(1, 5, new[] { "Bad", "Meh", "Ok", "Good", "Great" });
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.Kind.Should().Be(MetricInputKind.Scale);
        cell.StepCount.Should().Be(5);
        cell.ShowReferencePoints.Should().BeTrue();                       // small scale → caption every step
        cell.ReferencePoints.Should().Equal("Bad", "Meh", "Ok", "Good", "Great");
        cell.SelectedScaleIndex.Should().Be(0);                           // first step selected by default
        cell.BuildValue(0)!.ValueInteger.Should().Be(1);                  // 1-based: first step records 1, not 0

        cell.SelectedScaleIndex = 3;
        cell.BuildValue(0)!.ValueInteger.Should().Be(4); // 1-based: step index 3 → value 4
    }

    [Fact]
    public void Labelless_scale_uses_its_bare_numbers_as_reference_points()
    {
        var info = new ScaleInfo(1, 10, Array.Empty<string>());
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.StepCount.Should().Be(10);
        cell.ShowReferencePoints.Should().BeTrue();
        cell.ReferencePoints.Should().Equal("1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
    }

    [Fact]
    public void A_large_scale_hides_per_step_captions()
    {
        var info = new ScaleInfo(1, 100, Array.Empty<string>());
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.StepCount.Should().Be(100);
        cell.ShowReferencePoints.Should().BeFalse();                      // too many to caption
        cell.SliderMax.Should().Be(99);
    }
}
