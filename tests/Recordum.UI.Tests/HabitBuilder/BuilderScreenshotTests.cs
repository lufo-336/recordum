using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
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
        var w = new Window { Width = 1100, Height = 900, Content = view, RequestedThemeVariant = Theme(variant) };
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
            view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "NameBox").Text.Should().Be("Acqua");
        }
        finally { w.Close(); }
    }
}
