using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;

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
            new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero)));

        await vm.LoadAsync(Habit());

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

        var vm = new GoalProgressCardViewModel(goals, completions, clock);
        await vm.LoadAsync(habit);

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

        var vm = new GoalProgressCardViewModel(goals, new FakeCompletionService(), clock);
        await vm.LoadAsync(habit);

        Assert.Equal(9, vm.DaysLeft);
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

        var vm = new GoalProgressCardViewModel(goals, completions, clock);
        await vm.LoadAsync(habit);

        Assert.True(vm.IsDone);
    }
}
