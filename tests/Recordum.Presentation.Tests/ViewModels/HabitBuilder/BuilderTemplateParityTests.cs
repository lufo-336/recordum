using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Errors;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Resources;
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
[Collection(ProcessGlobalCultureCollection.Name)]
public class BuilderTemplateParityTests : IDisposable
{
    private readonly CultureInfo? _previousCulture = Strings.Culture;

    public void Dispose() => Strings.Culture = _previousCulture;

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
        "repeatable", "series_labels", "streak_tolerance_minutes", "rest_timer_seconds", "timer_auto_prompt",
        // ui_prefs
        "journal_gesture",
        // metrics
        "type", "unit", "scale_code",
        // segments
        "is_rest", "sort_order",
        // goal
        "target", "period_type", "rolling_days",
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
        // Ogni valore diverso dal default (il "parametro muto", CLAUDE.md §7).
        vm.GoalBrick.Name = "Tazze della settimana";
        vm.GoalBrick.PeriodType = GoalPeriodType.Rolling;
        vm.GoalBrick.RollingDays = 7;
        vm.Schedule.StreakToleranceMinutes = 90;
        vm.SessionBrick.RestTimerSeconds = 45;
        vm.SessionBrick.TimerAutoPrompt = true;

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
        // Rolling porta i suoi giorni (ck_goal_period_consistency); Fixed non si offre nel creatore.
        payload.GetProperty("goal").GetProperty("period_type").GetString().Should().Be("Rolling");
        payload.GetProperty("goal").GetProperty("rolling_days").GetInt32().Should().Be(7);
        payload.GetProperty("goal").GetProperty("name").GetString().Should().Be("Tazze della settimana");
        config.GetProperty("streak_tolerance_minutes").GetInt32().Should().Be(90);
        config.GetProperty("rest_timer_seconds").GetInt32().Should().Be(45);
        config.GetProperty("timer_auto_prompt").GetBoolean().Should().BeTrue();
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
        request.Goal.Name.Should().Be("Tazze della settimana");
        request.Goal.PeriodType.Should().Be(GoalPeriodType.Rolling);
        request.Goal.RollingDays.Should().Be(7);
        request.Config.StreakToleranceMinutes.Should().Be(90);
        request.Config.RestTimerSeconds.Should().Be(45);
        request.Config.TimerAutoPrompt.Should().BeTrue();
        request.NewUnits!.Single().Code.Should().Be("tazze");
        request.NewScales!.Single().Code.Should().Be("energia");
    }

    [Fact]
    public void A_plain_composition_writes_none_of_the_optional_brick_keys()
    {
        // Assenti = il default di oggi: i modelli salvati prima di questa onda restano identici.
        var vm = NewVm();
        vm.Name = "Semplice";
        AddField(vm, FieldNature.Quantity, "Amount");
        vm.GoalBrick.HasGoal = true;
        vm.GoalBrick.Target = 3m;
        var json = vm.BuildTemplatePayloadJsonForTest();
        json.Should().NotContain("rolling_days").And.NotContain("streak_tolerance_minutes")
            .And.NotContain("rest_timer_seconds").And.NotContain("timer_auto_prompt");
        var goal = JsonDocument.Parse(json).RootElement.GetProperty("goal");
        goal.TryGetProperty("name", out _).Should().BeFalse("a goal without its own name takes the habit's");
        goal.GetProperty("period_type").GetString().Should().Be("Open");
    }

    [Fact]
    public void Rolling_goal_needs_its_days()
    {
        var vm = NewVm();
        vm.Name = "Acqua";
        AddField(vm, FieldNature.Quantity, "Amount");
        vm.GoalBrick.HasGoal = true;
        vm.GoalBrick.Target = 2m;
        vm.CanLaunch.Should().BeTrue();

        vm.GoalBrick.PeriodType = GoalPeriodType.Rolling;
        vm.CanLaunch.Should().BeFalse("Rolling with no days violates ck_goal_period_consistency");
        vm.GoalBrick.RollingDays = 0;
        vm.CanLaunch.Should().BeFalse();
        vm.GoalBrick.RollingDays = 7;
        vm.CanLaunch.Should().BeTrue();
        vm.SaveAsTemplateCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void A_streak_tolerance_out_of_range_blocks_launch(int minutes)
    {
        var vm = NewVm();
        vm.Name = "Corsa";
        vm.Schedule.StreakToleranceMinutes = minutes;
        vm.CanLaunch.Should().BeFalse();
        vm.Schedule.StreakToleranceMinutes = 1440;
        vm.CanLaunch.Should().BeTrue();
    }


    // ===== il creatore si carica da un modello (3a, task 3) =====

    [Theory]
    [MemberData(nameof(OfficialTemplateNames))]
    public async Task Every_official_template_loads_and_composes_like_its_fixture(string name)
    {
        Strings.Culture = CultureInfo.GetCultureInfo("en-US");
        var seed = HabitTemplateSeed.All.Single(t => t.Name == name);
        var expected = BuildFixture(name).BuildRequestForTest();

        var vm = NewVm(StoreWith(seed));
        await vm.LoadTemplateAsync(seed.Id);
        var actual = vm.BuildRequestForTest();

        // Gli Id sono nuovi a ogni richiesta; unita' e scale hanno gli Id del catalogo, uguali, e si confrontano.
        actual.Should().BeEquivalentTo(expected, o => o.Excluding(m => m.Name == "Id" || m.Name == "HabitNodeId"
            || m.Name == "MetricDefinitionId" || m.Name == "Description"));
        actual.Node.Description.Should().Be(seed.Description);
    }

    [Fact]
    public async Task An_official_template_loads_in_the_interface_language()
    {
        Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
        var sleep = HabitTemplateSeed.All.Single(t => t.Name == "Sleep tracker");
        var workout = HabitTemplateSeed.All.Single(t => t.Name == "Workout log");
        var templates = StoreWith(sleep, workout);

        var s = NewVm(templates);
        await s.LoadTemplateAsync(sleep.Id);
        var sr = s.BuildRequestForTest();
        sr.Node.Name.Should().Be("Monitor del sonno");
        sr.Node.Description.Should().NotBe(sleep.Description).And.NotBeNullOrWhiteSpace();
        sr.Metrics.Select(m => m.Name).Should().Equal("Ore dormite", "Qualità del sonno");
        sr.Metrics[1].LabelsJson.Should().Be("""["Pessima","Scarsa","Discreta","Buona","Ottima"]""");
        sr.Config!.SeriesLabelsJson.Should().Be("""["Notte","Pisolino"]""");

        var w = NewVm(templates);
        await w.LoadTemplateAsync(workout.Id);
        var wr = w.BuildRequestForTest();
        wr.Node.Name.Should().Be("Registro allenamento");
        wr.Node.Description.Should().Be("Abitudine a sessioni per registrare gli allenamenti in palestra, con segmenti di lavoro e riposo.");
        wr.Metrics.Select(m => m.Name).Should().Equal("Durata", "Ripetizioni", "Peso");
        wr.Segments!.Select(x => x.Name).Should().Equal("Lavoro", "Riposo");
    }

    [Fact]
    public async Task A_user_template_loads_as_written()
    {
        // Stesso nome e stessi testi di un ufficiale, ma e' dell'utente (Id suo): in italiano resta com'e' scritto.
        Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
        var mine = new HabitTemplate
        {
            Id = Guid.NewGuid(), Name = "Sleep tracker", Description = "My own words", IsOfficial = false,
            PayloadJson = """
                {"node":{"name":"Sleep tracker","node_type":"Habit","habit_type":"Counter","color":"#112233"},
                 "config":{"schedule_type":"Daily","repeatable":true,"series_labels":["Night","Nap"],"has_integrated_timer":true,"timer_mode":"Session"},
                 "metrics":[{"name":"Hours slept","type":"Decimal","unit":"hours"},{"name":"Duration","type":"Seconds","unit":"minutes"}],
                 "segments":[{"name":"Work","is_rest":false,"sort_order":0},{"name":"Rest","is_rest":true,"sort_order":1}]}
                """,
        };
        var vm = NewVm(StoreWith(mine));
        await vm.LoadTemplateAsync(mine.Id);

        var r = vm.BuildRequestForTest();
        r.Node.Name.Should().Be("Sleep tracker");
        r.Node.Description.Should().Be("My own words");
        r.Node.Color.Should().Be("#112233");
        r.Metrics.Select(m => m.Name).Should().Equal("Hours slept", "Duration");
        r.Config!.SeriesLabelsJson.Should().Be("""["Night","Nap"]""");
        r.Segments!.Select(x => x.Name).Should().Equal("Work", "Rest");
    }

    [Fact]
    public async Task Loading_then_saving_as_template_round_trips()
    {
        // Ogni valore diverso dal default, con unita' e scala inventate nel creatore: il payload ricaricato e' quello salvato.
        var templates = new FakeHabitTemplateService();
        var first = NewVm(templates);
        first.Name = "Tutto insieme";
        first.Color = "#123456";
        first.Icon = "star";
        first.Description = "Con la mia descrizione";
        AddField(first, FieldNature.Quantity, "Tazze");
        first.BeginAddCustomUnit(first.Fields[0]);
        first.UnitForm!.Name = "Tazze";
        first.ConfirmCustomUnitCommand.Execute(null);
        AddField(first, FieldNature.Scale, "Energia");
        first.BeginAddCustomScale(first.Fields[1]);
        first.ScaleForm!.Name = "Energia";
        first.ScaleForm.LabelsText = "Bassa, Media, Alta";
        first.ConfirmCustomScaleCommand.Execute(null);
        AddField(first, FieldNature.Time, "Durata", unit: "minutes");
        first.Repeatable = true;
        AddSeriesLabel(first, "Mattina");
        AddSeriesLabel(first, "Sera");
        first.Schedule.ScheduleType = ScheduleType.SpecificDays;
        foreach (var key in new[] { "Mon", "Wed", "Fri" })
            first.Schedule.Days.Single(d => d.DayKey == key).IsSelected = true;
        first.Schedule.StreakMode = StreakMode.Weekly;
        first.Schedule.StreakRecovery = StreakRecovery.Manual;
        first.Schedule.StreakToleranceMinutes = 90;
        first.SessionBrick.HasSession = true;
        first.SessionBrick.Segments.Add(new SegmentRowViewModel { Name = "Cooldown", IsRest = true });
        first.SessionBrick.RestTimerSeconds = 45;
        first.SessionBrick.TimerAutoPrompt = true;
        first.GoalBrick.HasGoal = true;
        first.GoalBrick.Target = 5m;
        first.GoalBrick.Name = "Tazze della settimana";
        first.GoalBrick.PeriodType = GoalPeriodType.Rolling;
        first.GoalBrick.RollingDays = 7;
        await first.SaveAsTemplateCommand.ExecuteAsync(null);
        var saved = templates.Created.Single();

        var second = NewVm(templates);
        await second.LoadTemplateAsync(saved.Id);

        second.BuildTemplatePayloadJsonForTest().Should().Be(saved.PayloadJson);
        second.Description.Should().Be("Con la mia descrizione");
        var (a, b) = (first.BuildRequestForTest(), second.BuildRequestForTest());
        b.Should().BeEquivalentTo(a, o => o.Excluding(m => m.Name == "Id" || m.Name == "HabitNodeId"
            || m.Name == "MetricDefinitionId" || m.Name == "UnitOfMeasureId" || m.Name == "ScaleTemplateId"));
        b.NewUnits!.Single().Code.Should().Be("tazze");
        b.NewScales!.Single().Code.Should().Be("energia");
        b.Metrics[0].UnitOfMeasureId.Should().Be(b.NewUnits!.Single().Id);
    }

    [Fact]
    public async Task A_fixed_goal_loads_as_open_keeping_target_and_name()
    {
        var t = UserTemplate("""
            {"node":{"name":"Acqua","habit_type":"Counter"},"metrics":[{"name":"Amount","type":"Decimal","unit":"l"}],
             "goal":{"name":"Estate","target":3,"period_type":"Fixed"}}
            """);
        var vm = NewVm(StoreWith(t));
        await vm.LoadTemplateAsync(t.Id);

        vm.GoalBrick.HasGoal.Should().BeTrue();
        vm.GoalBrick.PeriodType.Should().Be(GoalPeriodType.Open);
        vm.GoalBrick.Target.Should().Be(3m);
        vm.GoalBrick.Name.Should().Be("Estate");
        vm.CanLaunch.Should().BeTrue();
    }

    [Fact]
    public async Task A_rolling_goal_without_days_loads_incomplete_and_blocks_launch()
    {
        var t = UserTemplate("""
            {"node":{"name":"Acqua","habit_type":"Counter"},"metrics":[{"name":"Amount","type":"Decimal","unit":"l"}],
             "goal":{"target":3,"period_type":"Rolling"}}
            """);
        var vm = NewVm(StoreWith(t));
        await vm.LoadTemplateAsync(t.Id);

        vm.GoalBrick.PeriodType.Should().Be(GoalPeriodType.Rolling);
        vm.GoalBrick.RollingDays.Should().BeNull();
        vm.CanLaunch.Should().BeFalse();
        // La riga che manca e' in Avanzate: il creatore lo apre e la riga del perche' la nomina.
        vm.AdvancedOpen.Should().BeTrue();
        vm.LaunchBlockedReason.Should().Be(string.Format(System.Globalization.CultureInfo.CurrentCulture,
            Recordum.Presentation.Resources.Strings.HabitBuilder_Blocked_RollingDays,
            string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.Presentation.Resources.Strings.HabitBuilder_Goal_RollingRow, "…")));
    }

    [Fact]
    public async Task A_rolling_goal_loads_its_days_into_the_window_row()
    {
        var t = UserTemplate("""
            {"node":{"name":"Acqua","habit_type":"Counter"},"metrics":[{"name":"Amount","type":"Decimal","unit":"l"}],
             "goal":{"target":3,"period_type":"Rolling","rolling_days":7}}
            """);
        var vm = NewVm(StoreWith(t));
        await vm.LoadTemplateAsync(t.Id);

        vm.GoalBrick.RollingWindowDays.Should().Be(7);
        vm.CanLaunch.Should().BeTrue();
        vm.AdvancedOpen.Should().BeFalse("nulla manca: Avanzate resta chiuso");
    }

    [Fact]
    public async Task A_unit_or_scale_the_catalogue_does_not_know_leaves_the_field_incomplete()
    {
        var t = UserTemplate("""
            {"node":{"name":"Mix","habit_type":"Counter"},
             "metrics":[{"name":"Durata","type":"Seconds","unit":"fortnights"},{"name":"Umore","type":"Integer","scale_code":"nope"}]}
            """);
        var vm = NewVm(StoreWith(t));
        await vm.LoadTemplateAsync(t.Id);

        vm.Fields.Select(f => f.Nature).Should().Equal(FieldNature.Time, FieldNature.Scale);
        vm.Fields.Should().OnlyContain(f => f.UnitOfMeasureId == null && f.ScaleTemplateId == null);
        vm.CanLaunch.Should().BeFalse();
    }

    [Fact]
    public async Task Loading_a_missing_template_says_so()
    {
        var vm = NewVm();
        var act = () => vm.LoadTemplateAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<HabitTemplateNotFoundException>();
    }

    private static HabitTemplate UserTemplate(string payload) =>
        new() { Id = Guid.NewGuid(), Name = "Mio", IsOfficial = false, PayloadJson = payload };

    private static FakeHabitTemplateService StoreWith(params HabitTemplate[] templates)
    {
        var fake = new FakeHabitTemplateService();
        foreach (var t in templates) fake.Store[t.Id] = t;
        return fake;
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
    private static HabitBuilderViewModel NewVm(FakeHabitTemplateService? templates = null)
    {
        var settings = new FakeAppSettingService();
        var vm = new HabitBuilderViewModel(new FakeComposedHabitService(), templates ?? new FakeHabitTemplateService(),
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
