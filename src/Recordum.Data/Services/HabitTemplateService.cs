using System.Text.Json;
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
                if (payload.Config is null) return;
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
            }, ct);
        }

        return newId;
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
    }
}
