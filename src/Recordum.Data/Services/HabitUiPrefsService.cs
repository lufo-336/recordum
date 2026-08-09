using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class HabitUiPrefsService(IDbContextFactory<RecordumDbContext> dbFactory)
    : IHabitUiPrefsService
{
    // String enums + omit nulls keep the stored JSON small and human-inspectable (governance).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // camelCase is the documented on-disk contract (docs/ARCHITECTURE.md); one shared options
        // instance means the same policy governs both write and read, so round-trip stays consistent.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var json = await db.HabitNodes.AsNoTracking()
            .Where(n => n.Id == habitNodeId)
            .Select(n => n.UiPrefsJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return new HabitUiPrefs();
        return JsonSerializer.Deserialize<HabitUiPrefs>(json, JsonOptions) ?? new HabitUiPrefs();
    }

    public async ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new InvalidOperationException($"HabitNode {habitNodeId} not found.");
        node.UiPrefsJson = JsonSerializer.Serialize(prefs, JsonOptions);
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
        var current = string.IsNullOrWhiteSpace(node.UiPrefsJson)
            ? new HabitUiPrefs()
            : JsonSerializer.Deserialize<HabitUiPrefs>(node.UiPrefsJson, JsonOptions) ?? new HabitUiPrefs();
        node.UiPrefsJson = JsonSerializer.Serialize(mutate(current), JsonOptions);
        await db.SaveChangesAsync(ct);
    }
}
