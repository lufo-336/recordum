using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

/// <summary>
/// Onda 2.5 T10 — the permanent guard behind E16: <b>everything an official template ships, the user
/// must be able to compose in the builder</b> (principle 8: a template is a saved composition, never
/// an experience only the program can provide).
///
/// <para>Three layers, each closing a different way the promise could rot:
/// <b>coverage</b> — every seed template has a builder fixture, so adding a template without the brick
/// it needs fails here; <b>semantic parity</b> — the payload the builder writes for that fixture must
/// mean the same as the seed's, compared key by key with defaults normalised; <b>the whitelist</b> —
/// any key appearing in either payload must be known to this test, so a template that grows a new
/// capability cannot slip past by simply not being compared.</para>
///
/// <para>Twin of the R7 guard in <c>HabitTemplateSeedTests</c>, which walks the same templates for a
/// different invariant (no unit-less duration).</para>
/// </summary>
public class BuilderTemplateParityTests
{
    /// <summary>Every key the payload contract (T8) defines, plus the ones a seed may carry. A key
    /// outside this set means a template gained a capability that neither the builder nor this guard
    /// knows about — see the failure message.</summary>
    private static readonly HashSet<string> KnownPayloadKeys = new(StringComparer.Ordinal)
    {
        "node", "config", "ui_prefs", "metrics", "segments", "goal", "units", "scales", "description",
        // node
        "name", "node_type", "habit_type", "color", "icon",
        // config
        "schedule_type", "schedule_days_json", "schedule_weekly_target", "schedule_every_x_days",
        "streak_mode", "streak_recovery", "has_integrated_timer", "timer_mode", "timer_target_seconds",
        "repeatable", "series_labels",
        // ui_prefs
        "journal_gesture",
        // metrics
        "type", "unit", "scale_code",
        // segments
        "is_rest", "sort_order",
        // goal
        "target", "period_type",
        // unit / scale definitions
        "code", "min_value", "max_value", "labels", "symbol",
    };

    /// <summary>How each official template is composed in the builder. Written by hand on purpose:
    /// this is the claim "a user could have built this", and only a human-written recipe can make it.</summary>
    private static readonly Dictionary<string, Action<HabitBuilderViewModel>> FixtureBuilders = new(StringComparer.Ordinal)
    {
        ["Morning routine"] = vm =>
        {
            vm.Name = "Morning routine";
            vm.Color = "#f5a623";
        },
        ["Workout log"] = vm =>
        {
            vm.Name = "Workout log";
            vm.Color = "#e85d4a";
            // W25-2 chiuso: il seed dichiarava SpecificDays senza dire quali giorni, e la fixture era
            // costretta a non selezionarne nessuno per restare fedele al payload — cioe' a comporre
            // un'abitudine che il gate della review 2.5 (F3) giustamente rifiuta, perche' non sarebbe
            // mai scaduta. Ora dichiara "tre volte a settimana", che e' anche l'unica lettura coerente
            // con lo streak_mode Weekly che il seed gia' portava.
            vm.Schedule.ScheduleType = ScheduleType.WeeklyX;
            vm.Schedule.WeeklyTarget = 3;
            vm.Schedule.StreakMode = StreakMode.Weekly;
            vm.Schedule.StreakRecovery = StreakRecovery.None;
            AddField(vm, FieldNature.Time, "Duration", unit: "minutes");
            AddField(vm, FieldNature.Quantity, "Reps");
            AddField(vm, FieldNature.Quantity, "Weight", unit: null, decimalField: true);
            vm.SessionBrick.HasSession = true;
        },
        ["Daily journal"] = vm =>
        {
            vm.Name = "Daily journal";
            vm.Color = "#4a90e2";
            AddField(vm, FieldNature.Text, "Entry");
            vm.JournalGesture = JournalPrimaryGesture.Diary;
        },
        ["Water intake"] = vm =>
        {
            vm.Name = "Water intake";
            vm.Color = "#5bc8f5";
            AddField(vm, FieldNature.Quantity, "Amount", unit: "l");
            vm.GoalBrick.HasGoal = true;
            vm.GoalBrick.Target = 2m;
        },
        ["Sleep tracker"] = vm =>
        {
            vm.Name = "Sleep tracker";
            vm.Color = "#7b68ee";
            AddField(vm, FieldNature.Quantity, "Hours slept", unit: "hours");
            AddField(vm, FieldNature.Scale, "Sleep quality", scale: "sleep-quality");
            vm.Repeatable = true;
            AddSeriesLabel(vm, "Night");
            AddSeriesLabel(vm, "Nap");
        },
        ["Stop smoking"] = vm =>
        {
            vm.Name = "Stop smoking";
            vm.Color = "#27ae60";
            vm.Abstinence = true;
            vm.Schedule.StreakRecovery = StreakRecovery.None;
        },
        ["Mood tracker"] = vm =>
        {
            vm.Name = "Mood tracker";
            vm.Color = "#f39c12";
            AddField(vm, FieldNature.Scale, "Mood", scale: "mood-5");
        },
        ["Food diary"] = vm =>
        {
            vm.Name = "Food diary";
            vm.Color = "#e67e22";
            AddField(vm, FieldNature.Text, "Note");
            vm.JournalGesture = JournalPrimaryGesture.QuickNote;
        },
    };

