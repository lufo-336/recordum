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
        IRecentColorStore? recentColors = null)
    {
        var settings = new FakeAppSettingService();
        return new(composed ?? new FakeComposedHabitService(), templates ?? new FakeHabitTemplateService(),
               refData ?? new FakeReferenceData(), new FakeClock(),
               recentColors ?? new RecentColorStore(settings),
               new FavoriteColorStore(settings),
               new FakeScreenColorPicker());
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
        var fakeTemplates = new FakeHabitTemplateService();
        var sut = NewSut(templates: fakeTemplates);
        sut.Name = "Sonno";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);
        sut.Repeatable = true;

        await sut.SaveAsTemplateCommand.ExecuteAsync(null);

        var saved = fakeTemplates.Created.Single();
        using var doc = JsonDocument.Parse(saved.PayloadJson);
        doc.RootElement.GetProperty("repeatable").GetBoolean().Should().BeTrue();
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
}
