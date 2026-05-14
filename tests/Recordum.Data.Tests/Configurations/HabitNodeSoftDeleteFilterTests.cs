using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;
using Xunit;

namespace Recordum.Data.Tests.Configurations;

public class HabitNodeSoftDeleteFilterTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"recordum-test-{Guid.NewGuid():N}.db");
    private readonly RecordumDbContext _db;

    public HabitNodeSoftDeleteFilterTests()
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options;
        _db = new RecordumDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public void Soft_deleted_nodes_are_excluded_from_default_queries()
    {
        var alive = new HabitNode { Id = Guid.NewGuid(), Name = "alive", NodeType = NodeType.Habit, HabitType = HabitType.Action };
        var dead = new HabitNode { Id = Guid.NewGuid(), Name = "dead", NodeType = NodeType.Habit, HabitType = HabitType.Action, DeletedAt = DateTime.UtcNow };
        _db.HabitNodes.AddRange(alive, dead);
        _db.SaveChanges();

        var visible = _db.HabitNodes.Select(n => n.Name).ToList();

        Assert.Contains("alive", visible);
        Assert.DoesNotContain("dead", visible);
    }

    [Fact]
    public void IgnoreQueryFilters_includes_soft_deleted()
    {
        var dead = new HabitNode { Id = Guid.NewGuid(), Name = "dead", NodeType = NodeType.Habit, HabitType = HabitType.Action, DeletedAt = DateTime.UtcNow };
        _db.HabitNodes.Add(dead);
        _db.SaveChanges();

        var all = _db.HabitNodes.IgnoreQueryFilters().Select(n => n.Name).ToList();

        Assert.Contains("dead", all);
    }

    public void Dispose()
    {
        _db.Dispose();
        // Allow SQLite to release file handles before deletion
        GC.SuppressFinalize(this);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch (IOException) { /* best effort cleanup */ }
    }
}