    [Fact]
    public void Every_official_template_has_a_builder_fixture()
    {
        foreach (var t in HabitTemplateSeed.All)
        {
            FixtureBuilders.Should().ContainKey(t.Name,
                $"the official template '{t.Name}' must be composable in the builder (E16) — add the " +
                "fixture, or the brick it is missing");
        }
    }

    [Theory]
    [MemberData(nameof(OfficialTemplateNames))]
    public void The_builder_composes_every_official_template(string name)
    {
        var seed = HabitTemplateSeed.All.Single(t => t.Name == name);
        var vm = BuildFixture(name);

        var built = JsonDocument.Parse(vm.BuildTemplatePayloadJsonForTest()).RootElement;
        var expected = JsonDocument.Parse(seed.PayloadJson!).RootElement;

        AssertKnownKeys(expected, name);
        AssertKnownKeys(built, name);
        AssertSameMeaning(expected, built, path: "$", template: name);
    }

    public static TheoryData<string> OfficialTemplateNames()
    {
        var data = new TheoryData<string>();
        foreach (var t in HabitTemplateSeed.All) data.Add(t.Name);
        return data;
    }

    [Fact]
    public void A_composition_using_every_brick_survives_the_payload_round_trip()
    {
        // The synthetic case the seeds do not cover: staged reference data, a full schedule, named
        // series, a session and a goal, all at once.
        var vm = NewVm();
        vm.Name = "Tutto insieme";
        vm.Color = "#123456";
        AddField(vm, FieldNature.Quantity, "Tazze");
        vm.BeginAddCustomUnit(vm.Fields[0]);
        vm.UnitForm!.Name = "Tazze";
        vm.ConfirmCustomUnitCommand.Execute(null);
        AddField(vm, FieldNature.Scale, "Energia");
        vm.BeginAddCustomScale(vm.Fields[1]);
        vm.ScaleForm!.Name = "Energia";
        vm.ScaleForm.LabelsText = "Bassa, Media, Alta";
        vm.ConfirmCustomScaleCommand.Execute(null);
        vm.Repeatable = true;
        AddSeriesLabel(vm, "Mattina");
        AddSeriesLabel(vm, "Sera");
        vm.Schedule.ScheduleType = ScheduleType.SpecificDays;
        foreach (var key in new[] { "Mon", "Wed", "Fri" })
            vm.Schedule.Days.Single(d => d.DayKey == key).IsSelected = true;
        vm.Schedule.StreakMode = StreakMode.Weekly;
        vm.Schedule.StreakRecovery = StreakRecovery.Manual;
        vm.SessionBrick.HasSession = true;
        vm.SessionBrick.Segments.Add(new SegmentRowViewModel { Name = "Cooldown", IsRest = true });
        vm.GoalBrick.HasGoal = true;
        vm.GoalBrick.Target = 5m;

        var payload = JsonDocument.Parse(vm.BuildTemplatePayloadJsonForTest()).RootElement;
        AssertKnownKeys(payload, "Tutto insieme");

        var config = payload.GetProperty("config");
        config.GetProperty("schedule_type").GetString().Should().Be("SpecificDays");
        config.GetProperty("schedule_days_json").GetString().Should().Be("""["Mon","Wed","Fri"]""");
        config.GetProperty("streak_mode").GetString().Should().Be("Weekly");
        config.GetProperty("streak_recovery").GetString().Should().Be("Manual");
        config.GetProperty("repeatable").GetBoolean().Should().BeTrue();
        config.GetProperty("series_labels").EnumerateArray().Select(l => l.GetString())
            .Should().Equal("Mattina", "Sera");
        config.GetProperty("has_integrated_timer").GetBoolean().Should().BeTrue();
        config.GetProperty("timer_mode").GetString().Should().Be("Session");
        payload.GetProperty("segments").EnumerateArray().Select(s => s.GetProperty("name").GetString())
            .Should().Equal("Work", "Rest", "Cooldown");
        payload.GetProperty("goal").GetProperty("target").GetDecimal().Should().Be(5m);
        // Il builder compone solo goal aperti (review 2.5, F1): Fixed/Rolling richiedono campi che
        // solo Configura ha, e senza di essi violerebbero ck_goal_period_consistency.
        payload.GetProperty("goal").GetProperty("period_type").GetString().Should().Be("Open");
        payload.GetProperty("units").EnumerateArray().Single().GetProperty("code").GetString().Should().Be("tazze");
        payload.GetProperty("scales").EnumerateArray().Single().GetProperty("labels")
            .EnumerateArray().Select(l => l.GetString()).Should().Equal("Bassa", "Media", "Alta");

        // And the launched habit says the same thing as the saved template.
        var request = vm.BuildRequestForTest();
        request.Config!.ScheduleDaysJson.Should().Be("""["Mon","Wed","Fri"]""");
        request.Config.SeriesLabelsJson.Should().Be("""["Mattina","Sera"]""");
        request.Config.TimerMode.Should().Be(TimerMode.Session);
        request.Segments!.Select(s => s.Name).Should().Equal("Work", "Rest", "Cooldown");
        request.Goal!.TargetValue.Should().Be(5m);
        request.NewUnits!.Single().Code.Should().Be("tazze");
        request.NewScales!.Single().Code.Should().Be("energia");
    }

