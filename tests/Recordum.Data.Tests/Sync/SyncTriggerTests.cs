using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Backup;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Security;
using Recordum.Data.Services;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;

namespace Recordum.Data.Tests.Sync;

/// <summary>I trigger del Task 3: lapidi e ore di modifica scritte dal database, con l'ora da <c>IClock</c>,
/// per ogni via di scrittura (ExecuteDelete/ExecuteUpdate, SQL, cascade).</summary>
public sealed class SyncTriggerTests
{
    private static FakeClock ClockAt(long ms) => new(DateTimeOffset.FromUnixTimeMilliseconds(ms));

    private static HabitNode NewHabit() => new()
    {
        Id = Guid.NewGuid(),
        Name = "h",
        NodeType = NodeType.Habit,
        HabitType = HabitType.Action,
        Color = "#808080",
    };

    private static async Task<(HabitNode Habit, StreakPause Pause)> SeedPauseAsync(DbFixture fx)
    {
        var habit = NewHabit();
        var pause = new StreakPause
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habit.Id,
            PauseFrom = new DateOnly(2026, 1, 1),
            PauseTo = new DateOnly(2026, 1, 2),
        };
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(habit);
        db.StreakPauses.Add(pause);
        await db.SaveChangesAsync();
        return (habit, pause);
    }

    private static string Id(Guid id) => id.ToString("D").ToUpperInvariant();

    private static async Task<List<Models.SyncTombstone>> TombstonesAsync(IDbContextFactory<RecordumDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SyncTombstones.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task ExecuteDeleteLeavesTombstone()
    {
        await using var fx = new DbFixture(ClockAt(1_000));
        var (_, pause) = await SeedPauseAsync(fx);

        await using (var db = await fx.Factory.CreateDbContextAsync())
            await db.StreakPauses.Where(p => p.Id == pause.Id).ExecuteDeleteAsync();

        var stones = await TombstonesAsync(fx.Factory);
        stones.Should().ContainSingle();
        stones[0].Entity.Should().Be("streak_pause");
        stones[0].EntityId.Should().Be(Id(pause.Id));
        stones[0].DeletedAtMs.Should().Be(1_000);
        stones[0].DeviceId.Should().BeNull();
        stones[0].AliasOf.Should().BeNull();
    }

    [Fact]
    public async Task CascadeDeleteLeavesTombstonesForChildren()
    {
        // attachment.note_id è Restrict: lo si cancella prima (come NoteService); note_histories e note_tag seguono la
        // nota per cascade del database, ed è quella che l'interceptor non vede.
        await using var fx = new DbFixture(ClockAt(1_000));
        var habit = NewHabit();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit.Id, BodyMd = "b" };
        var attachment = new Models.Attachment { Id = Guid.NewGuid(), NoteId = note.Id, Filename = "a.jpg", MimeType = "image/jpeg" };
        var history = new NoteHistory { Id = Guid.NewGuid(), NoteId = note.Id, BodyMd = "a" };
        var tag = new Tag { Id = Guid.NewGuid(), Name = "t" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            db.Notes.Add(note);
            db.Attachments.Add(attachment);
            db.NoteHistories.Add(history);
            db.Tags.Add(tag);
            db.NoteTags.Add(new NoteTag { NoteId = note.Id, TagId = tag.Id, Source = NoteTagSource.Inline });
            await db.SaveChangesAsync();
        }

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            await db.Attachments.Where(a => a.NoteId == note.Id).ExecuteDeleteAsync();
            await db.Notes.Where(n => n.Id == note.Id).ExecuteDeleteAsync();
            (await db.NoteHistories.CountAsync()).Should().Be(0, "la cascade del database ha tolto lo storico");
        }

        var stones = (await TombstonesAsync(fx.Factory)).Select(t => (t.Entity, t.EntityId)).ToList();
        stones.Should().BeEquivalentTo(new[]
        {
            ("attachment", Id(attachment.Id)),
            ("note", Id(note.Id)),
            ("note_history", Id(history.Id)),
            ("note_tag", $"{Id(note.Id)}|{Id(tag.Id)}|Inline"),
        });
    }

    [Fact]
    public async Task PurgeLeavesTombstonesForNodeAndChildren()
    {
        // Il soft delete è un UPDATE (nessuna lapide); la purga del cestino è un DELETE vero.
        await using var fx = new DbFixture(ClockAt(1_000));
        var (habit, pause) = await SeedPauseAsync(fx);
        var completion = new Completion { Id = Guid.NewGuid(), HabitNodeId = habit.Id, CompletedAt = DateTime.UnixEpoch };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.Completions.Add(completion);
            await db.SaveChangesAsync();
        }

        await new PurgeService(fx.Factory, fx.Clock, NullLogger<PurgeService>.Instance).PurgeSubtreeAsync(habit.Id);

        var stones = (await TombstonesAsync(fx.Factory)).Select(t => (t.Entity, t.EntityId)).ToList();
        stones.Should().BeEquivalentTo(new[]
        {
            ("habit_node", Id(habit.Id)),
            ("streak_pause", Id(pause.Id)),
            ("completion", Id(completion.Id)),
        });
    }

    [Fact]
    public async Task ExecuteUpdateBumpsUpdatedAtMs()
    {
        var clock = ClockAt(1_000);
        await using var fx = new DbFixture(clock);
        var (_, pause) = await SeedPauseAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE streak_pauses SET updated_at_ms = 5;");
        (await db.StreakPauses.AsNoTracking().SingleAsync()).UpdatedAtMs.Should().Be(5, "chi scrive l'ora la tiene");

        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(2_000));
        await db.StreakPauses.Where(p => p.Id == pause.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Reason, "x"));

        (await db.StreakPauses.AsNoTracking().SingleAsync()).UpdatedAtMs.Should().Be(2_000);
    }

    [Fact]
    public async Task ExplicitUpdatedAtMsIsKept()
    {
        await using var fx = new DbFixture(ClockAt(1_000));
        await SeedPauseAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE streak_pauses SET reason = 'y', updated_at_ms = 42;");
        (await db.StreakPauses.AsNoTracking().SingleAsync()).UpdatedAtMs.Should().Be(42);
    }

    [Fact]
    public async Task ApplyingSuppressesBoth()
    {
        var clock = ClockAt(1_000);
        await using var fx = new DbFixture(clock);
        var (_, pause) = await SeedPauseAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE streak_pauses SET updated_at_ms = 5;");
        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(2_000));

        var state = SyncConnectionState.For(db.Database.GetDbConnection());
        state.Applying = true;
        try
        {
            await db.StreakPauses.ExecuteUpdateAsync(s => s.SetProperty(p => p.Reason, "x"));
            (await db.StreakPauses.AsNoTracking().SingleAsync()).UpdatedAtMs.Should().Be(5);
            await db.StreakPauses.Where(p => p.Id == pause.Id).ExecuteDeleteAsync();
        }
        finally
        {
            state.Applying = false;
        }

        (await TombstonesAsync(fx.Factory)).Should().BeEmpty();
    }

    [Fact]
    public async Task EveryTravellingTableHasBothTriggers()
    {
        // Sul DB migrato (la via dell'app), non solo su EnsureCreated.
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        await using var db = TestDbHelper.CreateFull(conn);
        await db.Database.MigrateAsync();
        await SyncTriggerInstaller.EnsureAsync(db, CancellationToken.None);
        await SyncTriggerInstaller.EnsureAsync(db, CancellationToken.None);   // idempotente

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, tbl_name FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'trg_sync_%'";
        var triggers = new List<(string Name, string Table)>();
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync()) triggers.Add((reader.GetString(0), reader.GetString(1)));

        var expected = SyncCatalog.Entities
            .SelectMany(e => new[]
            {
                ($"trg_sync_del_{e.Table}", e.Table),
                ($"trg_sync_upd_{e.Table}", e.Table),
                ($"trg_sync_ins_{e.Table}", e.Table),
            })
            .Concat(SyncCatalog.Entities
                .Where(e => SyncCatalog.TimedFields(e).Count > 0)
                .Select(e => ($"trg_sync_fms_{e.Table}", e.Table)));
        triggers.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task NonTravellingSettingUpdateLeavesNoFieldMs()
    {
        var clock = ClockAt(1_000);
        await using var fx = new DbFixture(clock);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.AppSettings.Add(new AppSetting { Key = "home.rows", Value = "compact" });
            await db.SaveChangesAsync();
        }

        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(2_000));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            await db.AppSettings.Where(s => s.Key == "home.rows")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "relaxed"));
            (await db.SyncFieldMs.AsNoTracking().ToListAsync()).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ApplyingSuppressesFieldMs()
    {
        var clock = ClockAt(1_000);
        await using var fx = new DbFixture(clock);
        var (_, pause) = await SeedPauseAsync(fx);

        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(2_000));
        await using var db = await fx.Factory.CreateDbContextAsync();
        var state = SyncConnectionState.For(db.Database.GetDbConnection());
        state.Applying = true;
        try
        {
            await db.StreakPauses.Where(p => p.Id == pause.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Reason, "from-sync"));
        }
        finally
        {
            state.Applying = false;
        }

        (await db.SyncFieldMs.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task NonTravellingSettingChangeLeavesNoTombstone()
    {
        var clock = ClockAt(1_000);
        await using var fx = new DbFixture(clock);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            if (!await db.AppSettings.AnyAsync(s => s.Key == "daily_reset_time"))
                db.AppSettings.Add(new AppSetting { Key = "daily_reset_time", Value = "00:00" });
            db.AppSettings.Add(new AppSetting { Key = "home.rows", Value = "compact" });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("UPDATE app_settings SET updated_at_ms = 5;");
        }

        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(2_000));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE app_settings SET value = value || '';");
            var ms = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.UpdatedAtMs);
            ms["home.rows"].Should().Be(5, "una chiave locale non prende l'ora della sync");
            ms["daily_reset_time"].Should().Be(2_000);

            await db.AppSettings.Where(s => s.Key == "home.rows").ExecuteDeleteAsync();
            (await TombstonesAsync(fx.Factory)).Should().BeEmpty();

            await db.AppSettings.Where(s => s.Key == "daily_reset_time").ExecuteDeleteAsync();
        }

        var stones = await TombstonesAsync(fx.Factory);
        stones.Should().ContainSingle();
        (stones[0].Entity, stones[0].EntityId, stones[0].DeletedAtMs).Should().Be(("app_setting", "daily_reset_time", 2_000L));
    }

    [Fact]
    public async Task RecursiveTriggersAreOff()
    {
        // L'UPDATE dentro trg_sync_upd_* non deve riaccendere il trigger.
        await using var fx = new DbFixture(ClockAt(1_000));
        await using var db = await fx.Factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA recursive_triggers";
        Convert.ToInt64(await cmd.ExecuteScalarAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RawConnectionPathsDoNoDmlOnTravellingTables()
    {
        // Backup, ripristino (con l'istantanea di sicurezza) e rekey aprono il DB con SqlCipherConnectionFactory, senza
        // le funzioni della sync: su un DB pieno, coi trigger, non devono fallire con "no such function".
        await using var fx = await EncryptedDbFixture.SeedAsync();
        var habit = NewHabit();
        var pause = new StreakPause
        {
            Id = Guid.NewGuid(), HabitNodeId = habit.Id,
            PauseFrom = new DateOnly(2026, 1, 1), PauseTo = new DateOnly(2026, 1, 2),
        };
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            db.StreakPauses.Add(pause);
            await db.SaveChangesAsync();
        }

        var backup = await fx.CreateBackupAsync("pass-phrase-1");
        await fx.NewRestoreService().RestoreAsync(backup.Path, "pass-phrase-1", new RestoreOptions(SkipSafetySnapshot: false));
        SqliteConnection.ClearAllPools();

        // I trigger hanno attraversato backup e ripristino: la via EF lascia ancora la lapide.
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            await db.StreakPauses.Where(p => p.Id == pause.Id).ExecuteDeleteAsync();
        (await TombstonesAsync(fx.DbFactory)).Should().ContainSingle(t => t.EntityId == Id(pause.Id));

        SqliteConnection.ClearAllPools();
        using (var key = await fx.KeyProvider.GetKeyAsync())
        using (var conn = SqlCipherConnectionFactory.Open(fx.Options.DatabasePath, key))
        using (var rekey = conn.CreateCommand())
        {
            rekey.CommandText = CipherPragmas.Rekey(new byte[32]);
            rekey.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
    }
}
