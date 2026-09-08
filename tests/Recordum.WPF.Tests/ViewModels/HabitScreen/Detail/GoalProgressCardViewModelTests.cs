using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class GoalProgressCardViewModelTests
{
    private static HabitNode Habit(HabitType t = HabitType.Counter) => new()
    {
        Id = Guid.NewGuid(),
        Name = "h",
        NodeType = NodeType.Habit,
        HabitType = t,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static void SeedActiveGoal(FakeGoalService goals, Guid habitId, Goal g)
    {
        g.Id = Guid.NewGuid();
        g.HabitNodeId = habitId;
        g.IsClosed = false;
        goals.Store[g.Id] = g;
    }

    private static void SeedCompletion(FakeCompletionService completions, Guid habitId, DateTimeOffset when)
    {
        var id = Guid.NewGuid();
        completions.Store[id] = new Completion
        {
            Id = id,
            HabitNodeId = habitId,
            CompletedAt = when.UtcDateTime,
            CompletedAtMs = when.ToUnixTimeMilliseconds(),
            IsRetroactive = false,
        };
    }

    [Fact]
    public async Task No_goal_sets_IsEmpty_true_with_localized_message()
    {
        var vm = new GoalProgressCardViewModel(
            new FakeGoalService(),
            new FakeCompletionService(),
            new FakeMetricDefinitionService(),
            new FakeReferenceData(),
            new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero)));

        await vm.LoadAsync(Habit(), DayBoundary.Utc);

        Assert.True(vm.IsEmpty);
        Assert.False(string.IsNullOrEmpty(vm.EmptyMessage));
    }

    [Fact]
    public async Task Rolling_30_uses_count_when_metric_null()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var habit = Habit(HabitType.Action);
        var goals = new FakeGoalService();
        SeedActiveGoal(goals, habit.Id, new Goal
        {
            TargetValue = 10m,
            PeriodType = GoalPeriodType.Rolling,
            RollingDays = 30,
            MetricDefinitionId = null,
            Name = "G",
        });
        var completions = new FakeCompletionService();
        for (int i = 0; i < 4; i++)
        {
            SeedCompletion(completions, habit.Id, clock.UtcNow.AddDays(-i));
        }

        var vm = new GoalProgressCardViewModel(goals, completions, new FakeMetricDefinitionService(), new FakeReferenceData(), clock);
        await vm.LoadAsync(habit, DayBoundary.Utc);

        Assert.False(vm.IsEmpty);
        Assert.Equal(10m, vm.Target);
        Assert.Equal(4m, vm.Actual);
        Assert.InRange(vm.PercentComplete, 0.39, 0.41);
        Assert.Null(vm.DaysLeft);
    }

    [Fact]
    public async Task Fixed_period_computes_days_left_when_PeriodEnd_in_future()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var habit = Habit();
        var goals = new FakeGoalService();
        SeedActiveGoal(goals, habit.Id, new Goal
        {
            TargetValue = 30m,
            PeriodType = GoalPeriodType.Fixed,
            PeriodStart = new DateOnly(2026, 5, 1),
            PeriodEnd = new DateOnly(2026, 5, 31),
            Name = "G",
        });

        var vm = new GoalProgressCardViewModel(goals, new FakeCompletionService(), new FakeMetricDefinitionService(), new FakeReferenceData(), clock);
        await vm.LoadAsync(habit, DayBoundary.Utc);

        Assert.Equal(9, vm.DaysLeft);
    }

    /// <summary>Seeds a duration (Seconds) metric on the habit plus a goal bound to it, and two
    /// completions carrying <paramref name="secondsA"/> / <paramref name="secondsB"/>.</summary>
    private static (FakeGoalService goals, FakeCompletionService completions, FakeMetricDefinitionService metrics)
        SeedDurationGoal(HabitNode habit, DateTimeOffset when, decimal target, Guid? unitId, int secondsA, int secondsB)
    {
        var metrics = new FakeMetricDefinitionService();
        var metric = new MetricDefinition
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habit.Id,
            Name = "Tempo",
            Type = MetricType.Seconds,
            UnitOfMeasureId = unitId,
        };
        metrics.Store[metric.Id] = metric;

        var goals = new FakeGoalService();
        SeedActiveGoal(goals, habit.Id, new Goal
        {
            TargetValue = target,
            PeriodType = GoalPeriodType.Open,
            MetricDefinitionId = metric.Id,
            Name = "G",
        });

        var completions = new FakeCompletionService();
        foreach (var secs in new[] { secondsA, secondsB })
        {
            var id = Guid.NewGuid();
            completions.Store[id] = new Completion
            {
                Id = id,
                HabitNodeId = habit.Id,
                CompletedAt = when.UtcDateTime,
                CompletedAtMs = when.ToUnixTimeMilliseconds(),
                Values = [new CompletionValue { MetricDefinitionId = metric.Id, ValueSeconds = secs }],
            };
        }
        return (goals, completions, metrics);
    }

    [Fact]
    public async Task Duration_goal_actual_is_expressed_in_the_metric_unit()
    {
        // 90s + 30s = 120s on a "minutes" metric is 2 minutes against a 4-minute target — not 120.
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var habit = Habit();
        var refData = new FakeReferenceData();
        var minutes = refData.Units.Single(u => u.Code == "minutes").Id;
        var (goals, completions, metrics) = SeedDurationGoal(habit, clock.UtcNow, target: 4m, minutes, 90, 30);

        var vm = new GoalProgressCardViewModel(goals, completions, metrics, refData, clock);
        await vm.LoadAsync(habit, DayBoundary.Utc);

        Assert.Equal(2m, vm.Actual);
        Assert.Equal(0.5, vm.PercentComplete, 3);
        Assert.False(vm.IsDone);
    }

    [Fact]
    public async Task Duration_goal_without_a_time_unit_keeps_the_raw_second_count()
    {
        // A "+ Tempo" field ships with no unit: the target was typed in seconds, so the actual stays seconds.
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var habit = Habit();
        var (goals, completions, metrics) = SeedDurationGoal(habit, clock.UtcNow, target: 300m, unitId: null, 90, 30);

        var vm = new GoalProgressCardViewModel(goals, completions, metrics, new FakeReferenceData(), clock);
        await vm.LoadAsync(habit, DayBoundary.Utc);

        Assert.Equal(120m, vm.Actual);
    }

    [Fact]
    public async Task Non_duration_goal_is_not_converted_even_on_a_time_unit()
    {
        // A Decimal metric already stores minutes; converting it would divide a correct total by 60.
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var habit = Habit();
        var refData = new FakeReferenceData();
        var metrics = new FakeMetricDefinitionService();
        var metric = new MetricDefinition
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habit.Id,
            Name = "Minuti",
            Type = MetricType.Decimal,
            UnitOfMeasureId = refData.Units.Single(u => u.Code == "minutes").Id,
        };
        metrics.Store[metric.Id] = metric;
        var goals = new FakeGoalService();
        SeedActiveGoal(goals, habit.Id, new Goal
        {
            TargetValue = 200m,
            PeriodType = GoalPeriodType.Open,
            MetricDefinitionId = metric.Id,
            Name = "G",
        });
        var completions = new FakeCompletionService();
        var id = Guid.NewGuid();
        completions.Store[id] = new Completion
        {
            Id = id,
            HabitNodeId = habit.Id,
            CompletedAt = clock.UtcNow.UtcDateTime,
            CompletedAtMs = clock.UtcNow.ToUnixTimeMilliseconds(),
            Values = [new CompletionValue { MetricDefinitionId = metric.Id, ValueDecimal = 120m }],
        };

        var vm = new GoalProgressCardViewModel(goals, completions, metrics, refData, clock);
        await vm.LoadAsync(habit, DayBoundary.Utc);

        Assert.Equal(120m, vm.Actual);
    }

    [Fact]
    public async Task IsDone_true_when_actual_meets_or_exceeds_target()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var habit = Habit(HabitType.Action);
        var goals = new FakeGoalService();
        SeedActiveGoal(goals, habit.Id, new Goal { TargetValue = 1m, PeriodType = GoalPeriodType.Open, Name = "G" });
        var completions = new FakeCompletionService();
        SeedCompletion(completions, habit.Id, clock.UtcNow);

        var vm = new GoalProgressCardViewModel(goals, completions, new FakeMetricDefinitionService(), new FakeReferenceData(), clock);
        await vm.LoadAsync(habit, DayBoundary.Utc);

        Assert.True(vm.IsDone);
    }
}
