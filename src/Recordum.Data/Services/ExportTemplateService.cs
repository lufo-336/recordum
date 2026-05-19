using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class ExportTemplateService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    ILogger<ExportTemplateService> logger) : IExportTemplateService
{
    public async ValueTask<Guid> CreateAsync(ExportTemplate draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.ExportTemplates.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = pluginContext; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<ExportTemplate> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ExportTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        mutate(t);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.ExportTemplates.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<ExportTemplate?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ExportTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<ExportTemplate>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ExportTemplates.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    }
}
