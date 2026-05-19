using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class ChartService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    ILogger<ChartService> logger) : IChartService
{
    public async ValueTask<Guid> CreateAsync(Chart draft, IReadOnlyList<ChartSeries>? series = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Charts.Add(draft);
        if (series is not null)
        {
            foreach (var s in series)
            {
                s.ChartId = draft.Id;
                if (s.Id == Guid.Empty) s.Id = Guid.NewGuid();
                db.ChartSeries.Add(s);
            }
        }
        await db.SaveChangesAsync(ct);
        _ = clock; _ = pluginContext; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Chart> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Charts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return;
        mutate(c);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask ReplaceSeriesAsync(Guid chartId, IReadOnlyList<ChartSeries> series, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(series);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.ChartSeries.Where(s => s.ChartId == chartId).ExecuteDeleteAsync(ct);
        foreach (var s in series)
        {
            s.ChartId = chartId;
            if (s.Id == Guid.Empty) s.Id = Guid.NewGuid();
            db.ChartSeries.Add(s);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.ChartSeries.Where(s => s.ChartId == id).ExecuteDeleteAsync(ct);
        await db.Charts.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async ValueTask<Chart?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Charts.Include(c => c.Series).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }
}
