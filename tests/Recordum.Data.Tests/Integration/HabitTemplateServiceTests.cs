using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitTemplateServiceTests
{
    [Fact]
    public async Task Create_records_plugin_provenance()
    {
        await using var fx = new DbFixture();
        var svc = CreateSut(fx);
        Guid id;
        using (fx.PluginContext.BeginScope("plug"))
            id = await svc.CreateAsync(new HabitTemplate { Name = "t" });
        await using var db = await fx.Factory.CreateDbContextAsync();
        var t = await db.HabitTemplates.SingleAsync(x => x.Id == id);
        Assert.Equal("plug", t.CreatedByPluginId);
    }

    [Fact]
    public async Task Delete_removes_template()
    {
        await using var fx = new DbFixture();
        var svc = CreateSut(fx);
        var id = await svc.CreateAsync(new HabitTemplate { Name = "t" });
        await svc.DeleteAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.False(await db.HabitTemplates.AnyAsync(t => t.Id == id));
    }

    [Fact]
    public async Task InstantiateAsync_clones_seed_template_into_new_habit_node_and_config()
    {
        await using var fx = new DbFixture();
        // Seed data is not applied automatically in DbFixture (EnsureCreated skips HasData on in-memory).
        // Insert a seed-shaped template directly.
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId,
                Name = "Morning routine",
                Description = "desc",
                PayloadJson = "{\"node\":{\"name\":\"Morning routine\",\"node_type\":\"Habit\",\"habit_type\":\"Action\",\"color\":\"#f5a623\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}}",
                IsOfficial = true,
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var node = await db2.HabitNodes.FindAsync(newId);
        node.Should().NotBeNull();
        node!.Name.Should().Be("Morning routine");
        node.NodeType.Should().Be(NodeType.Habit);
        node.HabitType.Should().Be(HabitType.Action);
        node.Color.Should().Be("#f5a623");
        node.ParentId.Should().BeNull();

        var cfg = await db2.HabitConfigs.SingleAsync(c => c.HabitNodeId == newId);
        cfg.ScheduleType.Should().Be(ScheduleType.Daily);
        cfg.StreakMode.Should().Be(StreakMode.Daily);
        cfg.StreakRecovery.Should().Be(StreakRecovery.GraceDay);
    }

    [Fact]
    public async Task InstantiateAsync_with_parent_attaches_under_group()
    {
        await using var fx = new DbFixture();
        var groupId = Guid.NewGuid();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = groupId, NodeType = NodeType.Group, Name = "G" });
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId,
                Name = "Daily journal",
                PayloadJson = "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}}",
                IsOfficial = true,
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: groupId);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.HabitNodes.FindAsync(newId))!.ParentId.Should().Be(groupId);
    }

    [Fact]
    public async Task InstantiateAsync_throws_on_unknown_template_id()
    {
        await using var fx = new DbFixture();
        var svc = CreateSut(fx);
        var act = async () => await svc.InstantiateAsync(Guid.NewGuid(), parentId: null);
        await act.Should().ThrowAsync<HabitTemplateNotFoundException>();
    }

    [Fact]
    public async Task InstantiateAsync_materializes_metric_pointing_at_the_seeded_scale()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        Guid scaleId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            // mood-5 ships in the ScaleTemplate seed — reference it, don't re-insert.
            scaleId = (await db.ScaleTemplates.SingleAsync(s => s.Code == "mood-5")).Id;
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Mood tracker", IsOfficial = true,
                PayloadJson = "{\"node\":{\"name\":\"Mood tracker\",\"node_type\":\"Habit\",\"habit_type\":\"State\",\"color\":\"#f39c12\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Mood\",\"type\":\"Integer\",\"scale_code\":\"mood-5\"}],\"segments\":[]}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var metric = await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId);
        metric.Name.Should().Be("Mood");
        metric.Type.Should().Be(MetricType.Decimal);   // GIO-23: a scale metric is always Decimal, whatever the payload says
        metric.ScaleTemplateId.Should().Be(scaleId);
        (await db2.ScaleAnchors.CountAsync(a => a.ScaleTemplateId == scaleId)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InstantiateAsync_materializes_metric_unit_and_goal()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        Guid unitId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            // "l" ships in the UnitOfMeasure seed — reference it, don't re-insert.
            unitId = (await db.UnitsOfMeasure.SingleAsync(u => u.Code == "l")).Id;
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Water intake", IsOfficial = true,
                PayloadJson = "{\"node\":{\"name\":\"Water intake\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#5bc8f5\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Amount\",\"type\":\"Decimal\",\"unit\":\"l\"}],\"goal\":{\"target\":2,\"unit\":\"l\",\"period_type\":\"Open\"}}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var metric = await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId);
        metric.UnitOfMeasureId.Should().Be(unitId);

        var goal = await db2.Goals.SingleAsync(g => g.HabitNodeId == newId);
        goal.TargetValue.Should().Be(2);
        goal.PeriodType.Should().Be(GoalPeriodType.Open);
    }

    [Fact]
    public async Task InstantiateAsync_materializes_segments()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Workout log", IsOfficial = true,
                PayloadJson = "{\"node\":{\"name\":\"Workout log\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"None\",\"timer_mode\":\"Session\"},\"metrics\":[],\"segments\":[{\"name\":\"Work\",\"is_rest\":false,\"sort_order\":0},{\"name\":\"Rest\",\"is_rest\":true,\"sort_order\":1}]}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var segments = await db2.SegmentDefinitions.Where(s => s.HabitNodeId == newId).OrderBy(s => s.SortOrder).ToListAsync();
        segments.Select(s => s.Name).Should().Equal("Work", "Rest");
        segments[0].IsRest.Should().BeFalse();
        segments[1].IsRest.Should().BeTrue();
    }

    [Fact]
    public async Task InstantiateAsync_goal_inherits_the_habit_name_not_a_hardcoded_default()
    {
        // CR9: the goal's name must never fall back to a hardcoded English "Goal". When the payload
        // omits the node name, the effective habit name (the template name) is used instead.
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Meditate", IsOfficial = true,
                PayloadJson = "{\"node\":{\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7c5cff\"},\"goal\":{\"target\":1,\"period_type\":\"Open\"}}",
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var goal = await db2.Goals.SingleAsync(g => g.HabitNodeId == newId);
        goal.Name.Should().Be("Meditate");
    }

    [Fact]
    public async Task Instantiate_maps_repeatable_and_series_labels_from_the_payload()
    {
        await using var fx = new DbFixture();
        var payload = "{\"node\":{\"name\":\"S\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"}," +
                      "\"config\":{\"schedule_type\":\"Daily\",\"repeatable\":true,\"series_labels\":[\"Night\",\"Nap\"]},\"metrics\":[]}";
        var templateId = await InsertTemplateAsync(fx, payload);

        var svc = CreateSut(fx);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var nodeId = await svc.InstantiateAsync(templateId, parentId: null);

        var cfg = await configs.GetAsync(nodeId);
        cfg!.Repeatable.Should().BeTrue();
        cfg.SeriesLabelsJson.Should().Be("[\"Night\",\"Nap\"]");
    }

    [Fact]
    public async Task Instantiate_reads_legacy_root_level_repeatable_from_pre_task3_templates()
    {
        // I5: HabitBuilderViewModel wrote "repeatable" at the payload ROOT (with no "config" object
        // at all) for every template saved between U8 and the Task 3 fix (wave 2.4). InstantiateAsync
        // reads only config.repeatable, and its config-mutation lambda used to bail out entirely
        // when payload.Config was null — so a template saved under this shape silently lost the flag
        // on every re-instantiation. This is a compatibility read, not a new writer.
        await using var fx = new DbFixture();
        var payload = "{\"node\":{\"name\":\"S\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"}," +
                      "\"repeatable\":true,\"metrics\":[]}";
        var templateId = await InsertTemplateAsync(fx, payload);

        var svc = CreateSut(fx);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var nodeId = await svc.InstantiateAsync(templateId, parentId: null);

        var cfg = await configs.GetAsync(nodeId);
        cfg!.Repeatable.Should().BeTrue();
    }

    [Fact]
    public async Task Instantiate_prefers_nested_config_repeatable_over_the_legacy_root_key()
    {
        // Defensive: if a payload somehow carries both (should never happen from either writer), the
        // current (Task 3) shape wins.
        await using var fx = new DbFixture();
        var payload = "{\"node\":{\"name\":\"S\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"}," +
                      "\"config\":{\"schedule_type\":\"Daily\",\"repeatable\":false},\"repeatable\":true,\"metrics\":[]}";
        var templateId = await InsertTemplateAsync(fx, payload);

        var svc = CreateSut(fx);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var nodeId = await svc.InstantiateAsync(templateId, parentId: null);

        var cfg = await configs.GetAsync(nodeId);
        cfg!.Repeatable.Should().BeFalse();
    }

    [Fact]
    public async Task Instantiate_reads_goal_name_rolling_days_tolerance_rest_and_auto_prompt()
    {
        await using var fx = new DbFixture();
        var payload = "{\"node\":{\"name\":\"Corsa\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"}," +
                      "\"config\":{\"schedule_type\":\"Daily\",\"has_integrated_timer\":true,\"timer_mode\":\"Session\"," +
                      "\"streak_tolerance_minutes\":90,\"rest_timer_seconds\":45,\"timer_auto_prompt\":true}," +
                      "\"metrics\":[{\"name\":\"Km\",\"type\":\"Decimal\"}]," +
                      "\"goal\":{\"name\":\"Km della settimana\",\"target\":20,\"period_type\":\"Rolling\",\"rolling_days\":7}}";
        var templateId = await InsertTemplateAsync(fx, payload);

        var svc = CreateSut(fx);
        var nodeId = await svc.InstantiateAsync(templateId, parentId: null);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var cfg = await db.HabitConfigs.SingleAsync(c => c.HabitNodeId == nodeId);
        cfg.StreakToleranceMinutes.Should().Be(90);
        cfg.RestTimerSeconds.Should().Be(45);
        cfg.TimerAutoPrompt.Should().BeTrue();
        var goal = await db.Goals.SingleAsync(g => g.HabitNodeId == nodeId);
        goal.Name.Should().Be("Km della settimana");
        goal.PeriodType.Should().Be(GoalPeriodType.Rolling);
        goal.RollingDays.Should().Be(7);
    }

    [Fact]
    public async Task Instantiate_without_the_new_fields_keeps_todays_defaults()
    {
        await using var fx = new DbFixture();
        var payload = "{\"node\":{\"name\":\"Corsa\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"}," +
                      "\"config\":{\"schedule_type\":\"Daily\"},\"goal\":{\"target\":20,\"period_type\":\"Open\"}}";
        var templateId = await InsertTemplateAsync(fx, payload);

        var nodeId = await CreateSut(fx).InstantiateAsync(templateId, parentId: null);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var cfg = await db.HabitConfigs.SingleAsync(c => c.HabitNodeId == nodeId);
        cfg.StreakToleranceMinutes.Should().BeNull();
        cfg.RestTimerSeconds.Should().BeNull();
        cfg.TimerAutoPrompt.Should().BeFalse();
        var goal = await db.Goals.SingleAsync(g => g.HabitNodeId == nodeId);
        goal.Name.Should().Be("Corsa");
        goal.RollingDays.Should().BeNull();
    }

    private static async Task<Guid> InsertTemplateAsync(DbFixture fx, string payloadJson)
    {
        var templateId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitTemplates.Add(new HabitTemplate
        {
            Id = templateId,
            Name = "T",
            PayloadJson = payloadJson,
            IsOfficial = true,
        });
        await db.SaveChangesAsync();
        return templateId;
    }

    [Fact]
    public async Task Shipped_Workout_log_template_materializes_its_duration_with_a_time_unit()
    {
        // R7: the builder refuses to create a duration metric without a time unit, but the shipped
        // "Workout log" seed carried {"name":"Duration","type":"Seconds"} with no "unit" key — so
        // instantiating an official template produced, on every fresh install, exactly the object the
        // rule exists to prevent. This reads the SEEDED payload (EnsureCreated applies HasData), not a
        // payload the test writes, so it fails the day someone edits the seed back.
        await using var fx = new DbFixture();
        var workoutLogId = Guid.Parse("00000003-0000-0000-0000-000000000002");
        Guid minutesId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.HabitTemplates.SingleAsync(t => t.Id == workoutLogId)).Name.Should().Be("Workout log");
            minutesId = (await db.UnitsOfMeasure.SingleAsync(u => u.Code == "minutes")).Id;
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(workoutLogId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var duration = await db2.MetricDefinitions
            .SingleAsync(m => m.HabitNodeId == newId && m.Type == MetricType.Seconds);
        duration.Name.Should().Be("Duration");
        duration.UnitOfMeasureId.Should().Be(minutesId);
    }

    [Fact]
    public async Task No_shipped_template_ships_a_duration_metric_without_a_time_unit()
    {
        // The guard that generalises the one above: whatever official templates ship, none of them may
        // declare a Seconds metric without a "unit" that is one of the three time units. Catches a new
        // template added later with the same omission.
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var payloads = await db.HabitTemplates.Where(t => t.IsOfficial)
            .Select(t => new { t.Name, t.PayloadJson }).ToListAsync();
        payloads.Should().NotBeEmpty();

        foreach (var t in payloads)
        {
            var payload = System.Text.Json.JsonDocument.Parse(t.PayloadJson);
            if (!payload.RootElement.TryGetProperty("metrics", out var metrics)) continue;
            foreach (var m in metrics.EnumerateArray())
            {
                if (!m.TryGetProperty("type", out var type) || type.GetString() != "Seconds") continue;
                var unit = m.TryGetProperty("unit", out var u) ? u.GetString() : null;
                Recordum.Core.Formatting.SecondsDisplay.IsTimeUnit(unit).Should()
                    .BeTrue($"the '{t.Name}' template declares a duration metric, which needs a time unit (found '{unit ?? "none"}')");
            }
        }
    }

    [Fact]
    public async Task Shipped_Workout_log_template_materializes_a_schedule_that_comes_due()
    {
        // W25-2: il seed dichiarava SpecificDays senza dire QUALI giorni, e MatchesDayMask(date, null)
        // è false per ogni data — l'abitudine istanziata non scadeva mai e la sua streak restava a zero
        // per sempre. Ora dichiara WeeklyX con target 3, che è anche l'unica lettura coerente con lo
        // streak_mode Weekly che il seed già portava. Legge il payload SEMINATO, non uno scritto qui.
        await using var fx = new DbFixture();
        var workoutLogId = Guid.Parse("00000003-0000-0000-0000-000000000002");
        await using (var db = await fx.Factory.CreateDbContextAsync())
            (await db.HabitTemplates.SingleAsync(t => t.Id == workoutLogId)).Name.Should().Be("Workout log");

        var newId = await CreateSut(fx).InstantiateAsync(workoutLogId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var cfg = await db2.HabitConfigs.SingleAsync(c => c.HabitNodeId == newId);
        cfg.ScheduleType.Should().Be(ScheduleType.WeeklyX);
        cfg.ScheduleWeeklyTarget.Should().Be(3);
    }

    [Fact]
    public async Task No_shipped_template_ships_a_schedule_that_never_comes_due()
    {
        // Il gemello del guard di R7, per il programma invece che per le unità: qualunque template
        // ufficiale si spedisca, nessuno può dichiarare una forma di schedule che non è mai
        // programmata — SpecificDays senza giorni, WeeklyX senza target, EveryXDays senza intervallo.
        // Sono tre modi diversi di creare un'abitudine che non scade mai e non fa mai streak.
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var payloads = await db.HabitTemplates.Where(t => t.IsOfficial)
            .Select(t => new { t.Name, t.PayloadJson }).ToListAsync();
        payloads.Should().NotBeEmpty();

        foreach (var t in payloads)
        {
            var root = System.Text.Json.JsonDocument.Parse(t.PayloadJson).RootElement;
            if (!root.TryGetProperty("config", out var cfg)) continue;
            var type = cfg.TryGetProperty("schedule_type", out var st) ? st.GetString() : null;

            switch (type)
            {
                case "SpecificDays":
                    var days = cfg.TryGetProperty("schedule_days_json", out var d) ? d.GetString() : null;
                    days.Should().NotBeNullOrWhiteSpace(
                        $"the '{t.Name}' template schedules specific days without saying which");
                    days.Should().NotBe("[]",
                        $"the '{t.Name}' template schedules an empty set of days");
                    break;
                case "WeeklyX":
                    var target = cfg.TryGetProperty("schedule_weekly_target", out var w) ? w.GetInt32() : (int?)null;
                    target.Should().BeGreaterThan(0,
                        $"the '{t.Name}' template asks for X times a week without saying X");
                    break;
                case "EveryXDays":
                    var every = cfg.TryGetProperty("schedule_every_x_days", out var e) ? e.GetInt32() : (int?)null;
                    every.Should().BeGreaterThan(0,
                        $"the '{t.Name}' template repeats every X days without saying X");
                    break;
                default:
                    break;   // Daily / Monthly / Custom / assente: nessun campo obbligatorio da controllare
            }
        }
    }

    [Fact]
    public async Task InstantiateAsync_applies_the_journal_gesture_from_the_payload()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId,
                Name = "Food diary",
                PayloadJson = "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"}," +
                              "\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}," +
                              "\"ui_prefs\":{\"journal_gesture\":\"QuickNote\"}}",
                IsOfficial = true,
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateSut(fx);
        var newId = await svc.InstantiateAsync(tmplId, parentId: null);

        var prefs = new HabitUiPrefsService(fx.Factory);
        (await prefs.GetAsync(newId)).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
    }

    [Fact]
    public async Task InstantiateAsync_leaves_ui_prefs_null_when_the_payload_has_none()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId,
                Name = "Daily journal",
                PayloadJson = "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"}," +
                              "\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"}}",
                IsOfficial = true,
            });
            await db.SaveChangesAsync();
        }

        var newId = await CreateSut(fx).InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        // Nessuna scrittura spuria: la colonna resta NULL, cioè "usa il default del tipo".
        (await db2.HabitNodes.FindAsync(newId))!.UiPrefsJson.Should().BeNull();
    }

    // ===== Onda 2.5 T8 — unità e scale incorporate nel payload (risolvi-o-crea) =====

    [Fact]
    public async Task InstantiateAsync_creates_missing_units_and_scales_from_embedded_definitions()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Tè", IsOfficial = false,
                PayloadJson = """
                    {"node":{"name":"Tè","node_type":"Habit","habit_type":"Counter","color":"#123456"},
                     "config":{"schedule_type":"Daily","streak_mode":"Daily","streak_recovery":"GraceDay"},
                     "metrics":[{"name":"Tazze","type":"Decimal","unit":"tazze"},
                                {"name":"Energia","type":"Integer","scale_code":"energia-3"}],
                     "segments":[],
                     "units":[{"code":"tazze","name":"Tazze","symbol":"tz"}],
                     "scales":[{"code":"energia-3","name":"Energia","min_value":1,"max_value":3,
                                "labels":["Bassa","Media","Alta"]}]}
                    """,
            });
            await db.SaveChangesAsync();
        }

        var newId = await CreateSut(fx).InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var unit = await db2.UnitsOfMeasure.SingleAsync(u => u.Code == "tazze");
        unit.Name.Should().Be("Tazze");
        unit.Symbol.Should().Be("tz");
        unit.IsOfficial.Should().BeFalse();
        var scale = await db2.ScaleTemplates.SingleAsync(s => s.Code == "energia-3");
        scale.MaxValue.Should().Be(3);
        var anchors = await db2.ScaleAnchors.Where(a => a.ScaleTemplateId == scale.Id).OrderBy(a => a.Value).ToListAsync();
        anchors.Select(a => (a.Value, a.Label)).Should().Equal((1, "Bassa"), (2, "Media"), (3, "Alta"));
        anchors.Should().OnlyContain(a => a.TagId != null);
        scale.Precision.Should().Be(ScalePrecision.Integer);
        scale.IsOfficial.Should().BeFalse();

        var metric = await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId && m.Name == "Tazze");
        metric.UnitOfMeasureId.Should().Be(unit.Id);
        var scaleMetric = await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId && m.Name == "Energia");
        scaleMetric.ScaleTemplateId.Should().Be(scale.Id);
    }

    [Fact]
    public async Task InstantiateAsync_reads_old_labels_widening_max_and_new_anchors_with_precision()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Due", IsOfficial = false,
                PayloadJson = """
                    {"node":{"name":"Due","node_type":"Habit","habit_type":"State","color":"#123456"},
                     "config":{"schedule_type":"Daily","streak_mode":"Daily","streak_recovery":"GraceDay"},
                     "metrics":[{"name":"X","type":"Decimal","scale_code":"nuova"}],"segments":[],
                     "scales":[{"code":"vecchia","name":"Vecchia","min_value":1,"max_value":2,"labels":["A","B","C"]},
                               {"code":"nuova","name":"Nuova","min_value":0,"max_value":10,"precision":"Decimal",
                                "anchors":[{"value":0,"label":"Niente"},{"value":10,"label":null}]}]}
                    """,
            });
            await db.SaveChangesAsync();
        }

        await CreateSut(fx).InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var old = await db2.ScaleTemplates.Include(s => s.Anchors).SingleAsync(s => s.Code == "vecchia");
        old.MaxValue.Should().Be(3);
        old.Anchors.OrderBy(a => a.Value).Select(a => (a.Value, a.Label)).Should().Equal((1, "A"), (2, "B"), (3, "C"));
        var nu = await db2.ScaleTemplates.Include(s => s.Anchors).SingleAsync(s => s.Code == "nuova");
        nu.Precision.Should().Be(ScalePrecision.Decimal);
        nu.Anchors.Should().HaveCount(2);
        nu.Anchors.Single(a => a.Value == 10).TagId.Should().BeNull();
    }

    [Fact]
    public async Task InstantiateAsync_reuses_an_existing_code_instead_of_duplicating()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        Guid seedUnitId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            seedUnitId = (await db.UnitsOfMeasure.SingleAsync(u => u.Code == "l")).Id;
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Acqua", IsOfficial = false,
                PayloadJson = """
                    {"node":{"name":"Acqua","node_type":"Habit","habit_type":"Counter","color":"#5bc8f5"},
                     "config":{"schedule_type":"Daily","streak_mode":"Daily","streak_recovery":"GraceDay"},
                     "metrics":[{"name":"Quantità","type":"Decimal","unit":"l"}],
                     "units":[{"code":"l","name":"Duplicato"}]}
                    """,
            });
            await db.SaveChangesAsync();
        }

        var newId = await CreateSut(fx).InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.UnitsOfMeasure.CountAsync(u => u.Code == "l")).Should().Be(1);
        (await db2.UnitsOfMeasure.SingleAsync(u => u.Code == "l")).Name.Should().NotBe("Duplicato");
        (await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == newId)).UnitOfMeasureId.Should().Be(seedUnitId);
    }

    [Fact]
    public async Task Instantiating_the_same_template_twice_creates_one_unit_not_two()
    {
        // The second instantiation resolves the code the first one created — otherwise every launch
        // of the same custom template would litter the catalogue with tazze, tazze-2, tazze-3…
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Tè", IsOfficial = false,
                PayloadJson = """
                    {"node":{"name":"Tè","node_type":"Habit","habit_type":"Counter","color":"#123456"},
                     "metrics":[{"name":"Tazze","type":"Decimal","unit":"tazze"}],
                     "units":[{"code":"tazze","name":"Tazze"}]}
                    """,
            });
            await db.SaveChangesAsync();
        }
        var svc = CreateSut(fx);

        var first = await svc.InstantiateAsync(tmplId, parentId: null);
        var second = await svc.InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.UnitsOfMeasure.CountAsync(u => u.Code == "tazze")).Should().Be(1);
        var unitId = (await db2.UnitsOfMeasure.SingleAsync(u => u.Code == "tazze")).Id;
        (await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == first)).UnitOfMeasureId.Should().Be(unitId);
        (await db2.MetricDefinitions.SingleAsync(m => m.HabitNodeId == second)).UnitOfMeasureId.Should().Be(unitId);
    }

    private static HabitTemplateService CreateSut(DbFixture fx) => new(
        fx.Factory, fx.Clock, fx.PluginContext,
        new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance, new RecordingEventBus()),
        new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus()),
        new HabitUiPrefsService(fx.Factory),
        NullLogger<HabitTemplateService>.Instance);

    [Fact]
    public async Task InstantiateAsync_two_embedded_scales_sharing_a_label_share_one_tag()
    {
        await using var fx = new DbFixture();
        var tmplId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Id = tmplId, Name = "Due", IsOfficial = false,
                PayloadJson = """
                    {"node":{"name":"Due","node_type":"Habit","habit_type":"State","color":"#123456"},
                     "config":{"schedule_type":"Daily","streak_mode":"Daily","streak_recovery":"GraceDay"},
                     "metrics":[{"name":"X","type":"Decimal","scale_code":"s1"}],"segments":[],
                     "scales":[{"code":"s1","name":"S1","min_value":1,"max_value":3,"anchors":[{"value":1,"label":"Bene"}]},
                               {"code":"s2","name":"S2","min_value":1,"max_value":3,"anchors":[{"value":1,"label":"Bene"}]}]}
                    """,
            });
            await db.SaveChangesAsync();
        }

        await CreateSut(fx).InstantiateAsync(tmplId, parentId: null);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.Tags.CountAsync(t => t.Name == "bene")).Should().Be(1);
    }
}
