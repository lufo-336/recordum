using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitNodeSoftDeleteTests
{
    private static async Task<(DbFixture fx, HabitNodeService svc, Guid root, Guid mid, Guid leaf)> BuildTreeAsync()
    {
        var fx = new DbFixture();
        var svc = new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        var root = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "root" });
        var mid  = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "mid", ParentId = root });
        var leaf = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "leaf", ParentId = mid });
        return (fx, svc, root, mid, leaf);
    }

    [Fact]
    public async Task SoftDelete_cascades_with_shared_timestamp()
    {
        var (fx, svc, root, _, _) = await BuildTreeAsync();
        await svc.SoftDeleteAsync(root);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var all = await db.HabitNodes.IgnoreQueryFilters().ToListAsync();
        Assert.All(all, n => Assert.NotNull(n.DeletedAt));
        Assert.Single(all.Select(n => n.DeletedAt).Distinct());
        Assert.Empty(await db.HabitNodes.ToListAsync());
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task SoftDelete_is_idempotent()
    {
        var (fx, svc, root, _, _) = await BuildTreeAsync();
        await svc.SoftDeleteAsync(root);
        await svc.SoftDeleteAsync(root);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var deleted = await db.HabitNodes.IgnoreQueryFilters().Where(n => n.Id == root).SingleAsync();
        Assert.NotNull(deleted.DeletedAt);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Restore_only_revives_subtree_deleted_in_same_operation()
    {
        var (fx, svc, root, mid, leaf) = await BuildTreeAsync();
        ((FakeClock)fx.Clock).Set(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await svc.SoftDeleteAsync(leaf);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromMinutes(5));
        await svc.SoftDeleteAsync(root);
        await svc.RestoreAsync(root);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var rows = await db.HabitNodes.IgnoreQueryFilters().ToListAsync();
        Assert.Null(rows.Single(n => n.Id == root).DeletedAt);
        Assert.Null(rows.Single(n => n.Id == mid).DeletedAt);
        Assert.NotNull(rows.Single(n => n.Id == leaf).DeletedAt);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Restore_orphan_throws()
    {
        var (fx, svc, root, mid, _) = await BuildTreeAsync();
        await svc.SoftDeleteAsync(root);
        var ex = await Assert.ThrowsAsync<CannotRestoreOrphanException>(() => svc.RestoreAsync(mid).AsTask());
        Assert.Equal(mid, ex.Id);
        Assert.Equal(root, ex.ParentId);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Restore_no_op_when_not_soft_deleted()
    {
        var (fx, svc, root, _, _) = await BuildTreeAsync();
        await svc.RestoreAsync(root);
        await fx.DisposeAsync();
    }
}
