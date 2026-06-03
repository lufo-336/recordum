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
    public void Scale_cell_builds_zero_based_index_value()
    {
        var info = new ScaleInfo(1, 5, new[] { "Bad", "Meh", "Ok", "Good", "Great" });
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.Kind.Should().Be(MetricInputKind.Scale);
        cell.StepCount.Should().Be(5);
        cell.UseSlider.Should().BeFalse();
        cell.BuildValue(0).Should().BeNull(); // nothing selected yet

        cell.SelectedScaleIndex = 3;
        cell.BuildValue(0)!.ValueInteger.Should().Be(3); // 0-based, preserves State encoding
    }

    [Fact]
    public void Scale_with_more_than_ten_steps_uses_slider()
    {
        var info = new ScaleInfo(1, 100, Array.Empty<string>());
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.StepCount.Should().Be(100);
        cell.UseSlider.Should().BeTrue();
        cell.SliderMax.Should().Be(99);
    }
}
