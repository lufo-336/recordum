using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class HabitUiPrefsService(IDbContextFactory<RecordumDbContext> dbFactory)
    : IHabitUiPrefsService
{
    public async ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var json = await db.HabitNodes.AsNoTracking()
            .Where(n => n.Id == habitNodeId)
            .Select(n => n.UiPrefsJson)
            .FirstOrDefaultAsync(ct);
        return HabitUiPrefsJson.Deserialize(json);
    }

    public async ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new InvalidOperationException($"HabitNode {habitNodeId} not found.");
        node.UiPrefsJson = HabitUiPrefsJson.Serialize(prefs);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask UpdateAsync(Guid habitNodeId, Func<HabitUiPrefs, HabitUiPrefs> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new InvalidOperationException($"HabitNode {habitNodeId} not found.");
        // Re-read from the just-fetched row (not a caller-held snapshot) so a concurrent writer's
        // field always survives — the whole point of this method over GetAsync+SetAsync (C2).
        var current = HabitUiPrefsJson.Deserialize(node.UiPrefsJson);
        node.UiPrefsJson = HabitUiPrefsJson.Serialize(mutate(current));
        await db.SaveChangesAsync(ct);
    }
}
