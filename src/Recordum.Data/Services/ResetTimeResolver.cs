using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Time;

namespace Recordum.Data.Services;

public sealed partial class ResetTimeResolver(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IAppSettingService settings,
    IClock clock,
    ILogger<ResetTimeResolver> logger) : IResetTimeResolver
{
    private const int MaxDepth = 32;
    private static readonly TimeOnly DefaultReset = new(0, 0);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Reset-time cascade exceeded depth for {NodeId}; falling back")]
    private partial void LogDepthExceeded(Guid nodeId);

    private readonly record struct NodeCascadeRow(Guid Id, Guid? ParentId, TimeOnly? ResetTime);

    public async ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var current = await db.HabitNodes.AsNoTracking()
            .Where(n => n.Id == habitNodeId)
            .Select(n => new { n.Id, n.ParentId, n.ResetTime })
            .FirstOrDefaultAsync(ct);

        var seen = new HashSet<Guid>();
        var depth = 0;
        while (current is not null)
        {
            if (current.ResetTime is { } t) return t;
            if (!seen.Add(current.Id) || ++depth > MaxDepth)
            {
                LogDepthExceeded(habitNodeId);
                break;
            }
            if (current.ParentId is not { } pid) break;
            current = await db.HabitNodes.AsNoTracking()
                .Where(n => n.Id == pid)
                .Select(n => new { n.Id, n.ParentId, n.ResetTime })
                .FirstOrDefaultAsync(ct);
        }

        return await GetAppResetAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default)
        => new(clock.LocalTimeZone, await GetEffectiveResetTimeAsync(habitNodeId, ct).ConfigureAwait(false));

    public async ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default)
        => new(clock.LocalTimeZone, await GetAppResetAsync(ct).ConfigureAwait(false));

    public async ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(
        IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(habitNodeIds);
        var appReset = await GetAppResetAsync(ct).ConfigureAwait(false);
        var tz = clock.LocalTimeZone;
        if (habitNodeIds.Count == 0) return new Dictionary<Guid, DayBoundary>();

        // Una lettura sola dell'albero: i nodi sono centinaia, non milioni, e la cascata è una
        // passeggiata in memoria. IgnoreQueryFilters perché l'albero Cestino chiede i confini
        // dei nodi soft-deleted.
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var nodes = await db.HabitNodes.AsNoTracking().IgnoreQueryFilters()
            .Select(n => new NodeCascadeRow(n.Id, n.ParentId, n.ResetTime))
            .ToDictionaryAsync(n => n.Id, ct).ConfigureAwait(false);

        var result = new Dictionary<Guid, DayBoundary>(habitNodeIds.Count);
        foreach (var id in habitNodeIds)
        {
            if (result.ContainsKey(id)) continue;
            result[id] = new DayBoundary(tz, WalkCascade(id, nodes, appReset, () => LogDepthExceeded(id)));
        }
        return result;
    }

    private static TimeOnly WalkCascade(
        Guid startId,
        IReadOnlyDictionary<Guid, NodeCascadeRow> nodes,
        TimeOnly appReset,
        Action logDepth)
    {
        var seen = new HashSet<Guid>();
        var depth = 0;
        var currentId = (Guid?)startId;
        while (currentId is { } id && nodes.TryGetValue(id, out var node))
        {
            if (node.ResetTime is { } t) return t;
            if (!seen.Add(id) || ++depth > MaxDepth) { logDepth(); break; }
            currentId = node.ParentId;
        }
        return appReset;
    }

    private async ValueTask<TimeOnly> GetAppResetAsync(CancellationToken ct)
    {
        var raw = await settings.GetAsync("daily_reset_time", ct).ConfigureAwait(false);
        return raw is not null
            && TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DefaultReset;
    }
}
