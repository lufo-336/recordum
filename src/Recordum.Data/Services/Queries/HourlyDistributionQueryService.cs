using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Time;

namespace Recordum.Data.Services.Queries;

public sealed class HourlyDistributionQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IResetTimeResolver resetTimes) : IHourlyDistributionQueryService
{
    public async ValueTask<IReadOnlyList<int>> GetHourlyAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
        var (from, toExclusive) = day.RangeOf(fromDate, toDate);
        var fromMs = from.ToUnixTimeMilliseconds();
        var toMs = toExclusive.ToUnixTimeMilliseconds();

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var timestamps = await db.Completions.AsNoTracking()
            .Where(c => c.HabitNodeId == habitNodeId && !c.IsRetroactive && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
            .Select(c => c.CompletedAtMs)
            .ToListAsync(ct).ConfigureAwait(false);

        // Il bucket resta l'ora locale REALE dell'orologio, non spostata dal reset_time: "di solito
        // verso le 20:00" parla dell'orologio da parete, non del giorno di appartenenza. Il confine
        // decide quali registrazioni entrano nella finestra (sopra), non in quale ora cadono.
        var buckets = new int[24];
        foreach (var ms in timestamps)
        {
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(ms), day.TimeZone);
            buckets[local.Hour]++;
        }
        return buckets;
    }
}
