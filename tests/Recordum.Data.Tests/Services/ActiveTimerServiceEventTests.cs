using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class ActiveTimerServiceEventTests
{
    /// <summary>Records each timer event with what a fresh reader sees at the moment of publication.</summary>
    private sealed class SnapshotBus(Func<(int Rows, long? PausedAtMs)> read) : IEventBus
    {
        public List<(EventName Name, Guid HabitId, int Rows, long? PausedAtMs)> Seen { get; } = [];

        public void Publish(IEventPayload payload)
        {
            if (payload is not TimerChangedPayload p) return;
            var (rows, paused) = read();
            Seen.Add((p.Name, p.HabitNodeId, rows, paused));
        }

        public IDisposable Subscribe(EventName name, Action<EventEnvelope> handler) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Start_pause_resume_stop_each_publish_their_event_after_the_save()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var seed = await fx.Factory.CreateDbContextAsync())
        {
            seed.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" });
            await seed.SaveChangesAsync();
        }

        (int, long?) Read()
        {
            using var db = fx.Factory.CreateDbContext();
            var rows = db.ActiveTimers.AsNoTracking().ToList();
            return (rows.Count, rows.SingleOrDefault()?.PausedAtMs);
        }

        var bus = new SnapshotBus(Read);
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, completions, bus);

        await sut.StartAsync(habitId, TimerMode.Stopwatch);
        await sut.PauseAsync(habitId);
        await sut.ResumeAsync(habitId);
        await sut.StopAsync(habitId);

        bus.Seen.Select(s => s.Name).Should().Equal(
            WellKnownEvents.TimerStarted, WellKnownEvents.TimerPaused,
            WellKnownEvents.TimerResumed, WellKnownEvents.TimerStopped);
        bus.Seen.Should().OnlyContain(s => s.HabitId == habitId);
        // What a reader sees at publication time is the new state: the event came after the commit.
        bus.Seen[0].Rows.Should().Be(1);
        bus.Seen[1].PausedAtMs.Should().NotBeNull();
        bus.Seen[2].PausedAtMs.Should().BeNull();
        bus.Seen[3].Rows.Should().Be(0);
    }
}
