using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

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
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);
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
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

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
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

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
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

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
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

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

    [Fact]
    public async Task DeletePeriod_removes_a_closed_period_from_history()
    {
        // Arrange: start, lapse (1 closed), start again (1 open). Delete the closed one.
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

        await sut.StartAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(1));
        await sut.LapseAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(1));
        await sut.StartAsync(habitId);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var closed = await db.AbstinencePeriods.AsNoTracking().FirstAsync(p => p.EndedAtMs != null);

        await sut.DeletePeriodAsync(closed.Id);

        var remaining = await db.AbstinencePeriods.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.EndedAtMs.Should().BeNull();
    }

    [Fact]
    public async Task DeletePeriod_of_open_period_reverts_state_to_previous()
    {
        // Arrange: closed period, then an open one. Delete the open -> state falls back to Relapsed.
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

        await sut.StartAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(1));
        await sut.LapseAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromDays(1));
        await sut.StartAsync(habitId);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var open = await db.AbstinencePeriods.AsNoTracking().FirstAsync(p => p.EndedAtMs == null);

        await sut.DeletePeriodAsync(open.Id);

        var state = await sut.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Relapsed);
    }

    [Fact]
    public async Task DeletePeriod_with_unknown_id_is_noop()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedResistanceHabit(fx);
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

        await sut.StartAsync(habitId);

        await sut.DeletePeriodAsync(Guid.NewGuid()); // does not throw

        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.AbstinencePeriods.CountAsync()).Should().Be(1);
    }

    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static async Task SeedPeriodAtAsync(
        DbFixture fx, Guid habitId, DateTimeOffset startedAt, DateTimeOffset? endedAt = null)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            StartedAtMs = startedAt.ToUnixTimeMilliseconds(),
            EndedAtMs = endedAt?.ToUnixTimeMilliseconds(),
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ListPeriods_excludes_a_period_that_starts_on_the_next_local_day()
    {
        // StartedAtMs = 2026-08-18T23:00Z = 19 all'01:00 locali: chiedendo fino al 18 non deve uscire.
        // La finestra UTC arrivava alle 23:59:59.999Z del 18 e lo pescava.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedResistanceHabit(fx);
        await SeedPeriodAtAsync(fx, habitId, new DateTimeOffset(2026, 8, 18, 23, 0, 0, TimeSpan.Zero));
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

        var periods = await sut.ListPeriodsAsync(habitId, new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 18));

        periods.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPeriods_includes_a_period_that_ended_early_on_the_first_local_day()
    {
        // EndedAtMs = 2026-08-17T22:30Z = 18 alle 00:30 locali: chiedendo dal 18 deve uscire.
        // La finestra UTC partiva dalle 00:00Z del 18 e lo scartava.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero), Plus2);
        await using var fx = new DbFixture(clock);
        var habitId = await SeedResistanceHabit(fx);
        await SeedPeriodAtAsync(fx, habitId,
            new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 17, 22, 30, 0, TimeSpan.Zero));
        var sut = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes);

        var periods = await sut.ListPeriodsAsync(habitId, new DateOnly(2026, 8, 18), new DateOnly(2026, 8, 20));

        periods.Should().ContainSingle();
    }

    private static async Task<(DbFixture Fx, Guid HabitId, AbstinenceService Sut)> LapseSetupAsync(DateTimeOffset? startedAt)
    {
        // Fuso +2, reset 00:00: "oggi" = 20 agosto; mezzogiorno del 18 = 10:00Z.
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero), Plus2);
        var fx = new DbFixture(clock);
        var habitId = await SeedResistanceHabit(fx);
        if (startedAt is { } s) await SeedPeriodAtAsync(fx, habitId, s);
        return (fx, habitId, new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus(), fx.ResetTimes));
    }

    private static async Task<long?> OnlyPeriodEndAsync(DbFixture fx, Guid habitId)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        return (await db.AbstinencePeriods.AsNoTracking().SingleAsync(p => p.HabitNodeId == habitId)).EndedAtMs;
    }

    [Fact]
    public async Task Lapse_on_a_past_day_ends_at_noon_of_that_day()
    {
        var (fx, habitId, sut) = await LapseSetupAsync(new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));
        await using var _ = fx;

        await sut.LapseAsync(habitId, new DateOnly(2026, 8, 18));

        (await OnlyPeriodEndAsync(fx, habitId))
            .Should().Be(new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Lapse_on_the_same_day_as_start_ends_at_the_end_of_that_day()
    {
        // Inizio alle 15:00 locali (13:00Z) del 18: mezzogiorno (10:00Z) e' prima dell'inizio.
        var start = new DateTimeOffset(2026, 8, 18, 13, 0, 0, TimeSpan.Zero);
        var (fx, habitId, sut) = await LapseSetupAsync(start);
        await using var _ = fx;

        await sut.LapseAsync(habitId, new DateOnly(2026, 8, 18));

        var end = await OnlyPeriodEndAsync(fx, habitId);
        end.Should().Be(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() - 1);
        end.Should().BeGreaterThan(start.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Lapse_on_a_day_before_the_start_throws_and_writes_nothing()
    {
        var (fx, habitId, sut) = await LapseSetupAsync(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        await using var _ = fx;

        var act = () => sut.LapseAsync(habitId, new DateOnly(2026, 8, 18));

        await act.Should().ThrowAsync<Recordum.Core.Errors.AbstinenceLapseBeforeStartException>();
        (await OnlyPeriodEndAsync(fx, habitId)).Should().BeNull();
    }

    [Fact]
    public async Task Lapse_on_a_future_day_throws()
    {
        var (fx, habitId, sut) = await LapseSetupAsync(new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));
        await using var _ = fx;

        var act = () => sut.LapseAsync(habitId, new DateOnly(2026, 8, 21));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await OnlyPeriodEndAsync(fx, habitId)).Should().BeNull();
    }

    [Fact]
    public async Task Lapse_on_a_past_day_without_open_period_does_nothing()
    {
        var (fx, habitId, sut) = await LapseSetupAsync(null);
        await using var _ = fx;

        await sut.LapseAsync(habitId, new DateOnly(2026, 8, 18));

        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.AbstinencePeriods.CountAsync(p => p.HabitNodeId == habitId)).Should().Be(0);
    }

    [Fact]
    public async Task Lapse_without_day_ends_now()
    {
        var (fx, habitId, sut) = await LapseSetupAsync(new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));
        await using var _ = fx;

        await sut.LapseAsync(habitId);

        (await OnlyPeriodEndAsync(fx, habitId)).Should().Be(fx.Clock.UtcNowMs);
    }
}
