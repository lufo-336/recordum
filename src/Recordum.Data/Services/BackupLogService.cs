using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class BackupLogService(RecordumDbContext db) : IBackupLogService
{
    public async Task RecordAsync(BackupLog row, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
        db.BackupLogs.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BackupLog>> ListAsync(int take = 50, CancellationToken ct = default)
        => await db.BackupLogs.AsNoTracking()
                              .OrderByDescending(x => x.CreatedAt)
                              .Take(take)
                              .ToListAsync(ct).ConfigureAwait(false);

    public async Task<BackupLog?> GetAsync(Guid id, CancellationToken ct = default)
        => await db.BackupLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.BackupLogs.Where(x => x.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
