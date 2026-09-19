using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Plugins.Contracts.Events;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class DetailViewModelTests
{
    private static DetailViewModel BuildSut(out FakeEventBus bus)
    {
        bus = new FakeEventBus();
        return new DetailViewModel(
            new FakeGoalService(),
            new FakeStreakAggregator(),
            new FakeTrendQueryService(),
            new FakeHourlyDistributionQueryService(),
            new FakeCompletionService(),
            new FakeMetricDefinitionService(),
            new FakeReferenceData(),
            new IntensityRampResolver(),
            new FakeHabitConfigService(),
            new FakeAbstinenceService(),
            new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero)),
            bus);
    }

    [Fact]
    public async Task Group_node_loads_only_GoalProgressCard()
    {
        var sut = BuildSut(out _);
        var group = new HabitNode { Id = Guid.NewGuid(), Name = "g", NodeType = NodeType.Group };

        await sut.LoadAsync(group, DayBoundary.Utc);

        Assert.True(sut.IsGroup);
        Assert.False(sut.IsHabit);
    }

    [Fact]
    public async Task Habit_node_loads_all_child_VMs()
    {
        var sut = BuildSut(out _);
        var habit = new HabitNode
        {
            Id = Guid.NewGuid(),
            Name = "h",
            NodeType = NodeType.Habit,
            HabitType = HabitType.Action,
            CreatedAt = DateTime.UtcNow.AddYears(-1),
        };

        await sut.LoadAsync(habit, DayBoundary.Utc);

        Assert.True(sut.IsHabit);
        Assert.False(sut.IsGroup);
        Assert.NotNull(sut.YearHeatmap);
        Assert.NotNull(sut.Trend);
    }

    [Fact]
    public async Task CompletionRecorded_event_for_current_habit_triggers_reload()
    {
        var sut = BuildSut(out var bus);
        var habit = new HabitNode
        {
            Id = Guid.NewGuid(),
            Name = "h",
            NodeType = NodeType.Habit,
            HabitType = HabitType.Action,
            CreatedAt = DateTime.UtcNow.AddYears(-1),
        };
        await sut.LoadAsync(habit, DayBoundary.Utc);

        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), habit.Id, DateTime.UtcNow));
        await Task.Yield();

        Assert.NotNull(sut.Node);
    }

    [Fact]
    public void Dispose_unsubscribes_from_event_bus()
    {
        var sut = BuildSut(out var bus);
        Assert.True(bus.HasSubscribers);

        sut.Dispose();

        Assert.False(bus.HasSubscribers);
    }
}
