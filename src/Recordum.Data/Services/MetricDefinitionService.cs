using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed class MetricDefinitionService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<MetricDefinitionService> logger,
    IEventBus eventBus) : IMetricDefinitionService
{
    public async ValueTask<Guid> CreateAsync(MetricDefinition draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        CoerceScaleType(draft);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.MetricDefinitions.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = logger;
        Announce(draft.HabitNodeId);
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<MetricDefinition> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var m = await db.MetricDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return;
        mutate(m);
        CoerceScaleType(m);
        await db.SaveChangesAsync(ct);
        Announce(m.HabitNodeId);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var habitId = await db.MetricDefinitions.Where(x => x.Id == id).Select(x => (Guid?)x.HabitNodeId).FirstOrDefaultAsync(ct);
        await db.MetricDefinitions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        if (habitId is { } h) Announce(h);
    }

    public async ValueTask<IReadOnlyList<MetricDefinition>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.MetricDefinitions.AsNoTracking()
            .Where(m => m.HabitNodeId == habitNodeId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    // GIO-23: una scala si legge in decimale (una scala a decimali non sta in un intero): la metrica con scala e' sempre Decimal.
    internal static void CoerceScaleType(MetricDefinition m)
    {
        if (m.ScaleTemplateId is not null) m.Type = MetricType.Decimal;
    }

    // La forma del quick-add di una schermata aperta (tocco/serie, unita', scala) dipende dalle metriche: chi le scrive lo dice.
    private void Announce(Guid habitNodeId) => eventBus.Publish(new HabitUpdatedPayload(habitNodeId));

}
