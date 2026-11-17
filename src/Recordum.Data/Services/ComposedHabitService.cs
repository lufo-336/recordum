using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class ComposedHabitService(IDbContextFactory<RecordumDbContext> dbFactory) : IComposedHabitService
{
    public async ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Staged units/scales arrive with pre-assigned Guids and are added before the metrics
        // that reference them by Id (no code-based resolution on this path — that lives only
        // in HabitTemplateService.InstantiateAsync).
        foreach (var u in request.NewUnits ?? []) db.UnitsOfMeasure.Add(u);
        foreach (var s in request.NewScales ?? [])
        {
            // Gli scaglioni arrivano con il nome e senza tag: li costruisce ScaleAnchorTags (trova-o-crea i tag).
            var drafts = s.Anchors.Select(a => new ScaleAnchorDraft(a.Value, a.Label)).ToList();
            s.Anchors.Clear();
            db.ScaleTemplates.Add(s);
            await ScaleAnchorTags.BuildAsync(db, s.Id, drafts, ct);
        }
        // ui_prefs is serialized onto the node before Add, inside this transaction — no post-hoc
        // write (closes W24-R1).
        if (request.UiPrefs is { } prefs) request.Node.UiPrefsJson = HabitUiPrefsJson.Serialize(prefs);
        db.HabitNodes.Add(request.Node);
        if (request.Config is not null) db.HabitConfigs.Add(request.Config);
        foreach (var m in request.Metrics) db.MetricDefinitions.Add(m);
        foreach (var seg in request.Segments ?? []) db.SegmentDefinitions.Add(seg);
        if (request.Goal is not null) db.Goals.Add(request.Goal);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return request.Node.Id;
    }
}
