using Recordum.Data.Backup;
using Recordum.Data.Tests.Fixtures;
using Recordum.Core.Models;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public sealed class DbSchemaVersionTests
{
    [Fact]
    public async Task ReadAsync_returns_appsetting_value()
    {
        var fx = new DbFixture();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var row = await db.AppSettings.FindAsync("schema_version");
            if (row is null) db.AppSettings.Add(new AppSetting { Key = "schema_version", Value = "2.12" });
            else row.Value = "2.12";
            await db.SaveChangesAsync();
        }
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Equal("2.12", await DbSchemaVersion.ReadAsync(db2, "9.9", default));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ReadAsync_missing_row_returns_fallback()
    {
        var fx = new DbFixture();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var row = await db.AppSettings.FindAsync("schema_version");
            if (row is not null) { db.AppSettings.Remove(row); await db.SaveChangesAsync(); }
        }
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Equal("2.7", await DbSchemaVersion.ReadAsync(db2, "2.7", default));
        await fx.DisposeAsync();
    }
}
