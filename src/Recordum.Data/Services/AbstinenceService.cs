using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed class AbstinenceService(IDbContextFactory<RecordumDbContext> dbFactory, IClock clock, IEventBus eventBus) : IAbstinenceService
{
    private readonly IEventBus _events = eventBus;

    public async Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var open = await db.AbstinencePeriods.AsNoTracking()
            .Where(p => p.HabitNodeId == habitNodeId && p.EndedAtMs == null)
            .OrderByDescending(p => p.StartedAtMs).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (open is not null)
            return new AbstinenceState(AbstinenceStatus.Abstaining, open.StartedAtMs, null);

        var lastClosed = await db.AbstinencePeriods.AsNoTracking()
            .Where(p => p.HabitNodeId == habitNodeId && p.EndedAtMs != null)
            .OrderByDescending(p => p.EndedAtMs).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return lastClosed is null
            ? new AbstinenceState(AbstinenceStatus.NotStarted, null, null)
            : new AbstinenceState(AbstinenceStatus.Relapsed, null, lastClosed.EndedAtMs);
    }

    public async Task StartAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var hasOpen = await db.AbstinencePeriods
            .AnyAsync(p => p.HabitNodeId == habitNodeId && p.EndedAtMs == null, ct).ConfigureAwait(false);
        if (hasOpen) return; // guard: at most one open period

        db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            StartedAtMs = clock.UtcNowMs,
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _events.Publish(new AbstinenceChangedPayload(habitNodeId, WellKnownEvents.AbstinenceStarted));
    }

    public async Task LapseAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var open = await db.AbstinencePeriods
            .Where(p => p.HabitNodeId == habitNodeId && p.EndedAtMs == null)
            .OrderByDescending(p => p.StartedAtMs).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (open is null) return; // guard

        open.EndedAtMs = clock.UtcNowMs;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _events.Publish(new AbstinenceChangedPayload(habitNodeId, WellKnownEvents.AbstinenceLapsed));
    }

    public async Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var fromMs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        return await db.AbstinencePeriods.AsNoTracking()
            .Where(p => p.HabitNodeId == habitNodeId
                        && p.StartedAtMs <= toMs
                        && (p.EndedAtMs == null || p.EndedAtMs >= fromMs))
            .OrderBy(p => p.StartedAtMs)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var period = await db.AbstinencePeriods
            .FirstOrDefaultAsync(p => p.Id == periodId, ct).ConfigureAwait(false);
        if (period is null) return; // guard: unknown id

        // Read before the delete: the payload names the habit whose surfaces have to reload, and
        // after SaveChanges the entity is detached.
        var habitNodeId = period.HabitNodeId;
        db.AbstinencePeriods.Remove(period);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _events.Publish(new AbstinenceChangedPayload(habitNodeId, WellKnownEvents.AbstinencePeriodDeleted));
    }
}
