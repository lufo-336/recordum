using FluentAssertions;
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

    [Fact]
    public async Task ListByHabitAsync_returns_only_tags_attached_to_the_habit()
    {
        var (fx, habitId) = await SeedAsync();
        var other = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = other, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h2" });
            await db.SaveChangesAsync();
        }
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var ta = await svc.CreateAsync(new Tag { Name = "alpha" });
        var tb = await svc.CreateAsync(new Tag { Name = "beta" });
        var tc = await svc.CreateAsync(new Tag { Name = "gamma" });
        await svc.AttachAsync(habitId, ta);
        await svc.AttachAsync(habitId, tb);
        await svc.AttachAsync(other, tc);

        var result = await svc.ListByHabitAsync(habitId);

        result.Select(r => r.Name).Should().Equal("alpha", "beta");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task SetTagsAsync_replaces_attachments_idempotently()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var t1 = await svc.CreateAsync(new Tag { Name = "one" });
        var t2 = await svc.CreateAsync(new Tag { Name = "two" });
        await svc.AttachAsync(habitId, t1);

        await svc.SetTagsAsync(habitId, new[] { t2 });

        await using var db = await fx.Factory.CreateDbContextAsync();
        var ids = await db.HabitNodeTags.Where(l => l.HabitNodeId == habitId).Select(l => l.TagId).ToListAsync();
        ids.Should().ContainSingle().Which.Should().Be(t2);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task SetTagsAsync_with_empty_set_clears_all_attachments()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var t1 = await svc.CreateAsync(new Tag { Name = "one" });
        var t2 = await svc.CreateAsync(new Tag { Name = "two" });
        await svc.AttachAsync(habitId, t1);
        await svc.AttachAsync(habitId, t2);

        await svc.SetTagsAsync(habitId, Array.Empty<Guid>());

        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.HabitNodeTags.AnyAsync(l => l.HabitNodeId == habitId)).Should().BeFalse();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_of_an_anchor_tag_nulls_TagId_and_keeps_the_label()
    {
        await using var fx = new DbFixture();
        var svc = new TagService(fx.Factory, fx.Clock, NullLogger<TagService>.Instance);
        var tagId = await svc.CreateAsync(new Tag { Name = "male" });
        Guid anchorId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = "t", Name = "T", MinValue = 1, MaxValue = 3 };
            var anchor = new ScaleAnchor { Id = Guid.NewGuid(), ScaleTemplateId = scale.Id, Value = 1, Label = "Male", TagId = tagId };
            anchorId = anchor.Id;
            db.ScaleTemplates.Add(scale);
            db.ScaleAnchors.Add(anchor);
            await db.SaveChangesAsync();
        }

        await svc.DeleteAsync(tagId);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var a = await db2.ScaleAnchors.AsNoTracking().SingleAsync(x => x.Id == anchorId);
        a.TagId.Should().BeNull();
        a.Label.Should().Be("Male");
    }
}
