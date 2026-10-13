using FluentAssertions;
using Recordum.Core.Abstractions;
using Recordum.Core.Tests.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Timer;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Timer;

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
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(2)), nodes, new FakeEventBus(), NullTickSourceFactory.Instance);

        await sut.LoadAsync();

        sut.Rows.Should().ContainSingle();
        sut.IsVisible.Should().BeTrue();
    }

    // F2a-11: un timer sparito senza evento (per esempio cancellato con l'abitudine) lasciava una riga
    // morta, e il suo "ferma" lanciava ActiveTimerNotFoundException fino a chiudere l'app.
    [Fact]
    public async Task Stopping_a_row_whose_timer_is_gone_refreshes_the_bar_instead_of_throwing()
    {
        var habitId = Guid.NewGuid();
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), new FakeHabitNodeService(), new FakeEventBus(), NullTickSourceFactory.Instance);
        await sut.LoadAsync();
        ats.Active.Clear();   // sparito alle spalle della barra, nessun evento
        ats.StopFactory = id => throw new Recordum.Core.Errors.ActiveTimerNotFoundException(id);

        var stop = async () => await sut.Rows.Single().StopCommand.ExecuteAsync(null);

        await stop.Should().NotThrowAsync();
        sut.Rows.Should().BeEmpty("la barra si è ricaricata e la riga morta è sparita");
    }

    [Fact]
    public void IsVisible_is_false_when_no_rows()
    {
        var sut = new TimerStatusBarViewModel(new FakeActiveTimerService(), new FakeClock(), new FakeHabitNodeService(), new FakeEventBus(), NullTickSourceFactory.Instance);
        sut.IsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_replaces_rows_atomically()
    {
        var ats = new FakeActiveTimerService();
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), new FakeHabitNodeService(), new FakeEventBus(), NullTickSourceFactory.Instance);
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
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), nodes, new FakeEventBus(), NullTickSourceFactory.Instance);

        await sut.LoadAsync();

        sut.Rows.Single().HabitName.Should().Be("yoga");
    }

    [Fact]
    public async Task LoadAsync_uses_placeholder_when_habit_node_not_found()
    {
        var nodes = new FakeHabitNodeService();
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), nodes, new FakeEventBus(), NullTickSourceFactory.Instance);

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
        var sut = new TimerStatusBarViewModel(ats, clock, nodes, new FakeEventBus(), NullTickSourceFactory.Instance);
        await sut.LoadAsync();

        clock.Advance(TimeSpan.FromSeconds(42));
        sut.Tick();

        sut.Rows.Single().ElapsedSeconds.Should().Be(42);
    }

    /// <summary>The handler is fire-and-forget and may hop threads (SubscribeOnUi): yield until the condition holds.</summary>
    private static async Task Settled(Func<bool> condition)
    {
        for (var i = 0; i < 1000 && !condition(); i++) await Task.Delay(1);
    }

    private static ActiveTimer NewTimer(Guid habitId) => new() { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 0, TimerMode = TimerMode.Stopwatch };

    [Fact]
    public async Task A_timer_started_elsewhere_appears_in_the_bar()
    {
        var bus = new FakeEventBus();
        var ats = new FakeActiveTimerService();
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), new FakeHabitNodeService(), bus, NullTickSourceFactory.Instance);
        await sut.LoadAsync();
        sut.IsVisible.Should().BeFalse();

        var habitId = Guid.NewGuid();
        ats.Active.Add(NewTimer(habitId));
        bus.Publish(new TimerChangedPayload(habitId, WellKnownEvents.TimerStarted));
        await Settled(() => sut.Rows.Count == 1);

        sut.Rows.Should().ContainSingle().Which.HabitNodeId.Should().Be(habitId);
        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task A_stopped_timer_leaves_the_bar()
    {
        var bus = new FakeEventBus();
        var ats = new FakeActiveTimerService();
        var habitId = Guid.NewGuid();
        ats.Active.Add(NewTimer(habitId));
        var sut = new TimerStatusBarViewModel(ats, new FakeClock(), new FakeHabitNodeService(), bus, NullTickSourceFactory.Instance);
        await sut.LoadAsync();

        ats.Active.Clear();
        bus.Publish(new TimerChangedPayload(habitId, WellKnownEvents.TimerStopped));
        await Settled(() => sut.Rows.Count == 0);

        sut.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task The_bar_holds_no_running_tick_when_empty_and_starts_one_with_the_first_row()
    {
        var bus = new FakeEventBus();
        var ats = new FakeActiveTimerService();
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(0));
        var sut = new TimerStatusBarViewModel(ats, clock, new FakeHabitNodeService(), bus, ticks);
        await sut.LoadAsync();
        ticks.RunningCount.Should().Be(0);

        var habitId = Guid.NewGuid();
        ats.Active.Add(NewTimer(habitId));
        bus.Publish(new TimerChangedPayload(habitId, WellKnownEvents.TimerStarted));
        await Settled(() => sut.Rows.Count == 1);
        ticks.RunningCount.Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(7));
        ticks.TickAll();
        sut.Rows.Single().ElapsedSeconds.Should().Be(7);

        ats.Active.Clear();
        bus.Publish(new TimerChangedPayload(habitId, WellKnownEvents.TimerStopped));
        await Settled(() => sut.Rows.Count == 0);
        sut.Rows.Should().BeEmpty();
        ticks.RunningCount.Should().Be(0);
    }
}
