using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ExportTemplateServiceTests
{
    [Fact]
    public async Task Create_records_plugin_provenance()
    {
        await using var fx = new DbFixture();
        var svc = new ExportTemplateService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<ExportTemplateService>.Instance);
        Guid id;
        using (fx.PluginContext.BeginScope("plug"))
            id = await svc.CreateAsync(new ExportTemplate { Name = "t" });
        await using var db = await fx.Factory.CreateDbContextAsync();
        var t = await db.ExportTemplates.SingleAsync(x => x.Id == id);
        Assert.Equal("plug", t.CreatedByPluginId);
    }

    [Fact]
    public async Task Delete_removes_template()
    {
        await using var fx = new DbFixture();
        var svc = new ExportTemplateService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<ExportTemplateService>.Instance);
        var id = await svc.CreateAsync(new ExportTemplate { Name = "t" });
        await svc.DeleteAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.False(await db.ExportTemplates.AnyAsync(x => x.Id == id));
    }
}
