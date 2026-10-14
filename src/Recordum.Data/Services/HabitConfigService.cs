using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed partial class HabitConfigService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<HabitConfigService> logger,
    IEventBus eventBus) : IHabitConfigService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "HabitConfig for {HabitNodeId} upserted")]
    private partial void LogUpserted(Guid habitNodeId);

    public async ValueTask<HabitConfig?> GetAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.HabitConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.HabitNodeId == habitNodeId, ct);
    }

    public async ValueTask<IReadOnlyList<HabitConfigHistory>> ListHistoryAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Set<HabitConfigHistory>().AsNoTracking()
            .Where(h => h.HabitNodeId == habitNodeId)
            .OrderBy(h => h.ValidFrom)
            .ToListAsync(ct);
    }

    public async ValueTask UpsertAsync(Guid habitNodeId, Action<HabitConfig> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var cfg = await db.HabitConfigs.FirstOrDefaultAsync(c => c.HabitNodeId == habitNodeId, ct);
        if (cfg is null)
        {
            cfg = new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = habitNodeId };
            db.HabitConfigs.Add(cfg);
        }
        mutate(cfg);
        ValidateCrossFields(cfg);
        await db.SaveChangesAsync(ct);
        LogUpserted(habitNodeId);
        // Il cronometro e il quick-add di una schermata aperta dipendono dalla config (modalita', ripetibile):
        // chi la scrive lo dice, come HabitNodeService per nome/colore. Non lo fa la view.
        eventBus.Publish(new HabitUpdatedPayload(habitNodeId));
        _ = clock;
    }

    public async ValueTask AddSegmentAsync(Guid habitNodeId, SegmentDefinition segment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (segment.Id == Guid.Empty) segment.Id = Guid.NewGuid();
        segment.HabitNodeId = habitNodeId;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.SegmentDefinitions.Add(segment);
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new HabitUpdatedPayload(habitNodeId));   // i segmenti sono del cronometro della schermata aperta
    }

    public async ValueTask RemoveSegmentAsync(Guid segmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var seg = await db.SegmentDefinitions.FirstOrDefaultAsync(s => s.Id == segmentId, ct);
        if (seg is null) return;
        db.SegmentDefinitions.Remove(seg);
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new HabitUpdatedPayload(seg.HabitNodeId));
    }

    public async ValueTask<IReadOnlyList<SegmentDefinition>> ListSegmentsAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.SegmentDefinitions.AsNoTracking()
            .Where(s => s.HabitNodeId == habitNodeId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
    }

    public async ValueTask UpdateSegmentAsync(Guid segmentId, Action<SegmentDefinition> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var seg = await db.SegmentDefinitions.FirstOrDefaultAsync(s => s.Id == segmentId, ct)
            ?? throw new SegmentDefinitionNotFoundException(segmentId);
        mutate(seg);
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new HabitUpdatedPayload(seg.HabitNodeId));
    }

    private static void ValidateCrossFields(HabitConfig c)
    {
        if (c.ScheduleType == ScheduleType.WeeklyX && (c.ScheduleWeeklyTarget is null or <= 0))
            throw new InvalidOperationException("ScheduleWeeklyTarget must be > 0 when ScheduleType is WeeklyX.");
        if (c.ScheduleType == ScheduleType.EveryXDays && (c.ScheduleEveryXDays is null or <= 0))
            throw new InvalidOperationException("ScheduleEveryXDays must be > 0 when ScheduleType is EveryXDays.");
        if (c.TimerMode == Recordum.Core.Enums.TimerMode.Session && !c.SessionBaseTimer)
            throw new InvalidOperationException("SessionBaseTimer must be true when TimerMode is Session.");
    }
}
