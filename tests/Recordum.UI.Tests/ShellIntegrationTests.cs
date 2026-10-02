using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Workspace;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>La prova d'insieme: contenitore vero, database SQLCipher vero, abitudini seminate dai
/// servizi veri, finestra vera. Tutto quello che sta sotto e' gia' provato a pezzi; qui si prova che
/// i pezzi si parlano.</summary>
public class ShellIntegrationTests(ITestOutputHelper output)
{
    private static async Task<MainWindow> OpenSeededAsync()
    {
        await ShellFixture.Shared.SeedAsync();
        var (window, _) = await ShellFixture.OpenMainWindowAsync(ShellFixture.Shared.NewMainWindowViewModel());
        return window;
    }

    [AvaloniaFact]
    public async Task The_window_shows_one_realized_row_per_seeded_habit()
    {
        var window = await OpenSeededAsync();
        var vm = (MainWindowViewModel)window.DataContext!;

        vm.Home.Rows.Should().HaveCountGreaterThanOrEqualTo(ShellFixture.SeededNodeCount);

        var tree = window.GetVisualDescendants().OfType<ItemsControl>()
            .Single(c => c.Name == "RowsHost");
        tree.GetRealizedContainers().Should().HaveCount(vm.Home.Roots.Count,
            "ogni radice dell'albero deve avere il suo contenitore realizzato a schermo, non solo "
            + "una riga nel ViewModel");
        vm.Home.Roots.Should().NotBeEmpty();
    }

    [AvaloniaFact]
    public async Task Selecting_a_row_opens_a_tab_and_builds_a_habit_screen()
    {
        var window = await OpenSeededAsync();
        var vm = (MainWindowViewModel)window.DataContext!;
        var row = vm.Home.Rows.First(r => !r.IsGroup);

        // E' cio' che fa il clic su una riga: la selezione. Il costruttore di HabitScreenViewModel
        // (26 dipendenze) deve risolvere dal contenitore vero, anche se la sua view arriva in F2.
        vm.Home.SelectedRow = row;
        for (var i = 0; i < 100 && vm.Workspace.Tabs.Count == 0; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        vm.Workspace.Tabs.Should().ContainSingle();
        vm.Workspace.ActiveSubjectTab!.Screen.Should().NotBeNull();
        vm.Workspace.ActiveSubjectTab.Title.Should().Be(row.Name);

        var strip = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TabStrip");
        strip.GetRealizedContainers().Should().ContainSingle();

        // E il guscio si riorganizza: la Home diventa barra laterale e compare il divisore.
        var grid = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "HomeWorkspace");
        grid.ColumnDefinitions[0].Width.Should().Be(new GridLength(280));
        window.GetVisualDescendants().OfType<GridSplitter>().Single().IsVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task The_shell_shortcuts_work_on_a_loaded_window()
    {
        var window = await OpenSeededAsync();
        var vm = (MainWindowViewModel)window.DataContext!;

        window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        vm.Home.ActiveTab.Should().Be(HomeTab.Trash);

        window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        vm.Home.ActiveTab.Should().Be(HomeTab.Active);

        // Esc con una selezione multipla la annulla, e lo fa prima di qualunque altra cosa.
        var rows = vm.Home.Rows.Take(2).ToArray();
        vm.Home.SelectInteractive(rows[0], ctrl: false, shift: false);
        vm.Home.SelectInteractive(rows[1], ctrl: true, shift: false);
        vm.Home.HasMultiSelection.Should().BeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        vm.Home.HasMultiSelection.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task The_home_load_stays_within_a_generous_budget_and_the_number_is_printed()
    {
        await ShellFixture.Shared.SeedAsync();   // fuori dalla misura: semina una volta sola
        var vm = ShellFixture.Shared.NewMainWindowViewModel();
        var window = new MainWindow(vm);

        var watch = Stopwatch.StartNew();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 400 && vm.Home.Rows.Count == 0; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        watch.Stop();

        vm.Home.Rows.Should().NotBeEmpty();
        // Soglia larga di proposito: qui non si difende il budget E9/E20 (<500 ms sulla Home), si
        // difende il fatto che l'avvio non degradi di un ordine di grandezza (misurato: ~3,0 s quando
        // questo e' il primo caricamento del processo, che paga la costruzione del modello EF e il JIT;
        // ~0,2 s quando altri test lo hanno gia' scaldato). Il numero vero e'
        // stampato, ed e' quello che si guarda quando si torna sul budget (D13).
        output.WriteLine($"Home caricata in {watch.ElapsedMilliseconds} ms "
                         + $"({vm.Home.Rows.Count} righe, DB SQLCipher su file)");
        watch.ElapsedMilliseconds.Should().BeLessThan(8000);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_shell_renders_in_both_theme_variants(string variantName)
    {
        var window = await OpenSeededAsync();
        window.RequestedThemeVariant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var path = ScreenshotProbeTests.Capture(window, $"shell-{variantName.ToLowerInvariant()}");
        output.WriteLine(path);

        // L'assert NON e' il peso del file (uno screenshot vuoto pesa lo stesso): e' la struttura.
        // Lo screenshot serve a GUARDARLO.
        window.GetVisualDescendants().OfType<WorkspaceRibbon>().Should().ContainSingle();
        window.GetVisualDescendants().OfType<HomeView>().Should().ContainSingle();
        window.GetVisualDescendants().OfType<WorkspaceView>().Should().ContainSingle();
        window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "RowsHost")
            .GetRealizedContainers().Should().NotBeEmpty();
    }
}
