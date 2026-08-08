using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Recordum.Data.Tests.Schema;

/// <summary>
/// Verifies that InitialSchema migration seeds all required data per CLAUDE.md §6.8.
/// Uses the shared SchemaFixture (one migration per test class).
/// </summary>
public sealed class SeedDataTests : IClassFixture<SchemaFixture>
{
    private readonly SchemaFixture _fixture;

    public SeedDataTests(SchemaFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void UnitOfMeasure_has_14_official_rows()
    {
        using var ctx = _fixture.CreateContext();
        var count = ctx.UnitsOfMeasure.Count(u => u.IsOfficial);
        Assert.Equal(14, count);
    }

    [Fact]
    public void UnitOfMeasure_contains_expected_codes()
    {
        using var ctx = _fixture.CreateContext();
        var codes = ctx.UnitsOfMeasure.Select(u => u.Code).ToHashSet();

        var expectedCodes = new[]
        {
            "km", "m", "kg", "g", "ml", "l", "kcal",
            "steps", "hours", "minutes", "seconds", "pages", "reps", "sets",
        };

        foreach (var code in expectedCodes)
            Assert.Contains(code, codes);
    }

    [Fact]
    public void ScaleTemplate_has_7_official_rows()
    {
        using var ctx = _fixture.CreateContext();
        var count = ctx.ScaleTemplates.Count(s => s.IsOfficial);
        Assert.Equal(7, count);
    }

    [Fact]
    public void ScaleTemplate_contains_expected_codes()
    {
        using var ctx = _fixture.CreateContext();
        var codes = ctx.ScaleTemplates.Select(s => s.Code).ToHashSet();

        var expectedCodes = new[]
        {
            "bristol",
            "mayo-rectal-bleeding",
            "nrs-pain",
            "likert-5",
            "sleep-quality",
            "stress-10",
            "mood-5",
        };

        foreach (var code in expectedCodes)
            Assert.Contains(code, codes);
    }

    [Fact]
    public void HabitTemplate_has_8_rows()
    {
        using var ctx = _fixture.CreateContext();
        var count = ctx.HabitTemplates.Count();
        Assert.Equal(8, count);
    }

    [Fact]
    public void HabitTemplate_all_are_official()
    {
        using var ctx = _fixture.CreateContext();
        Assert.All(ctx.HabitTemplates, t => Assert.True(t.IsOfficial));
    }

    [Fact]
    public void ExportTemplate_has_4_rows()
    {
        using var ctx = _fixture.CreateContext();
        var count = ctx.ExportTemplates.Count();
        Assert.Equal(4, count);
    }

    [Fact]
    public void ExportTemplate_all_are_official()
    {
        using var ctx = _fixture.CreateContext();
        Assert.All(ctx.ExportTemplates, t => Assert.True(t.IsOfficial));
    }

    [Fact]
    public void AppSetting_has_18_rows()
    {
        using var ctx = _fixture.CreateContext();
        var count = ctx.AppSettings.Count();
        Assert.Equal(18, count);
    }

    [Fact]
    public void AppSetting_schema_version_is_2_17()
    {
        using var ctx = _fixture.CreateContext();
        var schemaVersion = ctx.AppSettings
            .Where(s => s.Key == "schema_version")
            .Select(s => s.Value)
            .Single();

        Assert.Equal("2.17", schemaVersion);
    }

    [Fact]
    public void No_official_journal_template_declares_metrics()
    {
        // Un habit Journal si registra scrivendo: non ha nessun modulo metriche. Un template
        // journal che dichiara metriche promette un inserimento che il tipo non sa offrire
        // (principio 8). Gemello del guard sulle durate senza unita' introdotto con R7.
        using var ctx = _fixture.CreateContext();
        foreach (var t in ctx.HabitTemplates.Where(t => t.IsOfficial).ToList())
        {
            using var doc = System.Text.Json.JsonDocument.Parse(t.PayloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("node", out var node)) continue;
            if (!node.TryGetProperty("habit_type", out var ht) || ht.GetString() != "Journal") continue;
            var hasMetrics = root.TryGetProperty("metrics", out var metrics)
                && metrics.ValueKind == System.Text.Json.JsonValueKind.Array
                && metrics.GetArrayLength() > 0;
            Assert.False(hasMetrics, $"Template '{t.Name}' e' un Journal ma dichiara metriche.");
        }
    }

    [Fact]
    public void Food_diary_template_ships_the_quick_note_gesture()
    {
        using var ctx = _fixture.CreateContext();
        var t = ctx.HabitTemplates.Single(x => x.Name == "Food diary");
        using var doc = System.Text.Json.JsonDocument.Parse(t.PayloadJson);
        var gesture = doc.RootElement.GetProperty("ui_prefs").GetProperty("journal_gesture").GetString();
        Assert.Equal("QuickNote", gesture);
    }

    [Fact]
    public void AppSetting_all_required_keys_exist()
    {
        using var ctx = _fixture.CreateContext();
        var keys = ctx.AppSettings.Select(s => s.Key).ToHashSet();

        var expectedKeys = new[]
        {
            "language",
            "daily_reset_time",
            "theme",
            "onboarding_complete",
            "ai_mode",
            "ai_default_sharing",
            "notifications_enabled",
            "app_lock_enabled",
            "app_lock_type",
            "inactivity_timeout_minutes",
            "monthly_summary_day",
            "monthly_summary_time",
            "backup.auto.enabled",
            "backup.auto.frequency_days",
            "backup.auto.retention_count",
            "backup.auto.last_run_utc",
            "backup.auto.path",
            "schema_version",
        };

        foreach (var key in expectedKeys)
            Assert.Contains(key, keys);
    }

    [Fact]
    public void AppSetting_ai_mode_default_is_disabled()
    {
        using var ctx = _fixture.CreateContext();
        var value = ctx.AppSettings
            .Where(s => s.Key == "ai_mode")
            .Select(s => s.Value)
            .Single();

        Assert.Equal("disabled", value);
    }
}