    // ===== helpers =====

    private static HabitBuilderViewModel BuildFixture(string name)
    {
        var vm = NewVm();
        FixtureBuilders[name](vm);
        return vm;
    }

    /// <summary>A builder loaded with the REAL seed catalogue, so the fixtures can pick units and
    /// scales by the same codes the seed payloads name.</summary>
    private static HabitBuilderViewModel NewVm()
    {
        var settings = new FakeAppSettingService();
        var vm = new HabitBuilderViewModel(new FakeComposedHabitService(), new FakeHabitTemplateService(),
            new SeedReferenceData(), new FakeClock(), new RecentColorStore(settings),
            new FavoriteColorStore(settings), new FakeScreenColorPicker());
        vm.InitializeAsync().GetAwaiter().GetResult();
        return vm;
    }

    private sealed class SeedReferenceData : IReferenceDataQueryService
    {
        public ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default) =>
            new(UnitOfMeasureSeed.All);
        public ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default) =>
            new(ScaleTemplateSeed.All);
    }

    private static void AddField(HabitBuilderViewModel vm, FieldNature nature, string name,
        string? unit = null, string? scale = null, bool decimalField = false)
    {
        vm.AddFieldCommand.Execute(nature);
        var field = vm.Fields[^1];
        field.Name = name;
        if (unit is not null) field.UnitOfMeasureId = vm.AvailableUnits.Single(u => u.Code == unit).Id;
        if (scale is not null) field.ScaleTemplateId = vm.AvailableScales.Single(s => s.Code == scale).Id;
        field.AllowsDecimals = decimalField;
    }

    private static void AddSeriesLabel(HabitBuilderViewModel vm, string label)
    {
        vm.AddSeriesLabelCommand.Execute(null);
        vm.SeriesLabels[^1].Name = label;
    }

    private static void AssertKnownKeys(JsonElement element, string template)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                KnownPayloadKeys.Should().Contain(prop.Name,
                    $"template '{template}' uses the payload key '{prop.Name}', which neither the " +
                    "builder nor this guard knows: a new capability reached the templates without a " +
                    "brick to compose it (E16) — add the brick and extend the guard, or drop the key");
                AssertKnownKeys(prop.Value, template);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) AssertKnownKeys(item, template);
        }
    }

    /// <summary>Compares two payloads by meaning rather than by text: key order is irrelevant, and an
    /// absent key equals null equals the type's default (an absent "segments" is an empty list, an
    /// absent "repeatable" is false). Anything else must match exactly.</summary>
    private static void AssertSameMeaning(JsonElement expected, JsonElement actual, string path, string template)
    {
        if (expected.ValueKind == JsonValueKind.Object || actual.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (expected.ValueKind == JsonValueKind.Object)
                foreach (var p in expected.EnumerateObject()) names.Add(p.Name);
            if (actual.ValueKind == JsonValueKind.Object)
                foreach (var p in actual.EnumerateObject()) names.Add(p.Name);

            foreach (var nameKey in names)
            {
                var e = Child(expected, nameKey);
                var a = Child(actual, nameKey);
                if (IsAbsentOrDefault(e) && IsAbsentOrDefault(a)) continue;
                AssertSameMeaning(e ?? default, a ?? default, $"{path}.{nameKey}", template);
            }
            return;
        }

        if (expected.ValueKind == JsonValueKind.Array || actual.ValueKind == JsonValueKind.Array)
        {
            var e = expected.ValueKind == JsonValueKind.Array ? expected.EnumerateArray().ToList() : [];
            var a = actual.ValueKind == JsonValueKind.Array ? actual.EnumerateArray().ToList() : [];
            a.Should().HaveCount(e.Count, $"'{template}' at {path}: the builder produced {a.Count} " +
                $"entries where the template has {e.Count}");
            for (var i = 0; i < e.Count; i++) AssertSameMeaning(e[i], a[i], $"{path}[{i}]", template);
            return;
        }

        actual.ToString().Should().Be(expected.ToString(),
            $"'{template}' at {path}: the composition the builder writes must mean the same as the " +
            "template's (E16)");
    }

    private static JsonElement? Child(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : null;

    /// <summary>Absent ≡ null ≡ the type's default: the two writers legitimately spell "nothing here"
    /// differently (the seed writes "segments":[], the builder omits the key).</summary>
    private static bool IsAbsentOrDefault(JsonElement? element) => element switch
    {
        null => true,
        { ValueKind: JsonValueKind.Null } => true,
        { ValueKind: JsonValueKind.False } => true,
        { ValueKind: JsonValueKind.Array } array => array.GetArrayLength() == 0,
        { ValueKind: JsonValueKind.String } s => s.GetString()!.Length == 0,
        _ => false,
    };
}
