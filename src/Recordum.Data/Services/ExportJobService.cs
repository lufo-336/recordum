using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class ExportJobService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<ExportJobService> logger) : IExportJobService
{
    public async ValueTask<Guid> RecordAsync(ExportJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Id == Guid.Empty) job.Id = Guid.NewGuid();
        if (job.GeneratedAt == default) job.GeneratedAt = clock.UtcNow.UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ExportJobs.Add(job);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _ = logger;
        return job.Id;
    }

    public async ValueTask<IReadOnlyList<ExportJob>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ExportJobs.AsNoTracking().OrderByDescending(j => j.GeneratedAt).ToListAsync(ct).ConfigureAwait(false);
    }
}
