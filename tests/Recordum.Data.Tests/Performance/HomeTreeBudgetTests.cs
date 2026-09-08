using System.Diagnostics;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Performance;

public class HomeTreeBudgetTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task GetHomeTreeAsync_p95_under_300ms_on_seeded_dataset()
    {
        await using var fx = new DbFixture();
        await PerfDataSeeder.SeedAsync(fx.Factory);
        var q = new HabitTreeQueryService(fx.Factory, fx.Clock, fx.ResetTimes);

        for (int i = 0; i < 3; i++) _ = await q.GetHomeTreeAsync();

        var samples = new List<long>();
        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = await q.GetHomeTreeAsync();
            samples.Add(sw.ElapsedMilliseconds);
        }
        samples.Sort();
        var p95 = samples[(int)(samples.Count * 0.95)];
        Assert.True(p95 < 300, $"p95={p95}ms exceeds 300ms budget. samples=[{string.Join(",", samples)}]");
    }
}
