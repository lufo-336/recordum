using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitBuilder;
using Xunit;

namespace Recordum.UI.Tests.HabitBuilder;

/// <summary>F2b-9.3: il selettore dei template e il builder con anteprima, in chiaro e in scuro. I PNG si guardano;
/// ogni schermata ha la sua asserzione strutturale (mai il PNG da solo).</summary>
public class BuilderScreenshotTests(ITestOutputHelper output)
{
    private static readonly PrivateDbHost Host = new("recordum-ui-bshots");

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static ThemeVariant Theme(string variant) => variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_template_picker_renders_in_both_themes(string variant)
    {
        using var culture = TestCulture.Pin();
        using (var scope = Host.Provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAppSettingService>().SetAsync("ui.workspace", new WorkspaceState().ToJson());
        var (window, vm) = await ShellFixture.OpenMainWindowAsync(Host.Provider.GetRequiredService<MainWindowViewModel>());
        var launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        Window? picker = null;
        launcher.WindowCreatedForTests += w => { picker = w; w.RequestedThemeVariant = Theme(variant); };
        _ = vm.Home.OpenNewHabitCommand.ExecuteAsync(null);
        try
        {
            await UntilAsync(() => picker is { IsVisible: true });
            PrivateDbHost.Pump();
            output.WriteLine(ScreenshotProbeTests.Capture(picker!, $"picker-{variant.ToLowerInvariant()}"));

            picker!.ActualThemeVariant.Should().Be(Theme(variant));
            var options = picker.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "OptionButton" && b.IsEffectivelyVisible).ToList();
            options.Should().HaveCountGreaterThanOrEqualTo(8, "i template ufficiali (e le due azioni) sono visibili");
            options.Select(o => o.Bounds.Height).Should().OnlyContain(h => h > 20, "ogni voce e' disegnata, non collassata");
        }
        finally { picker?.Close(); window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_builder_with_a_live_preview_renders_in_both_themes(string variant)
    {
        using var culture = TestCulture.Pin();
        using var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>();
        await vm.InitializeAsync();
        var view = new HabitBuilderView { DataContext = vm };
        // Lo sfondo della finestra vera (RecordumWindow): senza, il PNG in scuro usciva su nero puro.
        var frame = new Border { Child = view };
        frame[!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Brush.Background");
        var w = new Window { Width = 1100, Height = 900, Content = frame, RequestedThemeVariant = Theme(variant) };
        try
        {
            vm.Name = "Acqua";
            w.Show();
            PrivateDbHost.Pump();
            view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AddQuantityButton")
                .Command!.Execute(Recordum.Core.Enums.FieldNature.Quantity);
            PrivateDbHost.Pump();
            vm.DerivedType.Should().Be(Recordum.Core.Enums.HabitType.Counter);
            output.WriteLine(ScreenshotProbeTests.Capture(w, $"builder-{variant.ToLowerInvariant()}"));

            w.ActualThemeVariant.Should().Be(Theme(variant));
            var host = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PreviewHost");
            var pane = host.Child.Should().BeOfType<BuilderPreviewPane>().Subject;
            pane.IsEffectivelyVisible.Should().BeTrue();
            pane.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "Acqua", "l'anteprima porta il nome scritto");
            var row = pane.GetVisualDescendants().OfType<Recordum.UI.Controls.RowPreview>().Should().ContainSingle("la riga d'anteprima nuova (2b)").Subject;
            row.Model.Should().BeSameAs(vm.Preview!.HomeRow);
            row.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Tint").IsEffectivelyVisible.Should().BeTrue();
            view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "NameBox").Text.Should().Be("Acqua");
        }
        finally { w.Close(); }
    }

    // ---- Onda 3a Task 5: i cinque blocchi ---------------------------------------------------------------

    private static async Task<(IServiceScope Scope, HabitBuilderViewModel Vm, HabitBuilderView View, Window Window)> OpenFramedAsync(
        string variant, double width, double height)
    {
        var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>();
        await vm.InitializeAsync();
        var view = new HabitBuilderView { DataContext = vm };
        var frame = new Border { Child = view };
        frame[!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Brush.Background");
        var w = new Window { Width = width, Height = height, Content = frame, RequestedThemeVariant = Theme(variant) };
        w.Show();
        PrivateDbHost.Pump();
        return (scope, vm, view, w);
    }

    private static void Settle()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        PrivateDbHost.Pump();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_creator_shows_the_five_blocks(string variant)
    {
        using var culture = TestCulture.Pin();
        var (scope, vm, view, w) = await OpenFramedAsync(variant, 1100, 1000);
        try
        {
            vm.Name = "Acqua";
            vm.AddFieldCommand.Execute(Recordum.Core.Enums.FieldNature.Quantity);
            vm.Fields[0].Name = "Bicchieri";
            Settle();
            output.WriteLine(ScreenshotProbeTests.Capture(w, $"builder-blocks-{variant.ToLowerInvariant()}"));

            var stack = BuilderBlocksTests.Named<StackPanel>(view, "BlocksStack");
            BuilderBlocksTests.BlockTitles(stack).OrderBy(t => BuilderBlocksTests.Top(t, view)).Select(t => t.Text).Should().Equal(
                Strings.Editor_Block_Identity, Strings.Editor_Block_Record, Strings.Editor_Block_When, Strings.Editor_Block_Advanced);
            var home = BuilderBlocksTests.BlockTitles(BuilderBlocksTests.Named<Border>(view, "PreviewHost")).Should().ContainSingle().Subject;
            home.Text.Should().Be(Strings.Editor_Block_Home);
            BuilderBlocksTests.Left(home, view).Should().BeGreaterThan(stack.Bounds.Right, "la Home e' la colonna di destra");
            foreach (var n in new[] { "AddQuantityButton", "AddTimeButton", "AddScaleButton", "AddTextButton" })
                BuilderBlocksTests.Named<Button>(view, n).Bounds.Height.Should().BeGreaterThan(40, $"{n}: la tessera porta titolo e riga");
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_creator_advanced_block_opens_on_its_fields(string variant)
    {
        using var culture = TestCulture.Pin();
        var (scope, vm, view, w) = await OpenFramedAsync(variant, 1100, 900);
        try
        {
            vm.Name = "Corsa";
            vm.AddFieldCommand.Execute(Recordum.Core.Enums.FieldNature.Quantity);
            vm.GoalBrick.HasGoal = true;
            vm.GoalBrick.PeriodType = Recordum.Core.Enums.GoalPeriodType.Rolling;
            vm.SessionBrick.HasSession = true;
            Settle();
            BuilderBlocksTests.Named<Avalonia.Controls.Primitives.ToggleButton>(view, "AdvancedToggle").IsChecked = true;
            Settle();
            var rows = BuilderBlocksTests.Named<StackPanel>(view, "AdvancedRows");
            BuilderBlocksTests.Named<ScrollViewer>(view, "BlocksScroll").ScrollToEnd();
            Settle();
            output.WriteLine(ScreenshotProbeTests.Capture(w, $"builder-advanced-{variant.ToLowerInvariant()}"));

            foreach (var n in new[] { "ToleranceBox", "RollingDaysBox", "SegmentsList", "RestBox", "AutoPromptBox", "AiSharingRow" })
            {
                var c = BuilderBlocksTests.Named<Control>(rows, n);
                c.IsEffectivelyVisible.Should().BeTrue(n);
                c.Bounds.Height.Should().BeGreaterThan(0, n);
            }
            BuilderBlocksTests.Named<Avalonia.Controls.Shapes.Path>(view, "AdvancedChevron").RenderTransform.Should().NotBeNull("la freccia gira");
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_narrow_creator_puts_the_preview_under_the_blocks(string variant)
    {
        using var culture = TestCulture.Pin();
        var (scope, vm, view, w) = await OpenFramedAsync(variant, 600, 1000);
        try
        {
            vm.Name = "Acqua";
            vm.AddFieldCommand.Execute(Recordum.Core.Enums.FieldNature.Quantity);
            Settle();
            output.WriteLine(ScreenshotProbeTests.Capture(w, $"builder-narrow-{variant.ToLowerInvariant()}"));

            var host = BuilderBlocksTests.Named<Border>(view, "PreviewHost");
            host.IsEffectivelyVisible.Should().BeTrue();
            BuilderBlocksTests.Top(host, view).Should().BeGreaterThan(
                BuilderBlocksTests.Top(BuilderBlocksTests.Named<StackPanel>(view, "AdvancedBlock"), view), "l'anteprima scende sotto i blocchi");
            var overflow = view.GetVisualDescendants().OfType<Control>()
                .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0)
                .Select(c => (c, right: c.TranslatePoint(new Avalonia.Point(c.Bounds.Width, 0), view)!.Value.X))
                .Where(x => x.right > 600.5)
                .Select(x => $"{x.c.GetType().Name}#{x.c.Name} → {x.right:F0}")
                .ToList();
            overflow.Should().BeEmpty("a 600 px nessun controllo esce dal bordo destro");
        }
        finally { w.Close(); scope.Dispose(); }
    }
}
