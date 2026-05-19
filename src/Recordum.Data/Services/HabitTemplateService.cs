using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class HabitTemplateService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    ILogger<HabitTemplateService> logger) : IHabitTemplateService
{
    public async ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.HabitTemplates.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = pluginContext; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.HabitTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        mutate(t);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.HabitTemplates.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.HabitTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.HabitTemplates.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    }
}
