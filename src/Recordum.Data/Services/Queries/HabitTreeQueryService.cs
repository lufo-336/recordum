using System.Collections.Immutable;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;

namespace Recordum.Data.Services.Queries;

public sealed class HabitTreeQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock) : IHabitTreeQueryService
{
    public ValueTask<IReadOnlyList<HomeTreeRow>> GetHomeTreeAsync(CancellationToken ct = default)
        => BuildRowsAsync(n => n.DeletedAt == null, ct);

    public ValueTask<IReadOnlyList<HomeTreeRow>> GetTrashTreeAsync(CancellationToken ct = default)
        => BuildRowsAsync(n => n.DeletedAt != null, ct);

    private async ValueTask<IReadOnlyList<HomeTreeRow>> BuildRowsAsync(
        Expression<Func<HabitNode, bool>> predicate,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = clock.UtcNow.UtcDateTime.Date;
        var startMs = new DateTimeOffset(today.AddDays(-6), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(today.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var todayMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var dayBuckets = await db.Completions
            .AsNoTracking()
            .Where(c => c.CompletedAtMs >= startMs && c.CompletedAtMs < endMs)
            .GroupBy(c => new
            {
                c.HabitNodeId,
                DayOffset = (int)((c.CompletedAtMs - startMs) / 86_400_000),
            })
            .Select(g => new { g.Key.HabitNodeId, g.Key.DayOffset, Count = g.Count() })
            .ToListAsync(ct);

        var bucketsByHabit = dayBuckets
            .GroupBy(b => b.HabitNodeId)
            .ToDictionary(g => g.Key, g =>
            {
                var arr = new int[7];
                foreach (var b in g)
                {
                    if (b.DayOffset is >= 0 and < 7) arr[b.DayOffset] = b.Count;
                }
                return ImmutableArray.Create(arr);
            });

        var emptyHeatmap = ImmutableArray.Create(new int[7]);

        var tagLinks = await db.HabitNodeTags
            .AsNoTracking()
            .Select(t => new { t.HabitNodeId, t.TagId })
            .ToListAsync(ct);
        var tagsByHabit = tagLinks
            .GroupBy(t => t.HabitNodeId)
            .ToDictionary(g => g.Key, g => ImmutableArray.CreateRange(g.Select(x => x.TagId)));

        var raw = await db.HabitNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(predicate)
            .Select(n => new
            {
                n.Id,
                n.ParentId,
                n.Name,
                n.Color,
                n.HabitType,
                n.PinScope,
                n.IsArchived,
                LastCompletionAt = db.Completions
                    .Where(c => c.HabitNodeId == n.Id)
                    .OrderByDescending(c => c.CompletedAtMs)
                    .Select(c => (DateTime?)c.CompletedAt)
                    .FirstOrDefault(),
                IsCompletedToday = db.Completions.Any(c => c.HabitNodeId == n.Id && c.CompletedAtMs >= todayMs),
                ChildCount = db.HabitNodes.Count(child => child.ParentId == n.Id && child.DeletedAt == null),
            })
            .ToListAsync(ct);

        return raw.Select(r => new HomeTreeRow(
                HabitNodeId: r.Id,
                ParentId: r.ParentId,
                Name: r.Name,
                ColorHex: r.Color,
                HabitType: r.HabitType,
                LastCompletionAt: r.LastCompletionAt,
                CurrentStreakCount: 0,
                IsCompletedToday: r.IsCompletedToday,
                PinScope: r.PinScope,
                ChildCount: r.ChildCount,
                IsArchived: r.IsArchived,
                CompletionsLast7Days: bucketsByHabit.TryGetValue(r.Id, out var arr) ? arr : emptyHeatmap,
                TagIds: tagsByHabit.TryGetValue(r.Id, out var tids) ? tids : ImmutableArray<Guid>.Empty))
            .ToList();
    }
}
