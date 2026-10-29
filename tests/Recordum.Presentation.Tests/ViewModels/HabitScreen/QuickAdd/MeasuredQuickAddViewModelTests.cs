using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.QuickAdd;

public class MeasuredQuickAddViewModelTests
{
    private static (MeasuredQuickAddViewModel Sut, FakeCompletionService Cs, FakeMetricDefinitionService Ms, FakeReferenceData Rd)
        Build(Guid nodeId)
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var sut = new MeasuredQuickAddViewModel(cs, ms, rd, new FakeClock(), new FakeActiveTimerService())
        {
            Node = new HabitNode { Id = nodeId, HabitType = HabitType.Counter },
        };
        return (sut, cs, ms, rd);
    }

    [Fact]
    public async Task Load_builds_a_cell_per_metric()
    {
        var node = Guid.NewGuid();
        var (sut, _, ms, _) = Build(node);
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Weight", Type = MetricType.Decimal, SortOrder = 1 });

        await sut.LoadMetricsAsync();

        sut.Row.Cells.Should().HaveCount(2);
        sut.Row.Cells[0].Name.Should().Be("Reps");
        sut.Row.Cells[1].Name.Should().Be("Weight");
    }

    [Fact]
    public async Task Save_records_a_value_per_filled_cell()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, _) = Build(node);
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Weight", Type = MetricType.Decimal, SortOrder = 1 });
        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].IntegerValue = 12;
        sut.Row.Cells[1].DecimalValue = 40.5m;
        await sut.SaveAsync();

        cs.LastValues.Should().HaveCount(2);
        cs.LastValues!.Single(v => v.ValueInteger == 12).Should().NotBeNull();
        cs.LastValues!.Single(v => v.ValueDecimal == 40.5m).Should().NotBeNull();
    }

    [Fact]
    public async Task Quantity_cell_resolves_unit_label()
    {
        var node = Guid.NewGuid();
        var (sut, _, ms, rd) = Build(node);
        var unitId = rd.Units[0].Id; // "l"/"Litre" seeded
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Water", Type = MetricType.Decimal, UnitOfMeasureId = unitId, SortOrder = 0 });

        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].UnitLabel.Should().Be(rd.Units[0].Code);
    }

    [Fact]
    public async Task Scale_cell_records_selected_index_and_loads_labels()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, rd) = Build(node);
        rd.Scales.Clear();
        var scaleId = Guid.NewGuid();
        rd.Scales.Add(new ScaleTemplate { Id = scaleId, Code = "mood-5", Name = "Mood", MinValue = 1, MaxValue = 5, LabelsJson = "[\"Bad\",\"Meh\",\"Ok\",\"Good\",\"Great\"]" });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Mood", Type = MetricType.Integer, ScaleTemplateId = scaleId, SortOrder = 0 });
        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].Kind.Should().Be(MetricInputKind.Scale);
        sut.Row.Cells[0].ScaleLabels.Should().HaveCount(5);
        sut.Row.Cells[0].SelectedScaleIndex = 3;
        await sut.SaveAsync();

        cs.LastValues.Should().ContainSingle();
        cs.LastValues![0].ValueInteger.Should().Be(4); // 1-based: step index 3 → value 4
    }

    [Fact]
    public async Task Two_scales_both_record()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, rd) = Build(node);
        rd.Scales.Clear();
        var s1 = Guid.NewGuid(); var s2 = Guid.NewGuid();
        rd.Scales.Add(new ScaleTemplate { Id = s1, Code = "a", Name = "A", MinValue = 1, MaxValue = 3, LabelsJson = "[\"x\",\"y\",\"z\"]" });
        rd.Scales.Add(new ScaleTemplate { Id = s2, Code = "b", Name = "B", MinValue = 1, MaxValue = 3, LabelsJson = "[\"x\",\"y\",\"z\"]" });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Energy", Type = MetricType.Integer, ScaleTemplateId = s1, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Focus", Type = MetricType.Integer, ScaleTemplateId = s2, SortOrder = 1 });
        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].SelectedScaleIndex = 1;
        sut.Row.Cells[1].SelectedScaleIndex = 2;
        await sut.SaveAsync();

        cs.LastValues.Should().HaveCount(2);
    }

    [Fact]
    public async Task State_save_with_no_values_sets_error_and_does_not_record()
    {
        // Only State requires a value (it records a current state); bare makes no sense and the
        // service would reject it. Resistance/Counter allow bare completions (spec multivalue §4.1).
        var node = Guid.NewGuid();
        var (sut, cs, ms, _) = Build(node);
        sut.Node = new HabitNode { Id = node, HabitType = HabitType.State };
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Mood", Type = MetricType.Integer, SortOrder = 0 });
        await sut.LoadMetricsAsync();

        await sut.SaveAsync();

        cs.LastRecord.Should().BeNull();
        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Resistance_with_no_metrics_records_a_bare_lapse()
    {
        // "Stop smoking" abstinence has zero metrics: recording a relapse is a single gesture with
        // no value. The value-required guard must not block it (spec: Resistance records bare lapses).
        var node = Guid.NewGuid();
        var (sut, cs, ms, _) = Build(node);
        sut.Node = new HabitNode { Id = node, HabitType = HabitType.Resistance };
        await sut.LoadMetricsAsync(); // no metrics → no cells

        await sut.SaveAsync();

        cs.LastRecord.Should().NotBeNull();
        cs.LastRecord!.HabitNodeId.Should().Be(node);
        sut.ErrorMessage.Should().BeNullOrEmpty();
    }
}

/// <summary>Il comando dello stato in testa (direzione visiva §6.2): con una sola cella scala e al più 10 gradini, un
/// gradino per etichetta e un clic registra quel valore; altrimenti le celle e "Registra".</summary>
public class MeasuredQuickAddStepsTests
{
    private static async Task<(MeasuredQuickAddViewModel Sut, FakeCompletionService Cs)> BuildAsync(int maxValue, bool secondMetric = false)
    {
        var node = Guid.NewGuid();
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = "s", Name = "s", MinValue = 1, MaxValue = maxValue };
        rd.Scales.Add(scale);
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Umore", Type = MetricType.Integer, SortOrder = 0, ScaleTemplateId = scale.Id });
        if (secondMetric)
            await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Note", Type = MetricType.Text, SortOrder = 1 });
        var sut = new MeasuredQuickAddViewModel(cs, ms, rd, new FakeClock(), new FakeActiveTimerService())
        {
            Node = new HabitNode { Id = node, HabitType = HabitType.State },
        };
        await sut.LoadMetricsAsync();
        return (sut, cs);
    }

    [Fact]
    public async Task A_single_scale_of_five_has_five_steps_and_a_click_records_that_value()
    {
        var (sut, cs) = await BuildAsync(5);
        sut.Steps.Should().HaveCount(5);
        sut.HasSteps.Should().BeTrue();

        await sut.RecordStepCommand.ExecuteAsync(sut.Steps[2]);

        cs.LastValues.Should().ContainSingle().Which.ValueInteger.Should().Be(3);
    }

    [Theory]
    [InlineData(10, false, true)]
    [InlineData(11, false, false)]
    [InlineData(5, true, false)]
    public async Task Steps_only_for_one_scale_cell_of_at_most_ten(int max, bool secondMetric, bool expected)
    {
        var (sut, _) = await BuildAsync(max, secondMetric);
        sut.HasSteps.Should().Be(expected);
    }
}
