using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Today;

public class CounterTodayContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private static (CounterTodayContent Sut, FakeCompletionService Completions, FakeMetricDefinitionService Metrics,
                    FakeReferenceData RefData, FakeGoalService Goals, HabitNode Node) NewSut()
    {
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var refData = new FakeReferenceData();
        var goals = new FakeGoalService();
        var clock = new FakeClock(Now);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        return (new CounterTodayContent(completions, metrics, refData, goals, clock), completions, metrics, refData, goals, node);
    }

    [Fact]
    public async Task TodayTotal_and_UnitCode_come_from_the_day_magnitude_and_primary_metric()
    {
        var (sut, completions, metrics, refData, _, node) = NewSut();
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        completions.MagnitudeByHabit[node.Id] = new List<DailyMagnitude> { new(today, 12m) };
        var litre = refData.Units[0];
        metrics.Store[Guid.NewGuid()] = new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id, SortOrder = 0, UnitOfMeasureId = litre.Id, Name = "vol",
        };

        await sut.LoadAsync(node);

        sut.TodayTotal.Should().Be(12m);
        sut.UnitCode.Should().Be(litre.Code);
    }

    [Fact]
    public async Task No_completions_today_reads_zero_total()
    {
        var (sut, _, _, _, _, node) = NewSut();

        await sut.LoadAsync(node);

        sut.TodayTotal.Should().Be(0m);
        sut.HasGoal.Should().BeFalse();
    }

    [Fact]
    public async Task GoalFraction_comes_from_an_active_rolling_goal()
    {
        var (sut, completions, _, _, goals, node) = NewSut();
        var goal = new Goal
        {
            Id = Guid.NewGuid(), HabitNodeId = node.Id, IsClosed = false,
            TargetValue = 10m, PeriodType = GoalPeriodType.Rolling, RollingDays = 30,
            MetricDefinitionId = null, Name = "G",
        };
        goals.Store[goal.Id] = goal;
        // 4 completions inside the 30-day window → count-based goal → 4/10.
        for (var i = 0; i < 4; i++)
        {
            var when = Now.AddDays(-i);
            var id = Guid.NewGuid();
            completions.Store[id] = new Completion
            { Id = id, HabitNodeId = node.Id, CompletedAt = when.UtcDateTime, CompletedAtMs = when.ToUnixTimeMilliseconds() };
        }

        await sut.LoadAsync(node);

        sut.HasGoal.Should().BeTrue();
        sut.GoalFraction.Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public async Task Record_raises_an_open_record_module_action()
    {
        var (sut, _, _, _, _, node) = NewSut();
        await sut.LoadAsync(node);
        TodayContentAction? raised = null;
        sut.ActionRequested += (_, a) => raised = a;

        sut.RecordCommand.Execute(null);

        raised.Should().Be(TodayContentAction.OpenRecordModule);
    }
}
