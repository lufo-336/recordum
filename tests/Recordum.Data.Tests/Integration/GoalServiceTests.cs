using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class GoalServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    [Fact]
    public async Task Create_then_list_returns_goal()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        var list = await svc.ListByHabitAsync(hid);
        Assert.Single(list);
        Assert.Equal(id, list[0].Id);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Close_sets_IsClosed_and_ClosedAt()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        await svc.CloseAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var g = await db.Goals.FindAsync(id);
        Assert.True(g!.IsClosed);
        Assert.NotNull(g.ClosedAt);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Delete_removes_goal()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        await svc.DeleteAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.False(await db.Goals.AnyAsync(x => x.Id == id));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveAsync_returns_null_when_no_open_goal()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        (await svc.GetActiveAsync(hid)).Should().BeNull();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveAsync_ignores_closed_goals()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        await svc.CloseAsync(id);
        (await svc.GetActiveAsync(hid)).Should().BeNull();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpsertActiveAsync_creates_when_none_active()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        await svc.UpsertActiveAsync(hid, g => { g.Name = "drink water"; g.TargetValue = 2; g.PeriodType = GoalPeriodType.Open; });
        var active = await svc.GetActiveAsync(hid);
        active.Should().NotBeNull();
        active!.TargetValue.Should().Be(2);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpsertActiveAsync_mutates_existing_active_goal()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var firstId = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 5, PeriodType = GoalPeriodType.Open });
        await svc.UpsertActiveAsync(hid, g => g.TargetValue = 99);
        var active = await svc.GetActiveAsync(hid);
        active!.Id.Should().Be(firstId);
        active.TargetValue.Should().Be(99);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpsertActiveAsync_publishes_HabitUpdated_after_the_write_and_nothing_when_it_fails()
    {
        var (fx, hid) = await SeedAsync();
        var bus = new Recordum.Data.Tests.Fixtures.RecordingEventBus();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance, bus);
        await svc.UpsertActiveAsync(hid, g => { g.Name = "g"; g.TargetValue = 5; g.PeriodType = GoalPeriodType.Open; });
        bus.Published.OfType<Recordum.Plugins.Contracts.Events.HabitUpdatedPayload>().Should().ContainSingle().Which.HabitNodeId.Should().Be(hid);

        bus.Published.Clear();
        var act = async () => await svc.UpsertActiveAsync(hid, _ => throw new InvalidOperationException("rejected"));
        await act.Should().ThrowAsync<InvalidOperationException>();
        bus.Published.Should().BeEmpty();
        await fx.DisposeAsync();
    }
}
