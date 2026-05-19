using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class TagServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedAsync()
    {
        var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" });
        await db.SaveChangesAsync();
        return (fx, habitId);
    }

    [Fact]
    public async Task AttachAsync_is_idempotent()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var tagId = await svc.CreateAsync(new Tag { Name = "t" });
        await svc.AttachAsync(habitId, tagId);
        await svc.AttachAsync(habitId, tagId);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Single(db.HabitNodeTags);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_removes_join_row()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var tagId = await svc.CreateAsync(new Tag { Name = "t" });
        await svc.AttachAsync(habitId, tagId);
        await svc.DetachAsync(habitId, tagId);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Empty(db.HabitNodeTags);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Delete_removes_tag_and_join_rows()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var tagId = await svc.CreateAsync(new Tag { Name = "t" });
        await svc.AttachAsync(habitId, tagId);
        await svc.DeleteAsync(tagId);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Empty(db.Tags);
        Assert.Empty(db.HabitNodeTags);
        await fx.DisposeAsync();
    }
}
