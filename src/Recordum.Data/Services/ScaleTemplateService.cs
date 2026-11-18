using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Formatting;
using Recordum.Core.Models;
using Recordum.Core.Scales;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed class ScaleTemplateService(IDbContextFactory<RecordumDbContext> dbFactory, IEventBus eventBus) : IScaleTemplateService
{
    private const int MaxLabel = 100;

    public async ValueTask<Guid> CreateAsync(ScaleDraft draft, CancellationToken ct = default)
    {
        Validate(draft);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = await InsertAsync(db, draft, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public async ValueTask UpdateAsync(Guid id, ScaleDraft draft, CancellationToken ct = default)
    {
        Validate(draft);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var scale = await db.ScaleTemplates.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new InvalidOperationException("scale-missing");
        if (scale.IsOfficial) throw new InvalidOperationException("official");

        var usage = await UsageAsync(db, id, ct);
        ThrowIfRecordedOutside(draft.MinValue, draft.MaxValue, draft.Precision, usage.RecordedMin, usage.RecordedMax, usage.RecordedHasDecimals);

        scale.Name = draft.Name.Trim();
        scale.MinValue = draft.MinValue;
        scale.MaxValue = draft.MaxValue;
        scale.Precision = draft.Precision;
        // Prima via i vecchi scaglioni, poi i nuovi: l'indice (scala, valore) è unico.
        await db.ScaleAnchors.Where(a => a.ScaleTemplateId == id).ExecuteDeleteAsync(ct);
        await ScaleAnchorTags.BuildAsync(db, id, draft.Anchors, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // Le schermate aperte che leggono questa scala ricostruiscono il quick-add: lo si dice a ogni abitudine che la usa.
        foreach (var habitId in await db.MetricDefinitions.Where(m => m.ScaleTemplateId == id).Select(m => m.HabitNodeId).Distinct().ToListAsync(ct))
            eventBus.Publish(new HabitUpdatedPayload(habitId));
    }

    public async ValueTask<Guid> DuplicateAsync(Guid sourceId, string name, IReadOnlyList<ScaleAnchorDraft> anchors,
        Guid? repointMetricId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var src = await db.ScaleTemplates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sourceId, ct)
            ?? throw new InvalidOperationException("scale-missing");
        var draft = new ScaleDraft(name, src.MinValue, src.MaxValue, src.Precision, anchors);
        Validate(draft);
        var id = await InsertAsync(db, draft, ct);
        if (repointMetricId is { } mid)
            await db.MetricDefinitions.Where(m => m.Id == mid).ExecuteUpdateAsync(u => u.SetProperty(m => m.ScaleTemplateId, id), ct);
        await tx.CommitAsync(ct);
        // La metrica ripuntata cambia scala: la schermata aperta di quell'abitudine ricostruisce il quick-add.
        if (repointMetricId is { } m && await db.MetricDefinitions.Where(x => x.Id == m).Select(x => (Guid?)x.HabitNodeId).FirstOrDefaultAsync(ct) is { } habitId)
            eventBus.Publish(new HabitUpdatedPayload(habitId));
        return id;
    }

    public async ValueTask<ScaleUsage> GetUsageAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await UsageAsync(db, id, ct);
    }

    /// <summary>Rifiuta una scala che non contiene i valori registrati: il messaggio e' il codice di
    /// <see cref="ScaleRules.EditErrors"/>, e <c>Data["recorded"]</c> porta il valore che sborda (per il testo a schermo).</summary>
    internal static void ThrowIfRecordedOutside(int min, int max, ScalePrecision precision,
        decimal? recordedMin, decimal? recordedMax, bool recordedHasDecimals)
    {
        var edit = ScaleRules.EditErrors(min, max, precision, recordedMin, recordedMax, recordedHasDecimals);
        if (edit.Count == 0) return;
        var ex = new InvalidOperationException(edit[0]);
        ex.Data["recorded"] = edit[0] == "narrow-min" ? recordedMin : recordedMax;
        throw ex;
    }

    /// <summary>I valori registrati: il minimo, il massimo, e se qualcuno ha i decimali (null = nessun valore).</summary>
    internal static (decimal Min, decimal Max, bool HasDecimals)? Recorded(IReadOnlyCollection<decimal> values) =>
        values.Count == 0 ? null : (values.Min(), values.Max(), values.Any(v => v != decimal.Truncate(v)));

    private static void Validate(ScaleDraft d)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (string.IsNullOrWhiteSpace(d.Name)) throw new InvalidOperationException("name");
        if (d.Anchors.Any(a => a.Label is { } l && l.Trim().Length > MaxLabel)) throw new InvalidOperationException("label-length");
        var errors = ScaleRules.ShapeErrors(d.MinValue, d.MaxValue, d.Anchors.Select(a => a.Value).ToList());
        if (errors.Count > 0) throw new InvalidOperationException(errors[0]);
    }

    private static async Task<Guid> InsertAsync(RecordumDbContext db, ScaleDraft d, CancellationToken ct)
    {
        var codes = await db.ScaleTemplates.Select(s => s.Code).ToListAsync(ct);
        var scale = new ScaleTemplate
        {
            Id = Guid.NewGuid(),
            Code = ReferenceCodeSlug.From(d.Name, codes),
            Name = d.Name.Trim(),
            MinValue = d.MinValue,
            MaxValue = d.MaxValue,
            Precision = d.Precision,
            IsOfficial = false,
        };
        db.ScaleTemplates.Add(scale);
        await ScaleAnchorTags.BuildAsync(db, scale.Id, d.Anchors, ct);
        await db.SaveChangesAsync(ct);
        return scale.Id;
    }

    private static async Task<ScaleUsage> UsageAsync(RecordumDbContext db, Guid id, CancellationToken ct)
    {
        // Le abitudini nel cestino non contano fra chi la usa; i loro valori si', se ripristinate devono starci.
        var habits = await db.MetricDefinitions.Where(m => m.ScaleTemplateId == id && m.HabitNode.DeletedAt == null)
            .Select(m => m.HabitNodeId).Distinct().CountAsync(ct);
        var values = await db.CompletionValues
            .Where(cv => cv.MetricDefinition.ScaleTemplateId == id && cv.ValueDecimal != null)
            .Select(cv => cv.ValueDecimal!.Value).ToListAsync(ct);
        return Recorded(values) is { } r
            ? new ScaleUsage(habits, r.Min, r.Max, r.HasDecimals)
            : new ScaleUsage(habits, null, null, false);
    }
}
