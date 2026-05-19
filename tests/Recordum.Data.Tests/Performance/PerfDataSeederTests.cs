using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Performance;

public class PerfDataSeederTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task Seed_produces_expected_counts()
    {
        await using var fx = new DbFixture();
        await PerfDataSeeder.SeedAsync(fx.Factory, seed: 42);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(200, await db.HabitNodes.CountAsync(n => n.NodeType == NodeType.Habit));
        Assert.Equal(50_000, await db.Completions.CountAsync());
    }
}
