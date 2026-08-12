using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class HabitTemplateService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    IHabitNodeService nodes,
    IHabitConfigService configs,
    IHabitUiPrefsService uiPrefs,
    ILogger<HabitTemplateService> logger) : IHabitTemplateService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.HabitTemplates.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = pluginContext; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.HabitTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        mutate(t);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.HabitTemplates.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.HabitTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.HabitTemplates.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    }

    public async ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default)
    {
        HabitTemplate? tmpl;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            tmpl = await db.HabitTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        }
        if (tmpl is null) throw new HabitTemplateNotFoundException(templateId);

        var payload = ParsePayload(tmpl.PayloadJson);
        var node = new HabitNode
        {
            ParentId = parentId,
            NodeType = ParseEnum(payload.Node?.NodeType, NodeType.Habit),
            HabitType = payload.Node?.HabitType is { } ht
                ? ParseEnum<HabitType>(ht, HabitType.Action)
                : null,
            Name = payload.Node?.Name ?? tmpl.Name,
            Description = tmpl.Description,
            Color = string.IsNullOrWhiteSpace(payload.Node?.Color) ? "#808080" : payload.Node!.Color!,
            Icon = payload.Node?.Icon,
        };
        var newId = await nodes.CreateAsync(node, ct);

        if (node.NodeType == NodeType.Habit)
        {
            await configs.UpsertAsync(newId, c =>
            {
                // I5: templates saved by the builder before Task 3 (wave 2.4) wrote "repeatable" at
                // the payload ROOT and had no "config" object at all — the only shape the builder
                // produced between U8 and that fix. Read it here, before the payload.Config null-guard
                // below would otherwise skip this branch entirely and silently drop the flag on
                // instantiation. The writer no longer produces this shape; this is a compatibility
                // read only, kept because the flag can still live in an already-saved local template.
                if (payload.Config is null)
                {
                    c.Repeatable = payload.RepeatableLegacy ?? false;
                    return;
                }
                c.ScheduleType = ParseEnum(payload.Config.ScheduleType, ScheduleType.Daily);
                c.ScheduleDaysJson = payload.Config.ScheduleDaysJson;
                c.ScheduleWeeklyTarget = payload.Config.ScheduleWeeklyTarget;
                c.ScheduleEveryXDays = payload.Config.ScheduleEveryXDays;
                c.StreakMode = ParseEnum(payload.Config.StreakMode, StreakMode.Daily);
                c.StreakRecovery = ParseEnum(payload.Config.StreakRecovery, StreakRecovery.None);
                c.HasIntegratedTimer = payload.Config.HasIntegratedTimer ?? false;
                c.TimerMode = payload.Config.TimerMode is { } tm
                    ? ParseEnum<TimerMode>(tm, TimerMode.Stopwatch)
                    : null;
                c.TimerTargetSeconds = payload.Config.TimerTargetSeconds;
                if (c.TimerMode == Recordum.Core.Enums.TimerMode.Session) c.SessionBaseTimer = true;
                c.Repeatable = payload.Config.Repeatable ?? payload.RepeatableLegacy ?? false;
                c.SeriesLabelsJson = payload.Config.SeriesLabels is { Count: > 0 }
                    ? JsonSerializer.Serialize(payload.Config.SeriesLabels)
                    : null;
            }, ct);

            await MaterializeMetricsSegmentsGoalAsync(newId, node.Name, payload, ct);
        }

        // Presentazione per-nodo: si scrive solo se il template la dichiara, così un template
        // che non dice niente lascia ui_prefs_json NULL = "default del tipo" (governance §6).
        if (payload.UiPrefs?.JournalGesture is { } gestureText
            && Enum.TryParse<JournalPrimaryGesture>(gestureText, ignoreCase: true, out var gesture))
        {
            var prefs = await uiPrefs.GetAsync(newId, ct);
            await uiPrefs.SetAsync(newId, prefs with { JournalGesture = gesture }, ct);
        }

        return newId;
    }

    // Materializes the rich parts of a template payload that node + config alone drop: metrics (with
    // unit / scale resolved by code, and the scale's labels snapshotted onto the metric so the State
    // quick-add shows them), segments, and the goal. Single DbContext + transaction so the whole
    // instantiation tail is one connection open instead of N+3 (CR7) — same pattern as
    // ComposedHabitService; the timestamp/provenance interceptor still fires on SaveChanges.
    private async Task MaterializeMetricsSegmentsGoalAsync(Guid habitId, string habitName, TemplatePayload payload, CancellationToken ct)
    {
        if (payload.Metrics is not { Count: > 0 } && payload.Segments is not { Count: > 0 } && payload.Goal is null)
            return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var unitsByCode = await db.UnitsOfMeasure.AsNoTracking()
            .ToDictionaryAsync(u => u.Code, u => u.Id, StringComparer.OrdinalIgnoreCase, ct);
        var scalesByCode = await db.ScaleTemplates.AsNoTracking()
            .ToDictionaryAsync(s => s.Code, s => (Id: s.Id, Labels: (string?)s.LabelsJson), StringComparer.OrdinalIgnoreCase, ct);

        // Risolvi-o-crea (Onda 2.5 T8): un template composto nel builder può portarsi dietro le unità
        // e le scale che ha inventato. Un codice già nel catalogo vince sempre sulla definizione
        // incorporata — così ristanziare lo stesso template non moltiplica "tazze, tazze-2, tazze-3"
        // e non ridefinisce un'unità ufficiale. Dentro la stessa transazione delle metriche.
        foreach (var u in payload.Units ?? [])
        {
            if (string.IsNullOrWhiteSpace(u.Code) || unitsByCode.ContainsKey(u.Code)) continue;
            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Code = u.Code,
                Name = string.IsNullOrWhiteSpace(u.Name) ? u.Code : u.Name,
                Symbol = u.Symbol,
                IsOfficial = false,
            };
            db.UnitsOfMeasure.Add(unit);
            unitsByCode[unit.Code] = unit.Id;
        }
        foreach (var s in payload.Scales ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Code) || scalesByCode.ContainsKey(s.Code)) continue;
            var labels = JsonSerializer.Serialize(s.Labels ?? []);
            var scale = new ScaleTemplate
            {
                Id = Guid.NewGuid(),
                Code = s.Code,
                Name = string.IsNullOrWhiteSpace(s.Name) ? s.Code : s.Name,
                MinValue = s.MinValue,
                MaxValue = s.MaxValue,
                LabelsJson = labels,
                IsOfficial = false,
            };
            db.ScaleTemplates.Add(scale);
            scalesByCode[scale.Code] = (scale.Id, labels);
        }

        Guid? firstMetricId = null;
        if (payload.Metrics is { Count: > 0 })
        {
            for (var i = 0; i < payload.Metrics.Count; i++)
            {
                var m = payload.Metrics[i];
                if (string.IsNullOrWhiteSpace(m.Name)) continue;

                Guid? scaleId = null;
                string? labels = null;
                if (m.ScaleCode is { } sc && scalesByCode.TryGetValue(sc, out var scale))
                {
                    scaleId = scale.Id;
                    labels = scale.Labels;
                }
                Guid? unitId = m.Unit is { } uc && unitsByCode.TryGetValue(uc, out var u) ? u : null;

                var metric = new MetricDefinition
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = habitId,
                    Name = m.Name!,
                    Type = ParseEnum(m.Type, MetricType.Integer),
                    SortOrder = i,
                    UnitOfMeasureId = unitId,
                    ScaleTemplateId = scaleId,
                    LabelsJson = labels,
                };
                db.MetricDefinitions.Add(metric);
                firstMetricId ??= metric.Id;
            }
        }

        if (payload.Segments is { Count: > 0 })
        {
            foreach (var s in payload.Segments)
            {
                if (string.IsNullOrWhiteSpace(s.Name)) continue;
                db.SegmentDefinitions.Add(new SegmentDefinition
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = habitId,
                    Name = s.Name!,
                    IsRest = s.IsRest,
                    SortOrder = s.SortOrder,
                    Color = s.Color,
                });
            }
        }

        if (payload.Goal is { } g)
        {
            db.Goals.Add(new Goal
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                Name = habitName, // the goal belongs to the habit; never a hardcoded literal (CR9)
                MetricDefinitionId = firstMetricId,
                TargetValue = g.Target,
                PeriodType = ParseEnum(g.PeriodType, GoalPeriodType.Open),
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static TemplatePayload ParsePayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new TemplatePayload();
        try
        {
            return JsonSerializer.Deserialize<TemplatePayload>(json, s_jsonOptions) ?? new TemplatePayload();
        }
        catch (JsonException)
        {
            return new TemplatePayload();
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private sealed class TemplatePayload
    {
        public NodePayload? Node { get; set; }
        public ConfigPayload? Config { get; set; }
        public UiPrefsPayload? UiPrefs { get; set; }
        public List<MetricPayload>? Metrics { get; set; }
        public List<SegmentPayload>? Segments { get; set; }
        public GoalPayload? Goal { get; set; }

        /// <summary>Unità e scale che il template porta con sé (Onda 2.5 T8) perché il builder le ha
        /// composte insieme all'abitudine. Si risolvono per Code: se il codice esiste già si usa
        /// quello del catalogo, altrimenti si crea. Assenti = comportamento storico.</summary>
        public List<UnitDefPayload>? Units { get; set; }
        public List<ScaleDefPayload>? Scales { get; set; }

        /// <summary>Compatibility read only (I5): pre-Task-3 builder templates wrote "repeatable" at
        /// the payload root instead of nested under "config". The writer no longer produces this
        /// shape — see the comment at its read site in <see cref="HabitTemplateService.InstantiateAsync"/>.</summary>
        [JsonPropertyName("repeatable")]
        public bool? RepeatableLegacy { get; set; }
    }

    private sealed class UiPrefsPayload
    {
        /// <summary>"Diary" | "QuickNote". Chiave snake_case, valore PascalCase, come gli altri enum
        /// del payload. Null = il nodo resta senza prefs e usa il default del tipo.</summary>
        public string? JournalGesture { get; set; }
    }

    private sealed class MetricPayload
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Unit { get; set; }
        public string? ScaleCode { get; set; }
    }

    private sealed class SegmentPayload
    {
        public string? Name { get; set; }
        public bool IsRest { get; set; }
        public int SortOrder { get; set; }
        public string? Color { get; set; }
    }

    private sealed class UnitDefPayload
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Symbol { get; set; }
    }

    private sealed class ScaleDefPayload
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public List<string>? Labels { get; set; }
    }

    private sealed class GoalPayload
    {
        public decimal Target { get; set; }
        public string? Unit { get; set; }
        public string? PeriodType { get; set; }
    }

    private sealed class NodePayload
    {
        public string? Name { get; set; }
        public string? NodeType { get; set; }
        public string? HabitType { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
    }

    private sealed class ConfigPayload
    {
        public string? ScheduleType { get; set; }
        public string? ScheduleDaysJson { get; set; }
        public int? ScheduleWeeklyTarget { get; set; }
        public int? ScheduleEveryXDays { get; set; }
        public string? StreakMode { get; set; }
        public string? StreakRecovery { get; set; }
        public bool? HasIntegratedTimer { get; set; }
        public string? TimerMode { get; set; }
        public int? TimerTargetSeconds { get; set; }
        public bool? Repeatable { get; set; }
        public List<string>? SeriesLabels { get; set; }
    }
}
