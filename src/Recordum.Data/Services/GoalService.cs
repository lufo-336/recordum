using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class GoalService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<GoalService> logger) : IGoalService
{
    public async ValueTask<Guid> CreateAsync(Goal draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Goals.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Goal> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var g = await db.Goals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return;
        mutate(g);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask CloseAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var g = await db.Goals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return;
        g.IsClosed = true;
        g.ClosedAt = clock.UtcNow.UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Goals.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<IReadOnlyList<Goal>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Goals.AsNoTracking()
            .Where(g => g.HabitNodeId == habitNodeId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);
    }
}
