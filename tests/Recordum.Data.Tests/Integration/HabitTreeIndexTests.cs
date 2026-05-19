using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitTreeIndexTests
{
    [Fact]
    public async Task Completion_index_for_home_query_is_declared()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND tbl_name='completions';";
        var indexDdls = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (!r.IsDBNull(0)) indexDdls.Add(r.GetString(0));
        Assert.Contains(indexDdls, s =>
            s.Contains("habit_node_id", StringComparison.Ordinal) &&
            s.Contains("completed_at_ms", StringComparison.Ordinal));
    }
}
