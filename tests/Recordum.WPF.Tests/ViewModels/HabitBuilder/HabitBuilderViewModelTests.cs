using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitBuilder;

public class HabitBuilderViewModelTests
{
    private static HabitBuilderViewModel NewSut(
        IComposedHabitService? composed = null,
        IHabitTemplateService? templates = null,
        IReferenceDataQueryService? refData = null,
        IRecentColorStore? recentColors = null,
        IHabitUiPrefsService? uiPrefs = null)
    {
        var settings = new FakeAppSettingService();
        return new(composed ?? new FakeComposedHabitService(), templates ?? new FakeHabitTemplateService(),
               refData ?? new FakeReferenceData(), new FakeClock(),
               recentColors ?? new RecentColorStore(settings),
               new FavoriteColorStore(settings),
               new FakeScreenColorPicker(),
               uiPrefs ?? new FakeHabitUiPrefsService());
    }

    // Alias for plan compatibility — same as NewSut()
    private static HabitBuilderViewModel CreateVm() => NewSut();

    [WpfFact]
    public async Task InitializeAsync_loads_recent_colors()
    {
        var store = new RecentColorStore(new FakeAppSettingService());
        await store.AddAsync("#123456");
        var sut = NewSut(recentColors: store);

        await sut.InitializeAsync();

        sut.RecentColors.Should().Contain("#123456");
    }

    [WpfFact]
    public async Task Launch_records_the_picked_color_as_recent()
    {
        var settings = new FakeAppSettingService();
        var store = new RecentColorStore(settings);
        var sut = NewSut(recentColors: store);
        sut.Name = "Corsa";
        sut.Color = "#abcdef";
        // Use Quantity without unit (now launchable per spec §3.1)
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.LaunchCommand.ExecuteAsync(null);

        (await store.GetAsync()).Should().Contain("#abcdef");
    }

    [WpfFact]
    public async Task Launch_does_not_crash_when_recording_recent_color_fails()
    {
        // Recording the picked color is a best-effort MRU side effect; a transient DB hiccup there
        // must not crash the app or undo the habit that was already created. Regression for B-dbLock.
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake, recentColors: new ThrowingRecentColorStore());
        sut.Name = "Corsa";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.LaunchCommand.ExecuteAsync(null); // must not throw

