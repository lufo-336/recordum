using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public sealed class ExportJobServiceTests
{
    [Fact]
    public async Task RecordAsync_persists_and_ListAsync_returns_newest_first()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var svc = new ExportJobService(fx.Factory, fx.Clock, NullLogger<ExportJobService>.Instance);

        await svc.RecordAsync(new ExportJob { Format = ExportFormat.Json, FilePath = "a.json" });
        await svc.RecordAsync(new ExportJob { Format = ExportFormat.Pdf, FilePath = "b.pdf" });

        var all = await svc.ListAsync();
        Assert.Equal(2, all.Count);
        Assert.All(all, j => Assert.NotEqual(default, j.GeneratedAt));
        await fx.DisposeAsync();
    }
}
