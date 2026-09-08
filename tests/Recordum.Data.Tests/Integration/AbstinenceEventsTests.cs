using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

/// <summary>W2V-6 — abstinence (U9) is the one kind of habit whose data changes without producing a
/// completion, and it published nothing at all: no surface could learn that a period had opened or
/// closed. On screen that showed as a Home row still counting seconds on a period that had already
/// ended — a live wrong number, not a stale one, because the row re-reads the clock every second.
/// The periods are the source of truth, so mutating them has to say so.</summary>
public class AbstinenceEventsTests
{
    private static async Task<Guid> SeedResistanceHabit(DbFixture fx)
    {
        var id = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "abs" });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Starting_a_period_announces_it()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var bus = new RecordingEventBus();
        var sut = new AbstinenceService(fx.Factory, fx.Clock, bus, fx.ResetTimes);

        await sut.StartAsync(habitId);

        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<AbstinenceChangedPayload>()
            .Which.Should().Match<AbstinenceChangedPayload>(p =>
                p.HabitNodeId == habitId && p.Name == WellKnownEvents.AbstinenceStarted);
    }

    [Fact]
    public async Task Relapsing_announces_it()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var bus = new RecordingEventBus();
        var sut = new AbstinenceService(fx.Factory, fx.Clock, bus, fx.ResetTimes);
        await sut.StartAsync(habitId);
        bus.Published.Clear();

        await sut.LapseAsync(habitId);

        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<AbstinenceChangedPayload>()
            .Which.Name.Should().Be(WellKnownEvents.AbstinenceLapsed);
    }

    [Fact]
    public async Task Deleting_a_period_announces_it_with_the_habit_it_belonged_to()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var bus = new RecordingEventBus();
        var sut = new AbstinenceService(fx.Factory, fx.Clock, bus, fx.ResetTimes);
        await sut.StartAsync(habitId);
        // DateOnly.MaxValue as an inclusive upper bound has no representable "day after" for the
        // half-open RangeOf window R9 introduced; back off one day, which is still an effectively
        // unbounded future for "every period this habit has ever had".
        var period = (await sut.ListPeriodsAsync(habitId, DateOnly.MinValue, DateOnly.MaxValue.AddDays(-1)))[0];
        bus.Published.Clear();

        await sut.DeletePeriodAsync(period.Id);

        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<AbstinenceChangedPayload>()
            .Which.Should().Match<AbstinenceChangedPayload>(p =>
                p.HabitNodeId == habitId && p.Name == WellKnownEvents.AbstinencePeriodDeleted);
    }

    /// <summary>The three guards (a second Start, a Lapse with nothing open, an unknown period id)
    /// return without touching the database. Announcing a change that did not happen would make every
    /// listener reload for nothing — and would hide the guard from anyone reading the event log.</summary>
    [Fact]
    public async Task A_call_that_changes_nothing_announces_nothing()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var bus = new RecordingEventBus();
        var sut = new AbstinenceService(fx.Factory, fx.Clock, bus, fx.ResetTimes);

        await sut.LapseAsync(habitId);            // nothing open
        await sut.DeletePeriodAsync(Guid.NewGuid()); // unknown id
        await sut.StartAsync(habitId);
        bus.Published.Clear();
        await sut.StartAsync(habitId);            // already open

        bus.Published.Should().BeEmpty();
    }
}
