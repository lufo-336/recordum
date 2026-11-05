using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitBuilder;
using Xunit;

namespace Recordum.UI.Tests.HabitBuilder;

/// <summary>Onda 3a Task 5: il creatore nei cinque blocchi di spec §6.4 — Chi e', Cosa registri, Quando conta, Avanzate (chiuso)
/// a sinistra, "Come appare in Home" nella colonna di destra; in fondo Annulla · Salva come modello · Crea abitudine.</summary>
public class BuilderBlocksTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-hbblocks");
    private static void Pump() => PrivateDbHost.Pump();

    internal sealed record Opened(IServiceScope Scope, HabitBuilderViewModel Vm, HabitBuilderView View, Window Window) : IDisposable
    {
        public void Dispose() { Window.Close(); Scope.Dispose(); }
    }

    internal static async Task<Opened> OpenAsync(double width = 1100, double height = 1400)
    {
        var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>();
        await vm.InitializeAsync();
        var view = new HabitBuilderView { DataContext = vm };
        var w = new Window { Width = width, Height = height, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w);
    }

    internal static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window w, Control c)
    {
        c.BringIntoView();
        Pump();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    internal static IReadOnlyList<TextBlock> BlockTitles(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("block") && t.IsEffectivelyVisible).ToList();

    internal static double Top(Visual v, Visual root) => v.TranslatePoint(new Point(0, 0), root)!.Value.Y;
    internal static double Left(Visual v, Visual root) => v.TranslatePoint(new Point(0, 0), root)!.Value.X;

    internal static string Shown(TextBlock t) =>
        t.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(r => r.Text)) : t.Text ?? string.Empty;

    [AvaloniaFact]
    public async Task Blocks_follow_the_spec_order()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();

        var stack = Named<StackPanel>(o.View, "BlocksStack");
        BlockTitles(stack).OrderBy(t => Top(t, o.View)).Select(t => t.Text).Should().Equal(
            Strings.Editor_Block_Identity, Strings.Editor_Block_Record, Strings.Editor_Block_When, Strings.Editor_Block_Advanced);

        var host = Named<Border>(o.View, "PreviewHost");
        BlockTitles(host).Select(t => t.Text).Should().Equal(Strings.Editor_Block_Home);
        Left(host, o.View).Should().BeGreaterThanOrEqualTo(stack.Bounds.Width, "la Home e' la colonna di destra");
        host.GetVisualDescendants().OfType<BuilderPreviewPane>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public async Task Advanced_is_closed_and_holds_tolerance_rolling_segments_rest_prompt_ai()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Name = "Corsa";
        Named<Button>(o.View, "AddQuantityButton").Command!.Execute(FieldNature.Quantity);
        Pump();
        Named<ToggleButton>(o.View, "GoalToggle").IsChecked = true;
        o.Vm.GoalBrick.PeriodType = GoalPeriodType.Rolling;
        Named<ToggleButton>(o.View, "SessionToggle").IsChecked = true;
        Pump();

        var rows = Named<StackPanel>(o.View, "AdvancedRows");
        rows.IsEffectivelyVisible.Should().BeFalse("Avanzate parte chiuso");
        string[] inside = ["ToleranceBox", "RollingDaysBox", "SegmentsList", "RestBox", "AutoPromptBox", "AiSharingRow"];
        foreach (var n in inside)
            rows.GetVisualDescendants().OfType<Control>().Should().Contain(c => c.Name == n, $"{n} sta in Avanzate");

        Click(o.Window, Named<ToggleButton>(o.View, "AdvancedToggle"));
        rows.IsEffectivelyVisible.Should().BeTrue();
        foreach (var n in inside) Named<Control>(rows, n).IsEffectivelyVisible.Should().BeTrue(n);
        Named<ItemsControl>(rows, "SegmentsList").ItemCount.Should().Be(2, "la sessione nasce con Lavoro e Riposo");
        Named<StackPanel>(rows, "AiSharingRow").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)
            .Should().Contain(Strings.Editor_Summary_ComingSoon, "l'AI e' in arrivo, come in Configura");

        // Fuori da Avanzate: i blocchi di sopra non ripetono queste caselle.
        foreach (var block in new[] { "IdentityBlock", "RecordBlock", "WhenBlock" })
            Named<StackPanel>(o.View, block).GetVisualDescendants().OfType<Control>().Select(c => c.Name).Should().NotIntersectWith(inside);
    }

    [AvaloniaFact]
    public async Task Field_tile_disabled_says_why()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        var quantity = Named<Button>(o.View, "AddQuantityButton");
        quantity.IsEnabled.Should().BeTrue();
        quantity.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Should().Contain(Strings.HabitBuilder_Tile_Quantity_Hint);

        Click(o.Window, Named<CheckBox>(o.View, "CounterCheck"));
        o.Vm.CountsOccurrences.Should().BeTrue();

        quantity.IsEnabled.Should().BeFalse();
        var hint = quantity.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "QuantityHint");
        hint.Text.Should().Be(Strings.HabitBuilder_Tile_Disabled_Counter).And.Be("Non con il contatore a tocco: il tocco è già la registrazione");
        hint.IsEffectivelyVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Footer_is_cancel_save_create()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        var cancel = Named<Button>(o.View, "CancelButton");
        var save = Named<Button>(o.View, "SaveAsTemplateButton");
        var create = Named<Button>(o.View, "LaunchButton");

        cancel.Classes.Should().Contain("ghost");
        save.Classes.Should().NotContain("primary").And.NotContain("ghost");
        create.Classes.Should().Contain("primary");
        create.Content.Should().Be("Crea abitudine");
        Left(cancel, o.View).Should().BeLessThan(Left(save, o.View));
        Left(save, o.View).Should().BeLessThan(Left(create, o.View));

        // Spento, e dice perche'.
        var reason = Named<TextBlock>(o.View, "LaunchBlockedText");
        create.IsEffectivelyEnabled.Should().BeFalse();
        reason.IsEffectivelyVisible.Should().BeTrue();
        reason.Text.Should().Be(Strings.HabitBuilder_Blocked_Name);
        o.Vm.Name = "Acqua";
        Pump();
        create.IsEffectivelyEnabled.Should().BeTrue();
        reason.IsEffectivelyVisible.Should().BeFalse();

        // Un solo inchiostro per finestra, anche col modulo di un'unita' nuova aperto.
        Named<Button>(o.View, "AddQuantityButton").Command!.Execute(FieldNature.Quantity);
        Pump();
        o.View.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NewUnitButton" && b.IsEffectivelyVisible).Command!
            .Execute(o.Vm.Fields[0]);
        Pump();
        Named<Button>(o.View, "UnitFormConfirm").IsEffectivelyVisible.Should().BeTrue();
        o.View.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("primary"))
            .Should().ContainSingle().Which.Should().BeSameAs(create);
    }

    [AvaloniaFact]
    public async Task No_hex_field_no_sample_data()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Name = "Acqua";
        o.Vm.Color = "#2F7FC1";
        Pump();

        var hex = new Regex("^#?[0-9A-Fa-f]{6}$");
        o.View.GetVisualDescendants().OfType<TextBox>().Should().NotContain(t => t.Text != null && hex.IsMatch(t.Text));
        var texts = o.View.GetVisualDescendants().OfType<TextBlock>().Select(Shown).ToList();
        texts.Should().NotContain(Strings.HabitBuilder_Preview_Sample).And.NotContain("Dati d'esempio").And.NotContain("Colore (hex)");
    }

    [AvaloniaFact]
    public async Task Derived_type_line_follows_the_composition()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        var line = Named<TextBlock>(o.View, "DerivedTypeLine");
        Shown(line).Should().Be("Si comporta come azione · cambia da solo con quello che registri");
        line.Inlines!.OfType<Run>().Single(r => r.FontWeight >= Avalonia.Media.FontWeight.SemiBold).Text.Should().Be("azione");

        Click(o.Window, Named<Button>(o.View, "AddTextButton"));
        Shown(line).Should().Contain(Strings.Home_HabitType_Journal);
        line.Inlines!.OfType<Run>().Single(r => r.FontWeight >= Avalonia.Media.FontWeight.SemiBold).Text.Should().Be(Strings.Home_HabitType_Journal);
    }

    // ---- Fix round 1 -------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Block_3_has_no_period_choice()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Name = "Acqua";
        Named<Button>(o.View, "AddQuantityButton").Command!.Execute(FieldNature.Quantity);
        Named<ToggleButton>(o.View, "GoalToggle").IsChecked = true;
        Pump();

        var when = Named<StackPanel>(o.View, "WhenBlock");
        when.GetVisualDescendants().OfType<ComboBox>().Should().NotContain(c => c.Name == "GoalPeriodBox");
        when.GetVisualDescendants().OfType<ComboBox>().SelectMany(c => c.Items.Cast<object>())
            .Should().NotContain(i => i is GoalPeriodType, "il periodo dell'obiettivo non si sceglie in Quando conta");
        Named<TextBox>(when, "GoalTargetBox").IsEffectivelyVisible.Should().BeTrue();
        Named<TextBox>(when, "GoalNameBox").IsEffectivelyVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task The_window_row_in_advanced_sets_rolling_or_open()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Name = "Acqua";
        Named<Button>(o.View, "AddQuantityButton").Command!.Execute(FieldNature.Quantity);
        Click(o.Window, Named<ToggleButton>(o.View, "AdvancedToggle"));
        var box = Named<TextBox>(o.View, "RollingDaysBox");
        box.IsEffectivelyVisible.Should().BeFalse("senza obiettivo non c'e' finestra da contare");

        Named<ToggleButton>(o.View, "GoalToggle").IsChecked = true;
        o.Vm.GoalBrick.Target = 2m;
        Pump();
        box.IsEffectivelyVisible.Should().BeTrue();
        var row = (Panel)box.Parent!;
        Shown(row.Children.OfType<TextBlock>().First()).Should().Be("Conta sugli ultimi");
        row.Children.OfType<TextBlock>().Last().Text.Should().Be("giorni");
        ((Panel)row.Parent!).Children.OfType<TextBlock>().Should().Contain(t => t.Text == Strings.HabitBuilder_Goal_RollingRow_Help, "la riga che spiega");

        box.Text = "7";
        Pump();
        o.Vm.GoalBrick.PeriodType.Should().Be(GoalPeriodType.Rolling);
        o.Vm.GoalBrick.RollingDays.Should().Be(7);

        box.Text = "";
        Pump();
        o.Vm.GoalBrick.PeriodType.Should().Be(GoalPeriodType.Open);
        o.Vm.GoalBrick.RollingDays.Should().BeNull();
        Named<Button>(o.View, "LaunchButton").IsEffectivelyEnabled.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task A_loaded_rolling_goal_shows_its_days_and_a_missing_one_opens_advanced()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Name = "Acqua";
        Named<Button>(o.View, "AddQuantityButton").Command!.Execute(FieldNature.Quantity);
        // Come fa LoadTemplateAsync: periodo e giorni scritti sul mattone, Avanzate aperto quando il numero manca.
        o.Vm.GoalBrick.HasGoal = true;
        o.Vm.GoalBrick.Target = 2m;
        o.Vm.GoalBrick.PeriodType = GoalPeriodType.Rolling;
        o.Vm.GoalBrick.RollingDays = 7;
        o.Vm.AdvancedOpen = true;
        Pump();

        Named<StackPanel>(o.View, "AdvancedRows").IsEffectivelyVisible.Should().BeTrue();
        Named<ToggleButton>(o.View, "AdvancedToggle").IsChecked.Should().BeTrue();
        Named<TextBox>(o.View, "RollingDaysBox").Text.Should().Be("7");

        o.Vm.GoalBrick.RollingDays = null;
        Pump();
        Named<TextBox>(o.View, "RollingDaysBox").Text.Should().BeNullOrEmpty();
        Named<TextBlock>(o.View, "LaunchBlockedText").Text.Should().Be("Conta sugli ultimi … giorni: serve un numero sopra lo zero.");
    }

    [AvaloniaFact]
    public async Task Rejected_text_in_tolerance_blocks_creation()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Name = "Acqua";
        Click(o.Window, Named<ToggleButton>(o.View, "AdvancedToggle"));
        var create = Named<Button>(o.View, "LaunchButton");
        create.IsEffectivelyEnabled.Should().BeTrue();

        var box = Named<TextBox>(o.View, "ToleranceBox");
        box.Text = "dieci";
        Pump();
        DataValidationErrors.GetHasErrors(box).Should().BeTrue();
        create.IsEffectivelyEnabled.Should().BeFalse("la casella rossa non sparisce in silenzio");
        Named<TextBlock>(o.View, "LaunchBlockedText").Text.Should().Be("Tolleranza (minuti): non è un numero intero.");

        box.Text = "30";
        Pump();
        create.IsEffectivelyEnabled.Should().BeTrue();
        o.Vm.Schedule.StreakToleranceMinutes.Should().Be(30);

        // Vuoto e' "nessuna tolleranza", non un testo rifiutato.
        box.Text = "";
        Pump();
        DataValidationErrors.GetHasErrors(box).Should().BeFalse();
        o.Vm.Schedule.StreakToleranceMinutes.Should().BeNull();
        create.IsEffectivelyEnabled.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task A_rest_segment_says_it_is_a_break()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        Named<Button>(o.View, "AddQuantityButton").Command!.Execute(FieldNature.Quantity);
        o.Vm.SessionBrick.HasSession = true;
        o.Vm.AdvancedOpen = true;
        Pump();
        Named<ItemsControl>(o.View, "SegmentsList").GetVisualDescendants().OfType<CheckBox>().Select(c => c.Content)
            .Should().OnlyContain(c => (string)c! == "È una pausa").And.HaveCount(2);
    }
}
