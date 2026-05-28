using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class BackupLogServiceTests
{
    // Use a plain context without interceptors so CreatedAt is preserved exactly as set
    private static RecordumDbContext OpenPlainContext(SqliteConnection conn)
    {
        var opts = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite(conn)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RecordumDbContext(opts);
    }

    [Fact]
    public async Task Record_then_list_returns_newest_first()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using (var setupCtx = OpenPlainContext(conn))
            setupCtx.Database.EnsureCreated();

        // Each insert uses its own context so TimestampInterceptor (from DbFixture) doesn't overwrite CreatedAt
        using (var ctx1 = OpenPlainContext(conn))
            await new BackupLogService(ctx1).RecordAsync(new BackupLog { Id = Guid.NewGuid(), CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Kind = "Manual", Path = "a", SizeBytes = 1, SchemaVersion = "2.7", AppVersion = "3.10.0" });

        using (var ctx2 = OpenPlainContext(conn))
            await new BackupLogService(ctx2).RecordAsync(new BackupLog { Id = Guid.NewGuid(), CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), Kind = "Auto", Path = "b", SizeBytes = 2, SchemaVersion = "2.7", AppVersion = "3.10.0" });

        using var readCtx = OpenPlainContext(conn);
        var list = await new BackupLogService(readCtx).ListAsync(take: 10);

        list.Select(x => x.Path).Should().Equal("b", "a");
    }

    [Fact]
    public async Task DeleteAsync_removes_row()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using (var setupCtx = OpenPlainContext(conn))
            setupCtx.Database.EnsureCreated();

        using var ctx = OpenPlainContext(conn);
        var svc = new BackupLogService(ctx);
        var id = Guid.NewGuid();
        await svc.RecordAsync(new BackupLog { Id = id, CreatedAt = DateTime.UtcNow, Kind = "Manual", Path = "x", SizeBytes = 1, SchemaVersion = "2.7", AppVersion = "3.10.0" });

        await svc.DeleteAsync(id);

        (await svc.ListAsync(take: 10)).Should().BeEmpty();
    }
}
