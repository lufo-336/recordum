using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class ActiveTimerService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ICompletionService completions) : IActiveTimerService
{
    public async ValueTask<ActiveTimer> StartAsync(Guid habitNodeId, TimerMode mode, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.ActiveTimers.AnyAsync(t => t.HabitNodeId == habitNodeId, ct))
            throw new TimerAlreadyActiveException(habitNodeId);

        var nowMs = clock.UtcNow.ToUnixTimeMilliseconds();
        var t = new ActiveTimer
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            StartedAtMs = nowMs,
            TimerMode = mode,
            SegmentIndex = mode == TimerMode.Session ? 0 : null,
        };
        db.ActiveTimers.Add(t);
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async ValueTask PauseAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ActiveTimers.FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct);
        if (t is null || t.PausedAtMs is not null) return;
        t.PausedAtMs = clock.UtcNow.ToUnixTimeMilliseconds();
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask ResumeAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ActiveTimers.FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct);
        if (t is null || t.PausedAtMs is null) return;
        var nowMs = clock.UtcNow.ToUnixTimeMilliseconds();
        t.AccumulatedPausedMs += nowMs - t.PausedAtMs.Value;
        t.PausedAtMs = null;
        await db.SaveChangesAsync(ct);
    }

    public ValueTask<Completion> StopAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        _ = completions; // used after Task 7
        throw new NotImplementedException();
    }

    public ValueTask NextSegmentAsync(Guid habitNodeId, CancellationToken ct = default) => throw new NotImplementedException();

    public async ValueTask<ActiveTimer?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ActiveTimers.AsNoTracking().FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct);
    }

    public async ValueTask<IReadOnlyList<ActiveTimer>> ListActiveAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ActiveTimers.AsNoTracking().ToListAsync(ct);
    }
}
