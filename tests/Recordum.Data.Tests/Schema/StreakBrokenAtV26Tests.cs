using Xunit;

namespace Recordum.Data.Tests.Schema;

public sealed class StreakBrokenAtV26Tests : IClassFixture<SchemaFixture>
{
    private readonly SchemaFixture _fixture;

    public StreakBrokenAtV26Tests(SchemaFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_adds_streak_broken_at_column_and_bumps_schema_version()
    {
        await using var conn = _fixture.OpenRawConnection();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(habit_configs);";
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            var found = false;
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "streak_broken_at", StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, "habit_configs.streak_broken_at must exist");
        }

        cmd.CommandText = "SELECT value FROM app_settings WHERE key = 'schema_version';";
        var version = (string?)await cmd.ExecuteScalarAsync();
        // schema_version is now "2.9" after NoteHistoryV29 migration runs on top
        Assert.Equal("2.9", version);
    }
}
