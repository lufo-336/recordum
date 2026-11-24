using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;

namespace Recordum.Data.Tests.Sync;

public sealed class SyncMigrationDownTests
{
    [Fact]
    public async Task DownFromSyncRemovesTriggersFirst()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        await using var db = TestDbHelper.CreateFull(conn);
        await db.Database.MigrateAsync();
        await SyncTriggerInstaller.EnsureAsync(db, CancellationToken.None);

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var syncFoundationIndex = applied.FindIndex(m => m.EndsWith("_SyncFoundationV221", StringComparison.Ordinal));
        syncFoundationIndex.Should().BeGreaterThan(0);
        var beforeSync = applied[syncFoundationIndex - 1];

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(beforeSync);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'trg_sync_%'";
        Convert.ToInt64(await cmd.ExecuteScalarAsync()).Should().Be(0);

        var remaining = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        remaining.Last().Should().Be(beforeSync);
    }
}
