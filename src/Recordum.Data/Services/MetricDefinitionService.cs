using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class MetricDefinitionService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<MetricDefinitionService> logger) : IMetricDefinitionService
{
    public async ValueTask<Guid> CreateAsync(MetricDefinition draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ValidateLabels(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.MetricDefinitions.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<MetricDefinition> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var m = await db.MetricDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return;
        mutate(m);
        ValidateLabels(m);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.MetricDefinitions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<IReadOnlyList<MetricDefinition>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.MetricDefinitions.AsNoTracking()
            .Where(m => m.HabitNodeId == habitNodeId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    private static void ValidateLabels(MetricDefinition m)
    {
        if (m.LabelsJson is null) return;
        using var doc = JsonDocument.Parse(m.LabelsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("LabelsJson must be a JSON array.");
    }
}
