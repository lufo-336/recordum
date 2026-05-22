using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.Data.Services.Queries;

public sealed class HourlyDistributionQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock) : IHourlyDistributionQueryService
{
    public async ValueTask<IReadOnlyList<int>> GetHourlyAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var fromMs = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var timestamps = await db.Completions.AsNoTracking()
            .Where(c => c.HabitNodeId == habitNodeId && !c.IsRetroactive && c.CompletedAtMs >= fromMs && c.CompletedAtMs <= toMs)
            .Select(c => c.CompletedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        var buckets = new int[24];
        foreach (var utc in timestamps)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), clock.LocalTimeZone);
            buckets[local.Hour]++;
        }
        return buckets;
    }
}
