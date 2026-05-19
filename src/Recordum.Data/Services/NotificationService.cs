using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class NotificationService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<NotificationService> logger) : INotificationService
{
    public async ValueTask<Guid> CreateAsync(Notification draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Notifications.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Notification> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        mutate(n);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Notifications.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<IReadOnlyList<Notification>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notifications.AsNoTracking()
            .Where(x => x.HabitNodeId == habitNodeId)
            .ToListAsync(ct);
    }
}
