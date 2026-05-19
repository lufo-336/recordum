using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Recordum.Data.Tests.Fixtures;

public class FixtureSmokeTests
{
    [Fact]
    public async Task Fixture_creates_schema_and_disposes()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.True(await db.Database.CanConnectAsync());
        Assert.Empty(db.HabitNodes);
    }
}
