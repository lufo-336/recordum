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
        var svc = CreateSut(fx);
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

    [Fact]
    public async Task InstantiateAsync_materializes_metric_with_scale_and_snapshots_labels()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        Guid scaleId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            // mood-5 ships in the ScaleTemplate seed — reference it, don't re-insert.
            scaleId = (await db.ScaleTemplates.SingleAsync(s => s.Code == "mood-5")).Id;
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Mood tracker", IsOfficial = true,
                PayloadJson = "{\"node\":{\"name\":\"Mood tracker\",\"node_type\":\"Habit\",\"habit_type\":\"State\",\"color\":\"#f39c12\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Mood\",\"type\":\"Integer\",\"scale_code\":\"mood-5\"}],\"segments\":[]}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var metric = await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId);
        metric.Name.Should().Be("Mood");
        metric.Type.Should().Be(MetricType.Integer);
        metric.ScaleTemplateId.Should().Be(scaleId);
        metric.LabelsJson.Should().Contain("Very good");
    }

    [Fact]
    public async Task InstantiateAsync_materializes_metric_unit_and_goal()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        Guid unitId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            // "l" ships in the UnitOfMeasure seed — reference it, don't re-insert.
            unitId = (await db.UnitsOfMeasure.SingleAsync(u => u.Code == "l")).Id;
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Water intake", IsOfficial = true,
                PayloadJson = "{\"node\":{\"name\":\"Water intake\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#5bc8f5\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Amount\",\"type\":\"Decimal\",\"unit\":\"l\"}],\"goal\":{\"target\":2,\"unit\":\"l\",\"period_type\":\"Open\"}}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var metric = await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId);
        metric.UnitOfMeasureId.Should().Be(unitId);

        var goal = await db2.Goals.SingleAsync(g => g.HabitNodeId == newId);
        goal.TargetValue.Should().Be(2);
        goal.PeriodType.Should().Be(GoalPeriodType.Open);
    }

    [Fact]
    public async Task InstantiateAsync_materializes_segments()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Workout log", IsOfficial = true,
                PayloadJson = "{\"node\":{\"name\":\"Workout log\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"None\",\"timer_mode\":\"Session\"},\"metrics\":[],\"segments\":[{\"name\":\"Work\",\"is_rest\":false,\"sort_order\":0},{\"name\":\"Rest\",\"is_rest\":true,\"sort_order\":1}]}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var segments = await db2.SegmentDefinitions.Where(s => s.HabitNodeId == newId).OrderBy(s => s.SortOrder).ToListAsync();
        segments.Select(s => s.Name).Should().Equal("Work", "Rest");
        segments[0].IsRest.Should().BeFalse();
        segments[1].IsRest.Should().BeTrue();
    }

    [Fact]
    public async Task InstantiateAsync_goal_inherits_the_habit_name_not_a_hardcoded_default()
    {
        // CR9: the goal's name must never fall back to a hardcoded English "Goal". When the payload
        // omits the node name, the effective habit name (the template name) is used instead.
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Meditate", IsOfficial = true,
                PayloadJson = "{\"node\":{\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7c5cff\"},\"goal\":{\"target\":1,\"period_type\":\"Open\"}}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var goal = await db2.Goals.SingleAsync(g => g.HabitNodeId == newId);
        goal.Name.Should().Be("Meditate");
    }

    [Fact]
    public async Task Instantiate_maps_repeatable_and_series_labels_from_the_payload()
    {
        await using var fx = new DbFixture();
        var payload = "{\"node\":{\"name\":\"S\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"}," +
                      "\"config\":{\"schedule_type\":\"Daily\",\"repeatable\":true,\"series_labels\":[\"Night\",\"Nap\"]},\"metrics\":[]}";
        var templateId = await InsertTemplateAsync(fx, payload);

        var svc = CreateSut(fx);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var nodeId = await svc.InstantiateAsync(templateId, parentId: null);

        var cfg = await configs.GetAsync(nodeId);
        cfg!.Repeatable.Should().BeTrue();
        cfg.SeriesLabelsJson.Should().Be("[\"Night\",\"Nap\"]");
    }

    private static async Task<Guid> InsertTemplateAsync(DbFixture fx, string payloadJson)
    {
        var templateId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitTemplates.Add(new HabitTemplate
        {
            Id = templateId,
            Name = "T",
            PayloadJson = payloadJson,
            IsOfficial = true,
        });
        await db.SaveChangesAsync();
        return templateId;
    }

    private static HabitTemplateService CreateSut(DbFixture fx) => new(
        fx.Factory, fx.Clock, fx.PluginContext,
        new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance),
        new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance),
        NullLogger<HabitTemplateService>.Instance);
}
