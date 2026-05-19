using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class TimestampInterceptorProvenanceTests
{
    [Fact]
    public async Task PluginId_resolved_per_save_not_at_construction()
    {
        await using var fx = new DbFixture();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = Guid.NewGuid(),
                NodeType = NodeType.Group,
                Name = "g",
            });
            await db.SaveChangesAsync();
        }
        using (fx.PluginContext.BeginScope("com.example.plugin"))
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = Guid.NewGuid(),
                NodeType = NodeType.Group,
                Name = "g2",
            });
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var rows = await db.HabitNodes.OrderBy(h => h.Name).ToListAsync();
            Assert.Null(rows[0].CreatedByPluginId);
            Assert.Equal("com.example.plugin", rows[1].CreatedByPluginId);
        }
    }

    [Fact]
    public async Task Child_entities_do_not_get_provenance_column()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        using (fx.PluginContext.BeginScope("com.example.plugin"))
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "h",
            });
            db.Completions.Add(new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                CompletedAt = DateTime.UtcNow,
                CompletedAtMs = 1,
            });
            await db.SaveChangesAsync();
        }
        Assert.Null(typeof(Completion).GetProperty("CreatedByPluginId"));
    }
}
