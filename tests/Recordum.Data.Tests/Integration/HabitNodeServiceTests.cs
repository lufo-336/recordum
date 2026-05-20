using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitNodeServiceTests
{
    private static HabitNodeService Svc(DbFixture fx) =>
        new(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);

    [Fact]
    public async Task Create_returns_id_and_persists_with_user_provenance()
    {
        await using var fx = new DbFixture();
        var id = await Svc(fx).CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "Root" });
        await using var db = await fx.Factory.CreateDbContextAsync();
        var saved = await db.HabitNodes.SingleAsync();
        Assert.Equal(id, saved.Id);
        Assert.Null(saved.CreatedByPluginId);
    }

    [Fact]
    public async Task Create_inside_plugin_scope_records_provenance()
    {
        await using var fx = new DbFixture();
        using (fx.PluginContext.BeginScope("com.example.foo"))
            await Svc(fx).CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "g" });
        await using var db = await fx.Factory.CreateDbContextAsync();
        var saved = await db.HabitNodes.SingleAsync();
        Assert.Equal("com.example.foo", saved.CreatedByPluginId);
    }

    [Fact]
    public async Task Update_throws_when_node_missing()
    {
        await using var fx = new DbFixture();
        await Assert.ThrowsAsync<HabitNodeNotFoundException>(() =>
            Svc(fx).UpdateAsync(Guid.NewGuid(), n => n.Name = "x").AsTask());
    }

    [Fact]
    public async Task Move_to_self_descendant_throws_CyclicHabitNodeMoveException()
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var root = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "root" });
        var child = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "child", ParentId = root });
        await Assert.ThrowsAsync<CyclicHabitNodeMoveException>(() =>
            svc.MoveAsync(root, child).AsTask());
    }

    [Fact]
    public async Task Move_to_unrelated_node_works()
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var a = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "a" });
        var b = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "b" });
        var leaf = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "leaf", ParentId = a });
        await svc.MoveAsync(leaf, b);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(b, (await db.HabitNodes.FindAsync(leaf))!.ParentId);
    }

    [Theory]
    [InlineData(PinScope.None)]
    [InlineData(PinScope.Local)]
    [InlineData(PinScope.Global)]
    public async Task SetPinScopeAsync_persists(PinScope target)
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var id = await svc.CreateAsync(new HabitNode { Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action });

        await svc.SetPinScopeAsync(id, target);

        var node = await svc.GetAsync(id);
        node!.PinScope.Should().Be(target);
    }

    [Fact]
    public async Task SetPinScopeAsync_throws_on_unknown_id()
    {
        await using var fx = new DbFixture();
        var act = async () => await Svc(fx).SetPinScopeAsync(Guid.NewGuid(), PinScope.Local);
        await act.Should().ThrowAsync<HabitNodeNotFoundException>();
    }

    [Fact]
    public async Task SetPinScopeAsync_throws_on_soft_deleted_node()
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var id = await svc.CreateAsync(new HabitNode { Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action });
        await svc.SoftDeleteAsync(id);

        var act = async () => await svc.SetPinScopeAsync(id, PinScope.Local);
        await act.Should().ThrowAsync<HabitNodeAlreadySoftDeletedException>();
    }
}
