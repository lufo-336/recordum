using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.Data.Services;

public sealed partial class PurgeService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<PurgeService> logger) : IPurgeService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Purged subtree rooted at {Root} ({Size} nodes)")]
    private partial void LogPurged(Guid root, int size);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Purge failed for {Root}")]
    private partial void LogPurgeFailed(Exception exception, Guid root);

    public async ValueTask<PurgeReport> PurgeExpiredAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow.UtcDateTime - olderThan;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rootIds = await db.HabitNodes.IgnoreQueryFilters()
            .Where(n => n.DeletedAt != null && n.DeletedAt < cutoff && n.ParentId == null)
            .Select(n => n.Id)
            .ToListAsync(ct);

        var errors = new List<PurgeError>();
        int purged = 0, skipped = 0;

        foreach (var root in rootIds)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var subtree = await HabitNodeService.DescendantIdsAsync(db, root, ct);
                subtree.Add(root);

                await db.Attachments
                    .Where(a => a.NoteId != null && db.Notes.Any(n => n.Id == a.NoteId && subtree.Contains(n.HabitNodeId)))
                    .ExecuteDeleteAsync(ct);
                await db.Attachments
                    .Where(a => a.CompletionId != null && db.Completions.Any(c => c.Id == a.CompletionId && subtree.Contains(c.HabitNodeId)))
                    .ExecuteDeleteAsync(ct);
                await db.Notes.Where(n => subtree.Contains(n.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.CompletionValues
                    .Where(v => db.Completions.Any(c => c.Id == v.CompletionId && subtree.Contains(c.HabitNodeId)))
                    .ExecuteDeleteAsync(ct);
                await db.CompletionSegments
                    .Where(s => db.Completions.Any(c => c.Id == s.CompletionId && subtree.Contains(c.HabitNodeId)))
                    .ExecuteDeleteAsync(ct);
                await db.Completions.Where(c => subtree.Contains(c.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.Goals.Where(g => subtree.Contains(g.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.MetricDefinitions.Where(m => subtree.Contains(m.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.Notifications.Where(n => subtree.Contains(n.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.StreakPauses.Where(p => subtree.Contains(p.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.HabitNodeTags.Where(t => subtree.Contains(t.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.HabitConfigHistories.Where(h => subtree.Contains(h.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.HabitConfigs.Where(c => subtree.Contains(c.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.SegmentDefinitions.Where(s => subtree.Contains(s.HabitNodeId)).ExecuteDeleteAsync(ct);
                await db.HabitNodes.IgnoreQueryFilters()
                    .Where(n => subtree.Contains(n.Id))
                    .ExecuteDeleteAsync(ct);

                await tx.CommitAsync(ct);
                purged++;
                LogPurged(root, subtree.Count);
            }
#pragma warning disable CA1031 // catch broad — record + continue is the documented behaviour
            catch (Exception ex)
#pragma warning restore CA1031
            {
                skipped++;
                errors.Add(new PurgeError(root, "delete", ex.Message));
                LogPurgeFailed(ex, root);
            }
        }
        return new PurgeReport(purged, skipped, errors);
    }
}
