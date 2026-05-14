using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Data;
using Xunit;

namespace Recordum.Data.Tests.Schema;

/// <summary>
/// Shared fixture that applies the InitialSchema migration to a temp SQLite file once per test class.
/// Implements IDisposable to clean up the DB file after all tests in the class finish.
/// </summary>
public sealed class SchemaFixture : IDisposable
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"recordum-schema-{Guid.NewGuid():N}.db");

    public RecordumDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RecordumDbContext(options);
    }

    public SqliteConnection OpenRawConnection()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    public SchemaFixture()
    {
        // Apply migration once so all tests share the same migrated DB.
        using var ctx = CreateContext();
        ctx.Database.Migrate();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { if (File.Exists(DbPath)) File.Delete(DbPath); } catch (IOException) { /* best effort */ }
    }
}
