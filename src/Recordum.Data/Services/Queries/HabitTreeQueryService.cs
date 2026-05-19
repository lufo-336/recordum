using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.Data.Services.Queries;

public sealed class HabitTreeQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock) : IHabitTreeQueryService
{
    public async ValueTask<IReadOnlyList<HomeTreeRow>> GetHomeTreeAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var todayStartMs = new DateTimeOffset(clock.UtcNow.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var rows = await db.HabitNodes
            .AsNoTracking()
            .Select(n => new HomeTreeRow(
                n.Id,
                n.ParentId,
                n.Name,
                n.Color,
                n.HabitType,
                db.Completions
                    .Where(c => c.HabitNodeId == n.Id)
                    .OrderByDescending(c => c.CompletedAtMs)
                    .Select(c => (DateTime?)c.CompletedAt)
                    .FirstOrDefault(),
                0, // CurrentStreakCount — populated by 3.8 StreakEngine; project 0 here.
                db.Completions.Any(c => c.HabitNodeId == n.Id && c.CompletedAtMs >= todayStartMs)
            ))
            .ToListAsync(ct);

        return rows;
    }
}
