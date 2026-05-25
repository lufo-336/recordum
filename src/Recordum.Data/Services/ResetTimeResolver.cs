using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions.Services;

namespace Recordum.Data.Services;

public sealed partial class ResetTimeResolver(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IAppSettingService settings,
    ILogger<ResetTimeResolver> logger) : IResetTimeResolver
{
    private const int MaxDepth = 32;
    private static readonly TimeOnly DefaultReset = new(0, 0);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Reset-time cascade exceeded depth for {NodeId}; falling back")]
    private partial void LogDepthExceeded(Guid nodeId);

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

        var raw = await settings.GetAsync("daily_reset_time", ct);
        if (raw is not null
            && TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        return DefaultReset;
    }
}
