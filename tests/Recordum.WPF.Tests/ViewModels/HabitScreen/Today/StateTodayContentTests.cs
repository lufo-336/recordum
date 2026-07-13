using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Today;

public class StateTodayContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private static (StateTodayContent Sut, FakeCompletionService Completions, FakeMetricDefinitionService Metrics,
                    FakeReferenceData RefData, HabitNode Node) NewSut()
    {
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var refData = new FakeReferenceData();
        var clock = new FakeClock(Now);
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" };
        return (new StateTodayContent(completions, metrics, refData, clock), completions, metrics, refData, node);
    }

    private static MetricDefinition ScaleMetric(Guid habitId, Guid scaleId, string? labelsJson)
        => new() { Id = Guid.NewGuid(), HabitNodeId = habitId, SortOrder = 0, ScaleTemplateId = scaleId, LabelsJson = labelsJson, Name = "mood" };

    [Fact]
    public async Task Slider_bounds_come_from_the_metric_scale()
    {
        var (sut, _, metrics, refData, node) = NewSut();
        var scaleId = refData.Scales[0].Id;
        metrics.Store[Guid.NewGuid()] = ScaleMetric(node.Id, scaleId, JsonSerializer.Serialize(new[] { "Bad", "OK", "Good" }));

        await sut.LoadAsync(node);

        sut.Min.Should().Be(0);
        sut.Max.Should().Be(2);           // 3 labels → indices 0..2
        sut.CanRecord.Should().BeTrue();
    }

    [Fact]
    public async Task CurrentLabel_uses_the_metric_override_over_the_template()
    {
        var (sut, _, metrics, refData, node) = NewSut();
        var scale = refData.Scales[0];
        scale.LabelsJson = JsonSerializer.Serialize(new[] { "X", "Y", "Z" });        // template labels
        metrics.Store[Guid.NewGuid()] = ScaleMetric(node.Id, scale.Id, JsonSerializer.Serialize(new[] { "A", "B", "C" })); // override

        await sut.LoadAsync(node);

        sut.SliderValue = 0;
        sut.CurrentLabel.Should().Be("A");   // metric override wins over template
    }

    [Fact]
    public async Task Record_replaces_todays_value_and_stores_the_1_based_index()
    {
        var (sut, completions, metrics, refData, node) = NewSut();
        var scaleId = refData.Scales[0].Id;
        var metric = ScaleMetric(node.Id, scaleId, JsonSerializer.Serialize(new[] { "Bad", "OK", "Good" }));
        metrics.Store[metric.Id] = metric;
        // A pre-existing completion today must be dropped (last-wins).
        var priorId = Guid.NewGuid();
        completions.Store[priorId] = new Completion
        { Id = priorId, HabitNodeId = node.Id, CompletedAt = Now.UtcDateTime, CompletedAtMs = Now.ToUnixTimeMilliseconds() };

        await sut.LoadAsync(node);
        sut.SliderValue = 2;
        await sut.RecordCommand.ExecuteAsync(null);

        completions.DeleteCalls.Should().Contain(priorId);
        completions.LastValues.Should().ContainSingle(v => v.MetricDefinitionId == metric.Id && v.ValueInteger == 3);
    }

    [Fact]
    public async Task Missing_scale_degrades_to_a_numeric_slider_without_crashing()
    {
        var (sut, _, _, _, node) = NewSut();   // no metric seeded

        var act = async () => await sut.LoadAsync(node);

        await act.Should().NotThrowAsync();
        sut.Max.Should().BeGreaterThan(0);     // fallback numeric range
        sut.CanRecord.Should().BeFalse();      // nothing to record into
    }
}
