using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class TimestampInterceptorTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"recordum-ts-{Guid.NewGuid():N}.db");
    private readonly FakeClock _clock = new();
    private readonly AmbientPluginContext _pluginCtx = new();
    private readonly RecordumDbContext _db;

    public TimestampInterceptorTests()
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TimestampInterceptor(_clock, _pluginCtx))
            .Options;
        _db = new RecordumDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public void Insert_populates_created_and_updated_timestamps()
    {
        _clock.Now = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero);
        var node = new HabitNode { Id = Guid.NewGuid(), Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action };
        _db.HabitNodes.Add(node);
        _db.SaveChanges();

        Assert.Equal(_clock.Now.UtcDateTime, node.CreatedAt);
        Assert.Equal(_clock.Now.UtcDateTime, node.UpdatedAt);
        Assert.Equal(_clock.Now.ToUnixTimeMilliseconds(), node.UpdatedAtMs);
    }

    [Fact]
    public void Update_preserves_created_at_and_advances_updated()
    {
        _clock.Now = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero);
        var node = new HabitNode { Id = Guid.NewGuid(), Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action };
        _db.HabitNodes.Add(node);
        _db.SaveChanges();
        var createdAt = node.CreatedAt;

        _clock.Now = new DateTimeOffset(2026, 5, 14, 11, 0, 0, TimeSpan.Zero);
        node.Name = "y";
        _db.SaveChanges();

        Assert.Equal(createdAt, node.CreatedAt);
        Assert.Equal(_clock.Now.UtcDateTime, node.UpdatedAt);
    }

    [Fact]
    public void Insert_under_plugin_scope_populates_created_by_plugin_id()
    {
        var node = new HabitNode { Id = Guid.NewGuid(), Name = "from-plugin", NodeType = NodeType.Habit, HabitType = HabitType.Action };
        using (_pluginCtx.BeginScope("io.recordum.test"))
        {
            _db.HabitNodes.Add(node);
            _db.SaveChanges();
        }

        Assert.Equal("io.recordum.test", node.CreatedByPluginId);
    }

    [Fact]
    public void Insert_outside_plugin_scope_leaves_created_by_plugin_id_null()
    {
        var node = new HabitNode { Id = Guid.NewGuid(), Name = "user", NodeType = NodeType.Habit, HabitType = HabitType.Action };
        _db.HabitNodes.Add(node);
        _db.SaveChanges();

        Assert.Null(node.CreatedByPluginId);
    }

    [Fact]
    public void Update_under_plugin_scope_does_not_overwrite_created_by_plugin_id()
    {
        var node = new HabitNode { Id = Guid.NewGuid(), Name = "x", NodeType = NodeType.Habit, HabitType = HabitType.Action, CreatedByPluginId = "original" };
        _db.HabitNodes.Add(node);
        _db.SaveChanges();

        using (_pluginCtx.BeginScope("io.recordum.other"))
        {
            node.Name = "y";
            _db.SaveChanges();
        }

        Assert.Equal("original", node.CreatedByPluginId);
    }

    [Fact]
    public void Note_only_has_updated_at_ms_not_updated_at_property()
    {
        _clock.Now = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero);
        var parent = new HabitNode { Id = Guid.NewGuid(), Name = "p", NodeType = NodeType.Habit, HabitType = HabitType.Action };
        _db.HabitNodes.Add(parent);
        _db.SaveChanges();

        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = parent.Id, BodyMd = "hello" };
        _db.Notes.Add(note);
        _db.SaveChanges();

        Assert.Equal(_clock.Now.ToUnixTimeMilliseconds(), note.UpdatedAtMs);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch (IOException) { /* best effort cleanup */ }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UtcNow => Now;
        public long UtcNowMs => Now.ToUnixTimeMilliseconds();
    }
}
