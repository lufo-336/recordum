using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
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

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "HabitNode {Id} soft-deleted (subtree size {Count})")]
    private partial void LogSoftDeleted(Guid id, int count);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "HabitNode {Id} restored")]
    private partial void LogRestored(Guid id);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "HabitNode {Id} pin scope set to {Scope}")]
    private partial void LogPinScopeChanged(Guid id, PinScope scope);

    public async ValueTask<Guid> CreateAsync(HabitNode draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.HabitNodes.Add(draft);
        await db.SaveChangesAsync(ct);
        LogCreated(draft.Id, pluginContext.PluginId);
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

    public async ValueTask SetPinScopeAsync(Guid id, PinScope scope, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new HabitNodeNotFoundException(id);
        if (node.DeletedAt is not null) throw new HabitNodeAlreadySoftDeletedException(id);
        if (node.PinScope == scope) return;
        node.PinScope = scope;
        await db.SaveChangesAsync(ct);
        LogPinScopeChanged(id, scope);
    }

    public async ValueTask SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new HabitNodeNotFoundException(id);
        if (node.DeletedAt is not null) return;
        var deletedAt = clock.UtcNow.UtcDateTime;
        var descendants = await DescendantIdsAsync(db, id, ct);
        descendants.Add(id);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.HabitNodes.IgnoreQueryFilters()
            .Where(n => descendants.Contains(n.Id) && n.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.DeletedAt, deletedAt), ct);
        await tx.CommitAsync(ct);
        LogSoftDeleted(id, descendants.Count);
    }

    public async ValueTask RestoreAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new HabitNodeNotFoundException(id);
        if (node.DeletedAt is null) return;
        if (node.ParentId is { } parentId)
        {
            var parent = await db.HabitNodes.IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == parentId, ct);
            if (parent is not null && parent.DeletedAt is not null)
                throw new CannotRestoreOrphanException(id, parentId);
        }
        var tagDeletedAt = node.DeletedAt;
        var descendants = await DescendantIdsAsync(db, id, ct);
        descendants.Add(id);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.HabitNodes.IgnoreQueryFilters()
            .Where(n => descendants.Contains(n.Id) && n.DeletedAt == tagDeletedAt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => (DateTime?)n.DeletedAt, (DateTime?)null), ct);
        await tx.CommitAsync(ct);
        LogRestored(id);
    }

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
