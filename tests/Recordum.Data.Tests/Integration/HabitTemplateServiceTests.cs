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

public class HabitTemplateServiceTests
{
    [Fact]
    public async Task Create_records_plugin_provenance()
    {
        await using var fx = new DbFixture();
        var svc = new HabitTemplateService(
            fx.Factory, fx.Clock, fx.PluginContext,
            new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance),
            new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance),
            NullLogger<HabitTemplateService>.Instance);
        Guid id;
        using (fx.PluginContext.BeginScope("plug"))
            id = await svc.CreateAsync(new HabitTemplate { Name = "t" });
        await using var db = await fx.Factory.CreateDbContextAsync();
        var t = await db.HabitTemplates.SingleAsync(x => x.Id == id);
        Assert.Equal("plug", t.CreatedByPluginId);
    }

    [Fact]
    public async Task Delete_removes_template()
    {
        await using var fx = new DbFixture();
        var svc = CreateSut(fx);
        var id = await svc.CreateAsync(new HabitTemplate { Name = "t" });
        await svc.DeleteAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.False(await db.HabitTemplates.AnyAsync(t => t.Id == id));
    }

    [Fact]
    public async Task InstantiateAsync_clones_seed_template_into_new_habit_node_and_config()
    {
        await using var fx = new DbFixture();
        // Seed data is not applied automatically in DbFixture (EnsureCreated skips HasData on in-memory).
        // Insert a seed-shaped template directly.
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId,
                Name = "Morning routine",
                Description = "desc",
                PayloadJson = "{\"node\":{\"name\":\"Morning routine\",\"node_type\":\"Habit\",\"habit_type\":\"Action\",\"color\":\"#f5a623\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}}",
                IsOfficial = true,
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var node = await db2.HabitNodes.FindAsync(newId);
        node.Should().NotBeNull();
        node!.Name.Should().Be("Morning routine");
        node.NodeType.Should().Be(NodeType.Habit);
        node.HabitType.Should().Be(HabitType.Action);
        node.Color.Should().Be("#f5a623");
        node.ParentId.Should().BeNull();

        var cfg = await db2.HabitConfigs.SingleAsync(c => c.HabitNodeId == newId);
        cfg.ScheduleType.Should().Be(ScheduleType.Daily);
        cfg.StreakMode.Should().Be(StreakMode.Daily);
        cfg.StreakRecovery.Should().Be(StreakRecovery.GraceDay);
    }

    [Fact]
    public async Task InstantiateAsync_with_parent_attaches_under_group()
    {
        await using var fx = new DbFixture();
        var groupId = Guid.NewGuid();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = groupId, NodeType = NodeType.Group, Name = "G" });
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId,
                Name = "Daily journal",
                PayloadJson = "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}}",
                IsOfficial = true,
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: groupId);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.HabitNodes.FindAsync(newId))!.ParentId.Should().Be(groupId);
    }

    [Fact]
    public async Task InstantiateAsync_throws_on_unknown_template_id()
    {
        await using var fx = new DbFixture();
        var svc = CreateSut(fx);
        var act = async () => await svc.InstantiateAsync(Guid.NewGuid(), parentId: null);
        await act.Should().ThrowAsync<HabitTemplateNotFoundException>();
    }

    private static HabitTemplateService CreateSut(DbFixture fx) => new(
        fx.Factory, fx.Clock, fx.PluginContext,
        new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance),
        new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance),
        NullLogger<HabitTemplateService>.Instance);
}
