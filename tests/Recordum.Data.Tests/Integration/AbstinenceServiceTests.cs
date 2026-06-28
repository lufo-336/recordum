using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class AbstinenceServiceTests
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
    public async Task GetState_with_no_periods_is_NotStarted()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock);

        var state = await sut.GetStateAsync(habitId);

        state.Status.Should().Be(AbstinenceStatus.NotStarted);
        state.CurrentStartedAtMs.Should().BeNull();
        state.LastEndedAtMs.Should().BeNull();
    }

    [Fact]
    public async Task Start_when_none_open_opens_a_period()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock);
        var expectedMs = fx.Clock.UtcNowMs;

        await sut.StartAsync(habitId);

        var state = await sut.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Abstaining);
        state.CurrentStartedAtMs.Should().Be(expectedMs);
    }

    [Fact]
    public async Task Start_when_already_open_is_noop()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock);

        await sut.StartAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(1));
        await sut.StartAsync(habitId); // second call — must be no-op

        await using var db = await fx.Factory.CreateDbContextAsync();
        var count = db.AbstinencePeriods.Count(p => p.HabitNodeId == habitId);
        count.Should().Be(1);

        var state = await sut.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Abstaining);
    }

    [Fact]
    public async Task Lapse_when_open_closes_the_period()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock);

        await sut.StartAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(5));
        var lapseMs = fx.Clock.UtcNowMs;

        await sut.LapseAsync(habitId);

        var state = await sut.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Relapsed);
        state.CurrentStartedAtMs.Should().BeNull();
        state.LastEndedAtMs.Should().Be(lapseMs);
    }

    [Fact]
    public async Task Lapse_when_none_open_is_noop()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock);

        await sut.LapseAsync(habitId); // no-op: nothing open

        await using var db = await fx.Factory.CreateDbContextAsync();
        db.AbstinencePeriods.Count(p => p.HabitNodeId == habitId).Should().Be(0);

        var state = await sut.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.NotStarted);
    }

    [Fact]
    public async Task Start_lapse_start_yields_two_periods_second_open()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock);

        await sut.StartAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(3));
        await sut.LapseAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(1));
        await sut.StartAsync(habitId);

        var from = DateOnly.FromDateTime(fx.Clock.UtcNow.AddDays(-30).UtcDateTime);
        var to = DateOnly.FromDateTime(fx.Clock.UtcNow.AddDays(30).UtcDateTime);
        var periods = await sut.ListPeriodsAsync(habitId, from, to);

        periods.Count.Should().Be(2);
        periods.Count(p => p.EndedAtMs == null).Should().Be(1);
        periods.Count(p => p.EndedAtMs != null).Should().Be(1);

        var state = await sut.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Abstaining);
    }
}
