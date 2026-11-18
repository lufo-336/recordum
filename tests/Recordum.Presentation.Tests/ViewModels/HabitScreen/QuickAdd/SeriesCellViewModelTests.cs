using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.QuickAdd;

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

    private static ScaleInfo Info(int min, int max, params string[] names)
    {
        var t = new ScaleTemplate { Id = Guid.NewGuid(), Code = "s", Name = "s", MinValue = min, MaxValue = max, Precision = ScalePrecision.Integer };
        t.Anchors = names.Select((n, i) => new ScaleAnchor { Id = Guid.NewGuid(), Value = min + i, Label = n }).ToList();
        return new ScaleInfo(t);
    }

    [Fact]
    public void Scale_cell_builds_the_true_value()
    {
        var cell = new SeriesCellViewModel(Scale(), scale: Info(1, 5, "Bad", "Meh", "Ok", "Good", "Great"));

        cell.Kind.Should().Be(MetricInputKind.Scale);
        cell.StepCount.Should().Be(5);
        cell.ShowReferencePoints.Should().BeTrue();                       // small scale → tick per value
        cell.Points.Select(p => p.Label).Should().Equal("Bad", "Meh", "Ok", "Good", "Great");
        cell.SliderValue.Should().Be(1);                                  // rests on the first value by default
        cell.BuildValue(0)!.ValueDecimal.Should().Be(1m);

        cell.SliderValue = 4;
        cell.BuildValue(0)!.ValueDecimal.Should().Be(4m);                  // the value, not a position
        cell.BuildValue(0)!.ValueInteger.Should().BeNull();
    }

    [Fact]
    public void A_scale_starting_at_zero_records_zero_at_the_first_stop()
    {
        var cell = new SeriesCellViewModel(Scale(), scale: Info(0, 10));
        cell.BuildValue(0)!.ValueDecimal.Should().Be(0m);
    }

    [Fact]
    public void Labelless_scale_uses_its_bare_numbers_as_reference_points()
    {
        var cell = new SeriesCellViewModel(Scale(), scale: Info(1, 10));

        cell.StepCount.Should().Be(10);
        cell.ShowReferencePoints.Should().BeTrue();
        cell.Points.Select(p => p.Label).Should().Equal("1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
    }

    [Fact]
    public void A_large_scale_hides_the_reference_points()
    {
        var cell = new SeriesCellViewModel(Scale(), scale: Info(1, 100));

        cell.StepCount.Should().Be(100);
        cell.ShowReferencePoints.Should().BeFalse();                      // too many values for a tick each
        cell.Max.Should().Be(100);
    }

    [Fact]
    public void Duration_cell_normalizes_hours_to_seconds_on_build()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric, unitCode: "hours");
        cell.DurationUnit.Should().Be(TimeUnit.Hours);       // default follows the metric unit
        cell.DurationValue = 7.5m;

        var v = cell.BuildValue(0);

        v!.ValueSeconds.Should().Be(27000);                   // 7.5 h × 3600
    }

    [Fact]
    public void Duration_cell_defaults_to_minutes_when_the_metric_has_no_time_unit()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric);
        cell.DurationUnit.Should().Be(TimeUnit.Minutes);
        cell.DurationValue = 90m;

        cell.BuildValue(0)!.ValueSeconds.Should().Be(5400);
    }

    [Fact]
    public void Chrono_pour_sets_seconds_unit_and_value()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric, unitCode: "hours");

        cell.SetDurationSeconds(125);

        cell.DurationUnit.Should().Be(TimeUnit.Seconds);
        cell.DurationValue.Should().Be(125m);
        cell.BuildValue(0)!.ValueSeconds.Should().Be(125);
    }

    [Fact]
    public void Empty_duration_builds_no_value()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric);
        cell.BuildValue(0).Should().BeNull();
    }

    [Fact]
    public void Out_of_range_duration_builds_no_value()
    {
        // Regression: the decimal→int narrowing in BuildValue always throws OverflowException when
        // out of range, and BuildValue runs live on every keystroke (row IsValid re-evaluates on
        // PropertyChanged) — so typing a huge number must yield "no value", never crash.
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric, unitCode: "hours");
        cell.DurationValue = 999_999_999m;

        cell.BuildValue(0).Should().BeNull();
    }

    [Fact]
    public void Duration_cell_hides_the_trailing_unit_label_to_avoid_a_double_unit()
    {
        // A duration cell shows its unit in the h/min/sec selector; the trailing UnitLabel would
        // repeat it ("9 sec … minutes"), so ShowUnitLabel is false for durations.
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric, unitCode: "minutes");
        cell.IsDuration.Should().BeTrue();
        cell.ShowUnitLabel.Should().BeFalse();
    }

    [Fact]
    public void Non_duration_measured_cell_shows_its_unit_label()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "vol", Type = MetricType.Decimal };
        var cell = new SeriesCellViewModel(metric, unitCode: "l");
        cell.IsDuration.Should().BeFalse();
        cell.ShowUnitLabel.Should().BeTrue();
    }

    [Fact]
    public void Measured_cell_without_a_unit_shows_no_label()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "reps", Type = MetricType.Integer };
        var cell = new SeriesCellViewModel(metric);
        cell.ShowUnitLabel.Should().BeFalse();
    }

    [Fact]
    public void Extreme_duration_that_overflows_the_multiply_builds_no_value()
    {
        // Regression (F2): the value×factor multiply can overflow System.Decimal itself for extreme
        // input, throwing before the int-range check — BuildValue must still yield "no value", not
        // crash the live-validation path.
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric, unitCode: "hours");
        cell.DurationValue = decimal.MaxValue;

        cell.BuildValue(0).Should().BeNull();
    }

    [Fact]
    public void Negative_duration_builds_no_value()
    {
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
        var cell = new SeriesCellViewModel(metric);
        cell.DurationValue = -5m;

        cell.BuildValue(0).Should().BeNull();
    }

    [Fact]
    public void A_cell_shows_the_unit_symbol_not_its_code()
    {
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "kilometres", Name = "Kilometres", Symbol = "km" };
        var metric = new MetricDefinition
        { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), Name = "Distanza", Type = MetricType.Decimal, UnitOfMeasureId = unit.Id };

        var cell = new MetricCellFactory([unit], []).Create(metric);

        cell.UnitLabel.Should().Be("km");
    }
}
