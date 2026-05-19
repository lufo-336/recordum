using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Errors;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed partial class HabitNodeService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    ILogger<HabitNodeService> logger) : IHabitNodeService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "HabitNode {Id} created (plugin={PluginId})")]
    private partial void LogCreated(Guid id, string? pluginId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "HabitNode {Id} updated")]
    private partial void LogUpdated(Guid id);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "HabitNode {Id} moved to {NewParent}")]
    private partial void LogMoved(Guid id, Guid? newParent);

    public async ValueTask<Guid> CreateAsync(HabitNode draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.HabitNodes.Add(draft);
        await db.SaveChangesAsync(ct);
        LogCreated(draft.Id, pluginContext.PluginId);
        _ = clock;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<HabitNode> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new HabitNodeNotFoundException(id);
        if (node.DeletedAt is not null) throw new HabitNodeAlreadySoftDeletedException(id);
        mutate(node);
        await db.SaveChangesAsync(ct);
        LogUpdated(id);
    }

    public async ValueTask MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new HabitNodeNotFoundException(id);
        if (node.DeletedAt is not null) throw new HabitNodeAlreadySoftDeletedException(id);
        if (newParentId is { } np)
        {
            var descendantIds = await DescendantIdsAsync(db, id, ct);
            if (np == id || descendantIds.Contains(np))
                throw new CyclicHabitNodeMoveException(id, np);
            var parentExists = await db.HabitNodes.AnyAsync(n => n.Id == np, ct);
            if (!parentExists) throw new HabitNodeNotFoundException(np);
        }
        node.ParentId = newParentId;
        await db.SaveChangesAsync(ct);
        LogMoved(id, newParentId);
    }

    public async ValueTask<HabitNode?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.HabitNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public ValueTask SoftDeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException("Task 10");

    public ValueTask RestoreAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException("Task 10");

    internal static async Task<HashSet<Guid>> DescendantIdsAsync(
        RecordumDbContext db, Guid rootId, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE d(id) AS (
                SELECT id FROM habit_nodes WHERE id = $root
                UNION ALL
                SELECT n.id FROM habit_nodes n JOIN d ON n.parent_id = d.id
            )
            SELECT id FROM d WHERE id <> $root;
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "$root";
        p.Value = rootId;
        cmd.Parameters.Add(p);
        var ids = new HashSet<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(Guid.Parse(reader.GetString(0)));
        return ids;
    }
}