        sut.CreatedHabitNodeId.Should().Be(fake.LastRequest!.Node.Id);
    }

    private sealed class ThrowingRecentColorStore : IRecentColorStore
    {
        public ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask AddAsync(string hex, CancellationToken ct = default)
            => throw new InvalidOperationException("database is locked");
    }

    [WpfFact]
    public void Counter_block_derives_counter_and_locks_palette()
    {
        var vm = CreateVm();
        vm.FieldsAllowed.Should().BeTrue(); // palette enabled on an empty canvas
        vm.CountsOccurrences = true;
        vm.DerivedType.Should().Be(HabitType.Counter);
        vm.CanAddField(FieldNature.Quantity).Should().BeFalse();
        vm.FieldsAllowed.Should().BeFalse(); // bare counter disables the field palette
        vm.Preview.IsTapCounter.Should().BeTrue();

        vm.CountsOccurrences = false;
        vm.FieldsAllowed.Should().BeTrue(); // re-enabled when the tally block is cleared
    }

    [WpfFact]
    public void Adding_fields_makes_a_counter_with_series_preview_without_the_tally_block()
    {
        var vm = CreateVm();
        vm.AddFieldCommand.Execute(FieldNature.Quantity);
        vm.AddFieldCommand.Execute(FieldNature.Time);

        vm.Fields.Should().HaveCount(2);               // multiple blocks coexist
        vm.DerivedType.Should().Be(HabitType.Counter); // auto-derived, no tally checkbox needed
        vm.Preview.IsTapCounter.Should().BeFalse();    // shows series rows, not the +1 mock
        vm.Preview.HasAddSeries.Should().BeTrue();
        vm.Preview.Rows.Should().ContainSingle()
            .Which.CellNames.Should().HaveCount(2);
    }

    [WpfFact]
    public void Abstinence_derives_resistance()
    {
        var vm = CreateVm();
        vm.Abstinence = true;
        vm.AddFieldCommand.Execute(FieldNature.Quantity);
        vm.DerivedType.Should().Be(HabitType.Resistance);
    }

    [WpfFact]
    public void Quantity_without_unit_is_integer_and_launchable()
    {
        var vm = CreateVm();
        vm.Name = "Lettura";
        vm.AddFieldCommand.Execute(FieldNature.Quantity);
        vm.CanLaunch.Should().BeTrue(); // unità opzionale (spec §3.1)
        vm.Fields[0].MetricType.Should().Be(MetricType.Integer);
        vm.Fields[0].UnitOfMeasureId = Guid.NewGuid();
        vm.Fields[0].MetricType.Should().Be(MetricType.Decimal);
    }

    [WpfFact]
    public void Counter_with_fields_preview_shows_add_series()
    {
        var vm = CreateVm();
        vm.AddFieldCommand.Execute(FieldNature.Quantity);
        vm.Preview.HasAddSeries.Should().BeTrue();
        vm.Preview.Rows.Should().HaveCount(1);
    }

    [WpfFact]
    public async Task SaveAsTemplate_sets_saved_feedback_then_clears_on_change()
    {
        var sut = NewSut();
        sut.Name = "Flessioni";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.TemplateSaved.Should().BeFalse();

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);
        sut.TemplateSaved.Should().BeTrue();

        sut.Name = "Flessioni avanti"; // any edit invalidates the "saved" badge
        sut.TemplateSaved.Should().BeFalse();
    }

    [WpfFact]
    public async Task SaveAsTemplate_is_blocked_while_a_time_field_has_no_unit()
    {
        // R7: a template is a saved composition, and InstantiateAsync materialises it with no gate of
        // its own. Saving one the builder itself refuses to launch just moves the malformed object
        // downstream, where it becomes a habit with a unit-less duration — the very thing CanLaunch
        // exists to prevent. The guard is inside the method too, not only on CanExecute, because
        // ExecuteAsync does not consult CanExecute.
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        await sut.InitializeAsync();
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Time);

        sut.SaveAsTemplateCommand.CanExecute(null).Should().BeFalse();
        await sut.SaveAsTemplateCommand.ExecuteAsync(null);
        fakeTemplates.Created.Should().BeEmpty();

        sut.Fields[0].UnitOfMeasureId = sut.AvailableTimeUnits.Single(u => u.Code == "minutes").Id;

        sut.SaveAsTemplateCommand.CanExecute(null).Should().BeTrue();
        await sut.SaveAsTemplateCommand.ExecuteAsync(null);
        fakeTemplates.Created.Should().ContainSingle();
    }

    [WpfFact]
    public void Text_field_blocks_count_and_makes_journal()
    {
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Text);
        sut.CanAddField(FieldNature.Count).Should().BeFalse();
        sut.DerivedType.Should().Be(HabitType.Journal);
    }

    [WpfFact]
    public async Task Launch_persists_via_composed_service_with_derived_type()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        // Quantity does NOT require unit for CanLaunch (spec §3.1) — launch without setting unit

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.Node.HabitType.Should().Be(HabitType.Counter);
        fake.LastRequest.Metrics.Should().ContainSingle();
        sut.CreatedHabitNodeId.Should().Be(fake.LastRequest.Node.Id);
    }

    [WpfFact]
    public async Task Launch_includes_default_config_with_correct_habit_node_id()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Test Habit";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.Config.Should().NotBeNull();
        fake.LastRequest.Config!.HabitNodeId.Should().Be(fake.LastRequest.Node.Id);
    }

    [WpfFact]
    public async Task SaveAsTemplate_creates_custom_template_with_roundtrippable_payload()
    {
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Flessioni";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        saved.IsOfficial.Should().BeFalse();
        saved.Name.Should().Be("Flessioni");
        saved.PayloadJson.Should().NotBe("{}");

        // Round-trip: the payload must match the shape HabitTemplateService.InstantiateAsync reads
        // (snake_case keys: node.habit_type + metrics[].name/type). Quantity → Counter.
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        var root = doc.RootElement;
        root.GetProperty("node").GetProperty("habit_type").GetString().Should().Be("Counter");
        var metrics = root.GetProperty("metrics");
        metrics.GetArrayLength().Should().Be(1);
        metrics[0].GetProperty("name").GetString().Should().Be(Strings.HabitBuilder_DefaultField_Quantity); // check via parsed JSON (not raw)
        metrics[0].GetProperty("type").GetString().Should().Be("Integer"); // no unit → Integer
    }

    [WpfFact]
    public void Picking_a_unit_changes_metric_type_and_notifies_launch()
    {
        var sut = NewSut();
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.CanLaunch.Should().BeTrue(); // quantity without unit is now launchable
        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.Fields[0].UnitOfMeasureId = Guid.NewGuid();

        // MetricType changes from Integer to Decimal when unit is set
        sut.Fields[0].MetricType.Should().Be(MetricType.Decimal);
        // CanExecuteChanged was fired (because OnFieldChanged is called)
        fired.Should().BeTrue();
    }

    [WpfFact]
    public void Picking_a_scale_re_enables_the_launch_command()
    {
        var sut = NewSut();
        sut.Name = "Umore";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        sut.CanLaunch.Should().BeFalse(); // scale still requires template
        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.Fields[0].ScaleTemplateId = Guid.NewGuid();

        fired.Should().BeTrue();
        sut.LaunchCommand.CanExecute(null).Should().BeTrue();
    }

    [WpfFact]
    public void CanEnableCounter_is_false_when_fields_present()
    {
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        sut.CanEnableCounter.Should().BeFalse();
    }

    [WpfFact]
    public void CanEnableAbstinence_is_false_when_text_field_present()
    {
        var sut = NewSut();
        sut.AddFieldCommand.Execute(FieldNature.Text);

        sut.CanEnableAbstinence.Should().BeFalse();
    }

    [WpfFact]
    public async Task InitializeAsync_loads_units_and_scales_for_pickers()
    {
        var refData = new FakeReferenceData();
        var sut = NewSut(refData: refData);

        await sut.InitializeAsync();

        sut.AvailableUnits.Should().BeEquivalentTo(refData.Units);
        sut.AvailableScales.Should().BeEquivalentTo(refData.Scales);
    }

    [WpfFact]
    public async Task InitializeAsync_populates_time_units_subset()
    {
        var refData = new FakeReferenceData();
        var sut = NewSut(refData: refData);

        await sut.InitializeAsync();

        sut.AvailableTimeUnits.Select(u => u.Code)
            .Should().BeEquivalentTo(new[] { "hours", "minutes", "seconds" });
    }

    [WpfFact]
    public async Task Launch_persists_picked_time_unit_on_the_metric()
    {
        var refData = new FakeReferenceData();
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake, refData: refData);
        await sut.InitializeAsync();
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Time);
        var minutes = sut.AvailableTimeUnits.Single(u => u.Code == "minutes");
        sut.Fields[0].UnitOfMeasureId = minutes.Id;

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest!.Metrics.Single().UnitOfMeasureId.Should().Be(minutes.Id);
    }

    [WpfFact]
    public async Task Template_payload_writes_unit_code_for_quantity_field()
    {
        var refData = new FakeReferenceData();
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates, refData: refData);
        await sut.InitializeAsync();
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Fields[0].UnitOfMeasureId = refData.Units[0].Id;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        var metric = doc.RootElement.GetProperty("metrics")[0];
        metric.GetProperty("unit").GetString().Should().Be(refData.Units[0].Code);
    }

    [WpfFact]
    public async Task Template_payload_writes_scale_code_for_scale_field()
    {
        var refData = new FakeReferenceData();
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates, refData: refData);
        await sut.InitializeAsync();
        sut.Name = "Umore";
        sut.AddFieldCommand.Execute(FieldNature.Scale);
        sut.Fields[0].ScaleTemplateId = refData.Scales[0].Id;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        var metric = doc.RootElement.GetProperty("metrics")[0];
        metric.GetProperty("scale_code").GetString().Should().Be(refData.Scales[0].Code);
    }

    [WpfFact]
    public void Repeatable_flows_into_the_launched_config_and_template_payload()
    {
        var vm = CreateVm();
        vm.Name = "Sonno";
        vm.Repeatable = true;

        var request = vm.BuildRequestForTest();
        request.Config!.Repeatable.Should().BeTrue();
    }

    [WpfFact]
    public async Task Repeatable_writes_into_the_template_payload_when_true()
    {
        // Updated by Task 3 (wave 2.4): this test used to assert a root-level "repeatable" key, which
        // is exactly the pre-existing bug the task fixes — HabitTemplateService only ever read
        // config.repeatable, so a template saved under the old shape silently lost the flag on
        // re-instantiation. The assertion now matches the shape the service actually reads.
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Sonno";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        doc.RootElement.GetProperty("config").GetProperty("repeatable").GetBoolean().Should().BeTrue();
    }

    [WpfFact]
    public async Task SaveAsTemplate_nests_repeatable_under_config_so_instantiation_reads_it()
    {
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Sonno";
        sut.CountsOccurrences = true;
        sut.Repeatable = true;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var json = fakeTemplates.Created.Single().PayloadJson;
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("config").GetProperty("repeatable").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("repeatable", out _).Should().BeFalse("il servizio legge solo config.repeatable");
    }

    [WpfFact]
    public async Task SaveAsTemplate_writes_the_journal_gesture_for_a_journal_composition()
    {
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Diario alimentare";
        sut.AddFieldCommand.Execute(FieldNature.Text);          // una composizione di solo testo -> Journal
        sut.JournalGesture = JournalPrimaryGesture.QuickNote;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var json = fakeTemplates.Created.Single().PayloadJson;
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ui_prefs").GetProperty("journal_gesture").GetString().Should().Be("QuickNote");
    }

    [WpfFact]
    public async Task SaveAsTemplate_sets_saved_feedback_then_clears_on_journal_gesture_change()
    {
        // JournalGesture writes into ui_prefs (a real payload field), so it must invalidate the
        // "Saved" badge on change just like Repeatable/Color/Icon do — otherwise the badge lies
        // about a composition that no longer matches what was saved.
        var sut = NewSut();
        sut.Name = "Diario alimentare";
        sut.AddFieldCommand.Execute(FieldNature.Text); // -> Journal
        sut.JournalGesture = JournalPrimaryGesture.QuickNote;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);
        sut.TemplateSaved.Should().BeTrue();

        sut.JournalGesture = JournalPrimaryGesture.Diary;
        sut.TemplateSaved.Should().BeFalse();
    }

    [WpfFact]
    public async Task Launch_applies_the_journal_gesture_to_the_created_habit()
    {
        // I3: the gesture knob previously only ever flowed into a SAVED TEMPLATE's payload — Lancia
        // itself ignored it, so composing a text-only habit, picking "Quick note with photo" and
        // clicking Lancia still produced the Markdown editor. This asserts the habit created by
        // Lancia (not a re-instantiated template) carries the gesture.
        var fake = new FakeComposedHabitService();
        var prefs = new FakeHabitUiPrefsService();
        var sut = NewSut(composed: fake, uiPrefs: prefs);
        sut.Name = "Diario alimentare";
        sut.AddFieldCommand.Execute(FieldNature.Text); // -> Journal
        sut.JournalGesture = JournalPrimaryGesture.QuickNote;

        await sut.LaunchCommand.ExecuteAsync(null);

        sut.CreatedHabitNodeId.Should().NotBeNull();
        (await prefs.GetAsync(sut.CreatedHabitNodeId!.Value)).JournalGesture
            .Should().Be(JournalPrimaryGesture.QuickNote);
    }

    [WpfFact]
    public async Task Launch_does_not_touch_ui_prefs_for_a_non_journal_habit()
    {
        var fake = new FakeComposedHabitService();
        var prefs = new FakeHabitUiPrefsService();
        var sut = NewSut(composed: fake, uiPrefs: prefs);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity); // -> Counter

        await sut.LaunchCommand.ExecuteAsync(null);

        prefs.Store.Should().BeEmpty();
    }

    [WpfFact]
    public void Journal_gesture_knob_is_offered_only_for_journal_compositions()
    {
        var sut = CreateVm();
        sut.CountsOccurrences = true;               // -> Counter
        sut.ShowJournalGesture.Should().BeFalse();

        sut.CountsOccurrences = false;
        sut.AddFieldCommand.Execute(FieldNature.Text);             // -> Journal
        sut.ShowJournalGesture.Should().BeTrue();
    }

    [WpfFact]
    public async Task Repeatable_is_omitted_from_the_template_payload_when_false()
    {
        // Byte-stability: old payloads (saved before this task) must round-trip unchanged —
        // Repeatable defaults false and WhenWritingNull drops it, same as CountsOccurrences.
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Sonno";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        doc.RootElement.TryGetProperty("repeatable", out _).Should().BeFalse();
    }

    [WpfFact]
    public void A_time_field_without_a_unit_blocks_launch()
    {
        // A duration with no unit has nothing to render its totals in: the day total, the goal and
        // the export all fall back to raw seconds. The unit is required at creation, mirroring the
        // rule a Scale field already enforces.
        var sut = NewSut();
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Time);

        sut.CanLaunch.Should().BeFalse();
    }

    [WpfFact]
    public void Schedule_brick_writes_specific_days_into_the_launched_config()
    {
        // Onda 2.5 T3: the Schedule brick is a view over the draft, applied by BuildConfig — this
        // is the host-side half of ScheduleBrickViewModelTests.ApplyTo_writes_specific_days_...
        var vm = CreateVm();
        vm.Name = "Allenamento";
        vm.Schedule.ScheduleType = ScheduleType.SpecificDays;
        vm.Schedule.Days.Single(d => d.DayKey == "Mon").IsSelected = true;
        vm.Schedule.Days.Single(d => d.DayKey == "Fri").IsSelected = true;

        var request = vm.BuildRequestForTest();

        request.Config!.ScheduleDaysJson.Should().Be("[\"Mon\",\"Fri\"]");
    }

    [WpfFact]
    public async Task Schedule_brick_changes_clear_the_saved_template_badge()
    {
        // Onda 2.5 T3: HabitBuilderViewModel wires Schedule.Changed => TemplateSaved = false in its
        // constructor — this is a task constraint (BuildConfig applies Schedule to the launched
        // HabitConfig) with no dedicated test of its own until now, same as the other draft-mutation
        // sites already covered above (Name/Repeatable/JournalGesture).
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);
        sut.TemplateSaved.Should().BeTrue();

        sut.Schedule.StreakRecovery = StreakRecovery.None;

        sut.TemplateSaved.Should().BeFalse();
    }

    [WpfFact]
    public void GoalBrick_becomes_available_for_a_quantity_field_counter()
    {
        // Onda 2.5 T4: IsAvailable = DerivedType == Counter, recalculated by Recompute().
        var sut = CreateVm();
        sut.GoalBrick.IsAvailable.Should().BeFalse(); // empty canvas -> Action, not yet available

        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        sut.DerivedType.Should().Be(HabitType.Counter);
        sut.GoalBrick.IsAvailable.Should().BeTrue();
    }

    [WpfFact]
    public void GoalBrick_is_available_for_a_bare_tap_counter()
    {
        // Water intake precedent: a tap counter (CountsOccurrences) has no metric field of its own
        // but is still a Counter, and can still carry a goal.
        var sut = CreateVm();
        sut.CountsOccurrences = true;

        sut.GoalBrick.IsAvailable.Should().BeTrue();
    }

    [WpfFact]
    public void GoalBrick_is_forced_off_when_the_composition_stops_supporting_it()
    {
        // The brick switches itself off (never an orphaned goal in the draft) when the composition
        // no longer derives Counter — e.g. switching the tap counter to Abstinence.
        var sut = CreateVm();
        sut.CountsOccurrences = true; // -> Counter, tap counter
        sut.GoalBrick.HasGoal = true;
        sut.GoalBrick.Target = 3m;

        sut.CountsOccurrences = false;
        sut.Abstinence = true; // -> Resistance

        sut.DerivedType.Should().Be(HabitType.Resistance);
        sut.GoalBrick.IsAvailable.Should().BeFalse();
        sut.GoalBrick.HasGoal.Should().BeFalse();
    }

    [WpfFact]
    public void An_incomplete_goal_blocks_CanLaunch()
    {
        var sut = CreateVm();
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.GoalBrick.HasGoal = true; // target left null

        sut.CanLaunch.Should().BeFalse();

        sut.GoalBrick.Target = 2m;
        sut.CanLaunch.Should().BeTrue();

        sut.GoalBrick.Target = 0m; // not positive -> still blocked
        sut.CanLaunch.Should().BeFalse();
    }

    [WpfFact]
    public async Task Launch_is_blocked_internally_when_the_goal_is_incomplete()
    {
        // ExecuteAsync does not consult CanExecute (R7 precedent) — the guard must live inside
        // LaunchAsync too, not only on the greyed-out button.
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.GoalBrick.HasGoal = true;

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest.Should().BeNull();
        sut.CreatedHabitNodeId.Should().BeNull();
    }

    [WpfFact]
    public async Task Launch_persists_a_complete_goal_bound_to_the_habit_name_and_first_metric()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.GoalBrick.HasGoal = true;
        sut.GoalBrick.Target = 2m;

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.Goal.Should().NotBeNull();
        fake.LastRequest.Goal!.Name.Should().Be("Bevi acqua");
        fake.LastRequest.Goal.HabitNodeId.Should().Be(fake.LastRequest.Node.Id);
        fake.LastRequest.Goal.MetricDefinitionId.Should().Be(fake.LastRequest.Metrics.Single().Id);
        fake.LastRequest.Goal.TargetValue.Should().Be(2m);
    }

    [WpfFact]
    public async Task Launch_omits_the_goal_when_the_brick_is_off()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest!.Goal.Should().BeNull();
    }

    [WpfFact]
    public async Task GoalBrick_changes_clear_the_saved_template_badge_and_launch_state()
    {
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Bevi acqua";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);
        sut.TemplateSaved.Should().BeTrue();

        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.GoalBrick.HasGoal = true;

        sut.TemplateSaved.Should().BeFalse();
        fired.Should().BeTrue(); // CanLaunch now depends on the (incomplete) goal
    }

    [WpfFact]
    public async Task Picking_a_time_unit_re_enables_the_launch_command()
    {
        var sut = NewSut();
        await sut.InitializeAsync();
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Time);
        sut.CanLaunch.Should().BeFalse();
        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.Fields[0].UnitOfMeasureId = sut.AvailableTimeUnits.Single(u => u.Code == "minutes").Id;

        fired.Should().BeTrue();
        sut.CanLaunch.Should().BeTrue();
        sut.LaunchCommand.CanExecute(null).Should().BeTrue();
    }

    [WpfFact]
    public void SessionBrick_is_available_for_a_quantity_field_counter_but_not_for_a_tap_counter()
    {
        // Onda 2.5 T5: IsAvailable = DerivedType == Counter && !CountsOccurrences — the session needs
        // fields to record per segment (Workout log is the precedent); a bare tap counter has none.
        var sut = CreateVm();
        sut.SessionBrick.IsAvailable.Should().BeFalse(); // empty canvas -> Action, not yet available

        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.SessionBrick.IsAvailable.Should().BeTrue();

        sut.RemoveFieldCommand.Execute(sut.Fields[0]);
        sut.CountsOccurrences = true; // bare tap counter -> still Counter, but no fields to segment
        sut.SessionBrick.IsAvailable.Should().BeFalse();
    }

    [WpfFact]
    public void SessionBrick_is_forced_off_when_the_composition_stops_supporting_it()
    {
        var sut = CreateVm();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.SessionBrick.HasSession = true;
        sut.SessionBrick.Segments.Should().NotBeEmpty();

        sut.RemoveFieldCommand.Execute(sut.Fields[0]);
        sut.Abstinence = true; // -> Resistance

        sut.DerivedType.Should().Be(HabitType.Resistance);
        sut.SessionBrick.IsAvailable.Should().BeFalse();
        sut.SessionBrick.HasSession.Should().BeFalse();
        sut.SessionBrick.Segments.Should().BeEmpty();
    }

    [WpfFact]
    public async Task Launch_persists_segments_and_timer_flags_when_the_session_brick_is_on()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Workout";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.SessionBrick.HasSession = true;

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.Segments.Should().NotBeNull();
        fake.LastRequest.Segments!.Select(s => (s.Name, s.IsRest, s.SortOrder)).Should()
            .Equal(("Work", false, 0), ("Rest", true, 1));
        fake.LastRequest.Segments!.All(s => s.HabitNodeId == fake.LastRequest.Node.Id).Should().BeTrue();
        fake.LastRequest.Config!.HasIntegratedTimer.Should().BeTrue();
        fake.LastRequest.Config.TimerMode.Should().Be(TimerMode.Session);
        fake.LastRequest.Config.SessionBaseTimer.Should().BeTrue();
    }

    [WpfFact]
    public async Task Launch_omits_segments_and_timer_flags_when_the_session_brick_is_off()
    {
        var fake = new FakeComposedHabitService();
        var sut = NewSut(composed: fake);
        sut.Name = "Corsa";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        await sut.LaunchCommand.ExecuteAsync(null);

        fake.LastRequest!.Segments.Should().BeNull();
        fake.LastRequest.Config!.HasIntegratedTimer.Should().BeFalse();
        fake.LastRequest.Config.TimerMode.Should().BeNull();
        fake.LastRequest.Config.SessionBaseTimer.Should().BeFalse();
    }

    // ===== Onda 2.5 T6 — serie nominate nel mattone Ripetibile =====

    [WpfFact]
    public void Series_labels_reach_the_config_as_a_json_array()
    {
        var sut = CreateVm();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[0].Name = "Notte";
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[1].Name = "Riposino";

        sut.BuildRequestForTest().Config!.SeriesLabelsJson.Should().Be("""["Notte","Riposino"]""");
    }

    [WpfFact]
    public void Series_labels_are_null_when_none_are_named()
    {
        var sut = CreateVm();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;
        sut.AddSeriesLabelCommand.Execute(null); // added but left blank

        sut.BuildRequestForTest().Config!.SeriesLabelsJson.Should().BeNull();
    }

    [WpfFact]
    public void Series_labels_do_not_travel_without_the_repeatable_flag()
    {
        // The labels name the entries of a repeatable habit; on a non-repeatable one there is a single
        // entry a day and nothing to name, so they stay out of the config even if the list is filled.
        var sut = CreateVm();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[0].Name = "Notte";
        sut.Repeatable = false;

        sut.BuildRequestForTest().Config!.SeriesLabelsJson.Should().BeNull();
    }

    [WpfFact]
    public void Blank_series_labels_are_dropped_and_names_are_trimmed()
    {
        var sut = CreateVm();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[0].Name = "  Notte  ";
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[1].Name = "   ";

        sut.BuildRequestForTest().Config!.SeriesLabelsJson.Should().Be("""["Notte"]""");
    }

    [WpfFact]
    public void Removing_a_series_label_takes_it_out_of_the_config()
    {
        var sut = CreateVm();
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[0].Name = "Notte";
        sut.AddSeriesLabelCommand.Execute(null);
        sut.SeriesLabels[1].Name = "Riposino";

        sut.RemoveSeriesLabelCommand.Execute(sut.SeriesLabels[0]);

        sut.BuildRequestForTest().Config!.SeriesLabelsJson.Should().Be("""["Riposino"]""");
    }

    [WpfFact]
    public void Editing_a_series_label_clears_the_saved_template_badge()
    {
        var sut = CreateVm();
        sut.Repeatable = true;
        sut.AddSeriesLabelCommand.Execute(null);
        sut.TemplateSaved = true;

        sut.SeriesLabels[0].Name = "Notte";

        sut.TemplateSaved.Should().BeFalse();
    }

    [WpfFact]
    public void Adding_a_series_label_clears_the_saved_template_badge()
    {
        var sut = CreateVm();
        sut.Repeatable = true;
        sut.TemplateSaved = true;

        sut.AddSeriesLabelCommand.Execute(null);

        sut.TemplateSaved.Should().BeFalse();
    }
}
