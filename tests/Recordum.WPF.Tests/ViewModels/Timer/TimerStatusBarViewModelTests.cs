using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Timer;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Timer;

public class TimerStatusBarViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_rows_from_ListActiveAsync()
    {
        var nodes = new FakeHabitNodeService();
        var habitId = Guid.NewGuid();
        nodes.Store[habitId] = new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "yoga" };
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(2)), nodes);

        await sut.LoadAsync();

        sut.Rows.Should().ContainSingle();
        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void IsVisible_is_false_when_no_rows()
    {
        var sut = new TimerStatusBarViewModel(new FakeActiveTimerService(), new FakeClock(), new FakeHabitNodeService());
        sut.IsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_replaces_rows_atomically()
    {
        var ats = new FakeActiveTimerService();
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), new FakeHabitNodeService());
        await sut.LoadAsync();
        sut.Rows.Should().BeEmpty();

        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        await sut.RefreshAsync();

        sut.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_resolves_habit_name_via_HabitNodeService()
    {
        var nodes = new FakeHabitNodeService();
        var habitId = Guid.NewGuid();
        nodes.Store[habitId] = new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "yoga" };
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), nodes);

        await sut.LoadAsync();

        sut.Rows.Single().HabitName.Should().Be("yoga");
    }

    [Fact]
    public async Task LoadAsync_uses_placeholder_when_habit_node_not_found()
    {
        var nodes = new FakeHabitNodeService();
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), nodes);

        await sut.LoadAsync();

        sut.Rows.Single().HabitName.Should().Be("?");
    }

    [Fact]
    public async Task Tick_propagates_to_each_row()
    {
        var nodes = new FakeHabitNodeService();
        var habitId = Guid.NewGuid();
        nodes.Store[habitId] = new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" };
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 0, TimerMode = TimerMode.Stopwatch });
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(0));
        var sut = new TimerStatusBarViewModel(ats, clock, nodes);
        await sut.LoadAsync();

        clock.Advance(TimeSpan.FromSeconds(42));
        sut.Tick();

        sut.Rows.Single().ElapsedSeconds.Should().Be(42);
    }
}
