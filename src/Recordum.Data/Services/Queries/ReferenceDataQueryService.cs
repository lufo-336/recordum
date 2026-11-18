using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services.Queries;

public sealed class ReferenceDataQueryService(IDbContextFactory<RecordumDbContext> dbFactory)
    : IReferenceDataQueryService
{
    public async ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UnitsOfMeasure.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
    }

    public async ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var scales = await db.ScaleTemplates.AsNoTracking()
            .Include(s => s.Anchors).ThenInclude(a => a.Tag)
            .OrderBy(s => s.Name).ToListAsync(ct);
        foreach (var s in scales) s.Anchors.Sort((a, b) => a.Value.CompareTo(b.Value));
        return scales;
    }
}
