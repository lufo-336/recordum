using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class TimestampInterceptorTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"recordum-ts-{Guid.NewGuid():N}.db");
    private readonly FakeClock _clock = new();
    private readonly AmbientPluginContext _pluginCtx = new();
    private readonly FakeDeviceIdentity _deviceIdentity = new();
    private readonly RecordumDbContext _db;

    public TimestampInterceptorTests()
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TimestampInterceptor(_clock, _pluginCtx, _deviceIdentity))
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

    [Fact]
    public void StampsLastEditedDeviceNameOnlyWhenTextChanges()
    {
        var spy = new SpyDeviceIdentity { DeviceName = "Fedora" };
        using var db = CreateSpyContext(spy);

        var parent = new HabitNode { Id = Guid.NewGuid(), Name = "p", NodeType = NodeType.Habit, HabitType = HabitType.Journal };
        db.HabitNodes.Add(parent);
        db.SaveChanges();
        Assert.Equal(0, spy.Reads);

        // Added con testo vuoto / nullo: non legge DeviceIdentity né stampa.
        var emptyNote = new Note { Id = Guid.NewGuid(), HabitNodeId = parent.Id, BodyMd = string.Empty };
        var bareCompletion = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = parent.Id,
            CompletedAt = _clock.Now.UtcDateTime,
            CompletedAtMs = _clock.UtcNowMs,
        };
        db.Notes.Add(emptyNote);
        db.Completions.Add(bareCompletion);
        db.SaveChanges();
        Assert.Null(emptyNote.LastEditedDeviceName);
        Assert.Null(bareCompletion.LastEditedDeviceName);
        Assert.Equal(0, spy.Reads);

        // Added con testo non vuoto: legge e stampa "Fedora".
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = parent.Id, BodyMd = "v1" };
        var completion = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = parent.Id,
            CompletedAt = _clock.Now.UtcDateTime,
            CompletedAtMs = _clock.UtcNowMs,
            Notes = "c1",
        };
        db.Notes.Add(note);
        db.Completions.Add(completion);
        db.SaveChanges();
        Assert.Equal("Fedora", note.LastEditedDeviceName);
        Assert.Equal("Fedora", completion.LastEditedDeviceName);
        Assert.Equal(1, spy.Reads);

        // Modifica di altri campi: DeviceIdentity non viene letto, LastEditedDeviceName resta "Fedora".
        spy.DeviceName = "Mac";
        note.IsSensitive = true;
        note.TargetDate = new DateOnly(2026, 5, 14);
        completion.IsRetroactive = true;
        db.SaveChanges();
        Assert.Equal("Fedora", note.LastEditedDeviceName);
        Assert.Equal("Fedora", completion.LastEditedDeviceName);
        Assert.Equal(1, spy.Reads);

        // Modifica solo di BodyMd: ora aggiorna solo la nota.
        note.BodyMd = "v2";
        db.SaveChanges();
        Assert.Equal("Mac", note.LastEditedDeviceName);
        Assert.Equal("Fedora", completion.LastEditedDeviceName);
        Assert.Equal(2, spy.Reads);

        // Modifica solo di Completion.Notes: ora aggiorna la completion.
        spy.DeviceName = "Linux";
        completion.Notes = "c2";
        db.SaveChanges();
        Assert.Equal("Linux", completion.LastEditedDeviceName);
        Assert.Equal("Mac", note.LastEditedDeviceName);
        Assert.Equal(3, spy.Reads);
    }

    [Fact]
    public async Task DeviceIdentity_reads_sync_meta_lazily_caches_and_reloads_after_invalidate()
    {
        await using var fx = new DbFixture();
        var identity = new DeviceIdentity(new FactoryServiceProvider(fx.Factory));

        // Tabella sync_meta vuota -> null.
        Assert.Null(identity.DeviceName);

        // Anche inserendo la riga, resta in cache finché non si chiama Invalidate().
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.SyncMeta.Add(new SyncMeta
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-1",
                DeviceName = "Fedora",
                CreatedAt = _clock.Now.UtcDateTime,
            });
            await db.SaveChangesAsync();
        }
        Assert.Null(identity.DeviceName);

        identity.Invalidate();
        Assert.Equal("Fedora", identity.DeviceName);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var meta = await db.SyncMeta.SingleAsync();
            meta.DeviceName = "Fedora-Renamed";
            await db.SaveChangesAsync();
        }
        Assert.Equal("Fedora", identity.DeviceName);

        identity.Invalidate();
        Assert.Equal("Fedora-Renamed", identity.DeviceName);
    }

    [Fact]
    public void DeviceIdentity_read_failure_returns_null_and_retries_instead_of_failing_the_save()
    {
        // Ruling 9: la lettura di sync_meta passa da una seconda connessione dentro SavingChanges; se quella fallisce
        // (busy_timeout scaduto dietro una transazione grande) il salvataggio dell'utente non deve morire.
        var factory = new ThrowingFactory();
        var identity = new DeviceIdentity(new FactoryServiceProvider(factory));

        Assert.Null(identity.DeviceName);
        Assert.Null(identity.DeviceName);
        Assert.Equal(2, factory.Calls);   // un fallimento non si mette in cache: si riprova
    }

    private sealed class ThrowingFactory : IDbContextFactory<RecordumDbContext>
    {
        public int Calls { get; private set; }
        public RecordumDbContext CreateDbContext()
        {
            Calls++;
            throw new Microsoft.Data.Sqlite.SqliteException("database is locked", 5);
        }
    }

    private RecordumDbContext CreateSpyContext(IDeviceIdentity identity)
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TimestampInterceptor(_clock, _pluginCtx, identity))
            .Options;
        return new RecordumDbContext(options);
    }

    private sealed class SpyDeviceIdentity : IDeviceIdentity
    {
        private string? _deviceName;
        public int Reads { get; private set; }
        public string? DeviceName
        {
            get
            {
                Reads++;
                return _deviceName;
            }
            set => _deviceName = value;
        }
    }

    private sealed class FactoryServiceProvider(IDbContextFactory<RecordumDbContext> factory) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IDbContextFactory<RecordumDbContext>) ? factory : null;
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
        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
