using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Models;

namespace Recordum.Data.Interceptors;

public sealed partial class HabitConfigHistoryInterceptor(
    IClock clock,
    ILogger<HabitConfigHistoryInterceptor> logger) : SaveChangesInterceptor
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipping history snapshot for HabitConfig {HabitConfigId}: original values unavailable")]
    private partial void LogSnapshotSkipped(Exception exception, Guid habitConfigId);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? db)
    {
        if (db is null) return;
        var entries = db.ChangeTracker.Entries<HabitConfig>()
            .Where(en => en.State == EntityState.Modified || en.State == EntityState.Deleted)
            .ToList();
        if (entries.Count == 0) return;

        var nowUtc = clock.UtcNow.UtcDateTime;
        foreach (var entry in entries)
        {
            if (!HasVersionedChange(entry)) continue;
            try
            {
                var snap = BuildSnapshot(entry, nowUtc);
                db.Set<HabitConfigHistory>().Add(snap);
            }
            catch (InvalidOperationException ex)
            {
                LogSnapshotSkipped(ex, entry.Entity.Id);
            }
        }
    }

    private static bool HasVersionedChange(EntityEntry<HabitConfig> entry)
    {
        if (entry.State == EntityState.Deleted) return true;
        foreach (var name in HabitConfigHistoryColumns.Versioned)
        {
            var p = entry.Property(name);
            if (!Equals(p.OriginalValue, p.CurrentValue)) return true;
        }
        return false;
    }

    private static HabitConfigHistory BuildSnapshot(EntityEntry<HabitConfig> entry, DateTime nowUtc)
    {
        var original = entry.OriginalValues;
        var snap = new HabitConfigHistory
        {
            Id = Guid.NewGuid(),
            HabitNodeId = entry.Entity.HabitNodeId,
            ValidFrom = (DateTime)original[nameof(HabitConfig.UpdatedAt)]!,
            ValidUntil = nowUtc,
        };
        foreach (var name in HabitConfigHistoryColumns.Versioned)
        {
            var src = original[name];
            typeof(HabitConfigHistory).GetProperty(name)!.SetValue(snap, src);
        }
        return snap;
    }
}
