using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class StreakPauseService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<StreakPauseService> logger) : IStreakPauseService
{
    public async ValueTask<Guid> StartAsync(Guid habitNodeId, DateOnly from, DateOnly until, string? reason, CancellationToken ct = default)
    {
        if (until < from)
            throw new InvalidOperationException("PauseTo must be >= PauseFrom");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var pause = new StreakPause
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            PauseFrom = from,
            PauseTo = until,
            Reason = reason,
        };
        db.StreakPauses.Add(pause);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = logger;
        return pause.Id;
    }

    public async ValueTask EndAsync(Guid pauseId, DateOnly newUntil, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.StreakPauses.FirstOrDefaultAsync(x => x.Id == pauseId, ct);
        if (p is null) return;
        if (newUntil < p.PauseFrom)
            throw new InvalidOperationException("newUntil must be >= PauseFrom");
        p.PauseTo = newUntil;
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid pauseId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.StreakPauses.Where(x => x.Id == pauseId).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<IReadOnlyList<StreakPause>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.StreakPauses.AsNoTracking()
            .Where(x => x.HabitNodeId == habitNodeId)
            .OrderBy(x => x.PauseFrom)
            .ToListAsync(ct);
    }
}
