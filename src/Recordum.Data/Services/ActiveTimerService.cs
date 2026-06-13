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

        var nowMs = clock.UtcNowMs;
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
        t.PausedAtMs = clock.UtcNowMs;
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask ResumeAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ActiveTimers.FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct);
        if (t is null || t.PausedAtMs is null) return;
        var nowMs = clock.UtcNowMs;
        t.AccumulatedPausedMs += nowMs - t.PausedAtMs.Value;
        t.PausedAtMs = null;
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask<Completion> StopAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ActiveTimers.FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct)
            ?? throw new ActiveTimerNotFoundException(habitNodeId);

        var endMs = t.PausedAtMs ?? clock.UtcNowMs;

        var draft = new Completion
        {
            HabitNodeId = habitNodeId,
            CompletedAt = DateTimeOffset.FromUnixTimeMilliseconds(endMs).UtcDateTime,
            CompletedAtMs = endMs,
            StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(t.StartedAtMs).UtcDateTime,
            StartedAtMs = t.StartedAtMs,
            EndedAt = DateTimeOffset.FromUnixTimeMilliseconds(endMs).UtcDateTime,
            EndedAtMs = endMs,
            DurationSeconds = ElapsedSeconds(t, endMs),
        };

        db.ActiveTimers.Remove(t);
        await db.SaveChangesAsync(ct);
        await completions.RecordAsync(draft, values: null, segments: null, ct);
        return draft;
    }

    public async ValueTask<int> StopWithoutRecordingAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ActiveTimers.FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct)
            ?? throw new ActiveTimerNotFoundException(habitNodeId);

        var endMs = t.PausedAtMs ?? clock.UtcNowMs;
        var elapsed = ElapsedSeconds(t, endMs);
        db.ActiveTimers.Remove(t);
        await db.SaveChangesAsync(ct);
        return elapsed;
    }

    /// <summary>Elapsed active seconds at <paramref name="endMs"/>, net of accumulated pauses (never negative).</summary>
    private static int ElapsedSeconds(ActiveTimer t, long endMs)
    {
        var elapsedActiveMs = endMs - t.StartedAtMs - t.AccumulatedPausedMs;
        if (elapsedActiveMs < 0) elapsedActiveMs = 0;
        return (int)(elapsedActiveMs / 1000);
    }

    public async ValueTask NextSegmentAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.ActiveTimers.FirstOrDefaultAsync(x => x.HabitNodeId == habitNodeId, ct)
            ?? throw new ActiveTimerNotFoundException(habitNodeId);
        if (t.TimerMode != TimerMode.Session)
            throw new InvalidOperationException(
                $"NextSegment is only valid in Session mode (timer is {t.TimerMode}).");
        t.SegmentIndex = (t.SegmentIndex ?? 0) + 1;
        await db.SaveChangesAsync(ct);
    }

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
