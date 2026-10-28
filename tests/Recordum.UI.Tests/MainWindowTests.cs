using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Workspace;
using Xunit;

namespace Recordum.UI.Tests;

public class MainWindowTests
{
    private static Task<(MainWindow Window, MainWindowViewModel Vm)> Open()
        => ShellFixture.OpenMainWindowAsync(ShellFixture.Shared.NewMainWindowViewModel());

    [AvaloniaTheory]
    [InlineData(1280, 760, 1600, 900, 1280, 760)]   // ci sta: invariata
    [InlineData(1280, 760, 1280, 672, 1280, 672)]   // schermo RDP basso: si accorcia
    [InlineData(1280, 760, 1024, 768, 1024, 760)]   // solo la larghezza eccede
    [InlineData(800, 600, 1600, 900, 800, 600)]     // piu' piccola dell'area: invariata
    public void Clamp_to_work_area_never_exceeds_the_screen(
        double w, double h, double waW, double waH, double expectedW, double expectedH)
    {
        var (width, height) = MainWindow.ClampToWorkArea(w, h, waW, waH);
        width.Should().Be(expectedW);
        height.Should().Be(expectedH);
    }

    [AvaloniaFact]
    public void Resolve_columns_covers_the_four_shell_states()
    {
        // Panoramica: nessun tab aperto -> la Home prende tutto, niente divisore.
        var panoramica = MainWindow.ResolveColumns(panoramica: true, collapsed: false, sidebarWidth: 280, focusZen: false);
        panoramica.Sidebar.Should().Be(new GridLength(1, GridUnitType.Star));
        panoramica.Workspace.Should().Be(new GridLength(0));
        panoramica.ShowSplitter.Should().BeFalse();

        // Tab aperto: la Home diventa barra laterale della larghezza persistita, col divisore.
        var open = MainWindow.ResolveColumns(false, false, 320, false);
        open.Sidebar.Should().Be(new GridLength(320));
        open.Workspace.Should().Be(new GridLength(1, GridUnitType.Star));
        open.ShowSplitter.Should().BeTrue();

        // Barra laterale chiusa: larghezza 0 e nessun divisore da trascinare.
        var collapsed = MainWindow.ResolveColumns(false, true, 320, false);
        collapsed.Sidebar.Should().Be(new GridLength(0));
        collapsed.ShowSplitter.Should().BeFalse();

        // Focus zen: vince su tutto.
        var zen = MainWindow.ResolveColumns(false, false, 320, true);
        zen.Sidebar.Should().Be(new GridLength(0));
        zen.ShowSplitter.Should().BeFalse();

        // Larghezza persistita a zero (stato iniziale mai salvato) -> il default, non una barra invisibile.
        MainWindow.ResolveColumns(false, false, 0, false).Sidebar.Should().Be(new GridLength(340));
    }

    [AvaloniaFact]
    public async Task The_window_hosts_the_ribbon_the_home_and_the_workspace()
    {
        var (window, vm) = await Open();

        window.GetVisualDescendants().OfType<WorkspaceRibbon>().Should().ContainSingle();
        window.GetVisualDescendants().OfType<HomeView>().Should().ContainSingle();
        window.GetVisualDescendants().OfType<WorkspaceView>().Should().ContainSingle();
        window.Title.Should().Be(Recordum.Presentation.Resources.Strings.AppTitle);

        var ribbon = window.GetVisualDescendants().OfType<WorkspaceRibbon>().Single();
        ribbon.DataContext.Should().BeSameAs(vm, "il rail eredita il MainWindowViewModel");
        window.GetVisualDescendants().OfType<HomeView>().Single().DataContext.Should().BeSameAs(vm.Home);
        window.GetVisualDescendants().OfType<WorkspaceView>().Single().DataContext.Should().BeSameAs(vm.Workspace);
    }

    [AvaloniaFact]
    public async Task The_title_bar_declares_its_chrome_roles_and_its_height()
    {
        var (window, _) = await Open();

        window.ExtendClientAreaTitleBarHeightHint.Should().Be(36);

        var bar = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TitleBar");
        WindowDecorationProperties.GetElementRole(bar).Should().Be(WindowDecorationsElementRole.TitleBar);
        bar.Bounds.Height.Should().Be(36);

        Role(window, "MinButton").Should().Be(WindowDecorationsElementRole.MinimizeButton);
        Role(window, "MaxButton").Should().Be(WindowDecorationsElementRole.MaximizeButton);
        Role(window, "CloseButton").Should().Be(WindowDecorationsElementRole.CloseButton);
        // Il ☰ non e' un pulsante di didascalia: se avesse un ruolo di sistema la piattaforma se lo
        // prenderebbe e il comando non scatterebbe piu'.
        Role(window, "RibbonToggle").Should().Be(WindowDecorationsElementRole.User);

        static WindowDecorationsElementRole Role(Visual root, string name)
            => WindowDecorationProperties.GetElementRole(
                root.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name));
    }

    [AvaloniaTheory]
    [InlineData(false, false)]   // piattaforma che rifiuta l'estensione (Linux/X11): barra di sistema, niente doppioni
    [InlineData(true, true)]     // piattaforma che l'accoglie (Windows): i pulsanti di didascalia sono nostri
    public async Task The_caption_buttons_are_ours_only_where_the_platform_extended_the_client_area(
        bool extended, bool expectedVisible)
    {
        var (window, _) = await Open();
        // La decisione e' funzione del risultato, non del sistema operativo: la si guida in entrambi i versi.
        window.ApplyChrome(extended);

        foreach (var name in new[] { "MinButton", "MaxButton", "CloseButton" })
            window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name)
                .IsVisible.Should().Be(expectedVisible, $"{name} con extended={extended}");

        // Il pulsante del rail vive nella nostra barra in ogni caso.
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RibbonToggle")
            .IsVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Opening_applies_the_chrome_decision_from_what_the_platform_reported()
    {
        var (window, _) = await Open();

        // Opened e' gia' scattato in Open(): la finestra headless non estende l'area client, quindi
        // il risultato letto e' false e i pulsanti sono nascosti.
        window.IsExtendedIntoWindowDecorations.Should().BeFalse("la piattaforma headless non accoglie la richiesta");
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CloseButton")
            .IsVisible.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task The_caption_buttons_follow_the_platform_when_it_reports_the_extension_after_opening()
    {
        var (window, _) = await Open();
        Button Close() => window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CloseButton");
        Close().IsVisible.Should().BeFalse("la finestra headless parte senza estensione");

        // La proprieta' e' di sola lettura (DirectProperty con setter privato): la si guida dalla
        // stessa strada della piattaforma vera, il callback IWindowImpl.ExtendClientAreaToDecorationsChanged.
        // Il membro di IWindowImpl non e' accessibile (protected internal): lo si legge per riflessione.
        var report = (Action<bool>)typeof(IWindowImpl)
            .GetProperty("ExtendClientAreaToDecorationsChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(window.PlatformImpl)!;

        report(true);
        Dispatcher.UIThread.RunJobs();
        window.IsExtendedIntoWindowDecorations.Should().BeTrue();
        Close().IsVisible.Should().BeTrue("la piattaforma ha accolto la richiesta dopo Opened");

        report(false);
        Dispatcher.UIThread.RunJobs();
        Close().IsVisible.Should().BeFalse("e se la ritira i pulsanti si nascondono di nuovo");
    }

    [AvaloniaFact]
    public async Task Every_caption_glyph_draws_something_when_the_caption_buttons_are_ours()
    {
        // F1-ICON1 (review F1, I2): un Path con Stretch=Uniform e geometria alta zero ha sy=0 e non disegna nulla.
        // Si misura la geometria resa, non la presenza dell'elemento.
        var (window, _) = await Open();
        window.ApplyChrome(true);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        foreach (var name in new[] { "MinButton", "MaxButton", "CloseButton" })
        {
            var glyph = (global::Avalonia.Controls.Shapes.Path)window.GetVisualDescendants().OfType<Button>()
                .Single(b => b.Name == name).Content!;
            glyph.RenderedGeometry!.Bounds.Width.Should().BeGreaterThan(0, $"il glifo di {name} deve avere un'impronta");
        }
    }

    [AvaloniaFact]
    public async Task The_maximize_glyph_and_its_name_follow_the_window_state()
    {
        var (window, _) = await Open();
        var icon = window.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>()
            .Single(p => p.Name == "MaxIcon");
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "MaxButton");

        MainWindow.GlyphFor(WindowState.Normal).Should().NotBeSameAs(MainWindow.GlyphFor(WindowState.Maximized));
        global::Avalonia.Automation.AutomationProperties.GetName(button)
            .Should().Be(Recordum.Presentation.Resources.Strings.Window_Maximize);

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        icon.Data.Should().BeSameAs(MainWindow.GlyphFor(WindowState.Maximized),
            "massimizzata, il glifo diventa quello di ripristino");
        global::Avalonia.Automation.AutomationProperties.GetName(button)
            .Should().Be(Recordum.Presentation.Resources.Strings.Window_Restore);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        icon.Data.Should().BeSameAs(MainWindow.GlyphFor(WindowState.Normal));
        global::Avalonia.Automation.AutomationProperties.GetName(button)
            .Should().Be(Recordum.Presentation.Resources.Strings.Window_Maximize);
    }

    [AvaloniaFact]
    public async Task The_hamburger_toggles_the_ribbon()
    {
        var (window, vm) = await Open();
        var ribbon = window.GetVisualDescendants().OfType<WorkspaceRibbon>().Single();
        ribbon.IsVisible.Should().BeTrue();

        vm.ToggleRibbonCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        ribbon.IsVisible.Should().BeFalse();

        var toggle = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RibbonToggle");
        toggle.Command.Should().BeSameAs(vm.ToggleRibbonCommand);
    }

    [AvaloniaFact]
    public async Task Ctrl_1_2_3_switch_the_home_tab_and_return_to_the_home_workspace()
    {
        var (window, vm) = await Open();
        vm.CurrentWorkspace = AppWorkspace.Charts;
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        vm.Home.ActiveTab.Should().Be(HomeTab.Archived, "la KeyBinding cambia la sotto-scheda");
        vm.CurrentWorkspace.Should().Be(AppWorkspace.Home, "e il gestore in tunnel riporta alla Home");

        window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        vm.Home.ActiveTab.Should().Be(HomeTab.Active);
    }

    [AvaloniaFact]
    public async Task Esc_clears_a_multi_selection_before_anything_else()
    {
        var (window, vm) = await Open();
        var a = ShellFixture.Shared.NewHomeRow("a");
        var b = ShellFixture.Shared.NewHomeRow("b");
        vm.Home.Rows.Add(a);
        vm.Home.Rows.Add(b);
        vm.Home.SelectInteractive(a, ctrl: false, shift: false);
        vm.Home.SelectInteractive(b, ctrl: true, shift: false);
        vm.Home.HasMultiSelection.Should().BeTrue();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        vm.Home.HasMultiSelection.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task The_columns_follow_the_view_model_and_keep_following_it_after_a_splitter_drag()
    {
        var (window, vm) = await Open();
        var grid = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "HomeWorkspace");
        var sidebar = grid.ColumnDefinitions[0];
        var splitter = window.GetVisualDescendants().OfType<GridSplitter>().Single();

        // Panoramica: la Home riempie, niente divisore.
        sidebar.Width.Should().Be(new GridLength(1, GridUnitType.Star));
        splitter.IsVisible.Should().BeFalse();

        await vm.Workspace.OpenSubjectAsync(Guid.NewGuid(), Presentation.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        sidebar.Width.Should().Be(new GridLength(340), "con un tab aperto la Home diventa barra laterale (340 px, direzione visiva §5)");
        splitter.IsVisible.Should().BeTrue();

        // Il trascinamento scrive un valore LOCALE sulla colonna, che il ViewModel non conosce.
        // Si trascina OLTRE il massimo consentito: e' il caso che distingue "leggo e basta" da
        // "leggo, limito e ri-applico".
        var p = splitter.TranslatePoint(new Point(2, 300), window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseMove(new Point(p.X + 400, p.Y));
        window.MouseUp(new Point(p.X + 400, p.Y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        vm.Workspace.SidebarWidth.Should().Be(460,
            "a fine trascinamento la larghezza resa torna nel ViewModel, limitata a 0..460");
        sidebar.Width.Should().Be(new GridLength(460),
            "e la colonna si ri-allinea subito al valore limitato: senza ri-applicare, la barra "
            + "resterebbe larga quanto il trascinamento finche' qualcos'altro non muove il ViewModel");

        // Secondo trascinamento oltre il massimo, col ViewModel GIA' a 460: il limite non cambia
        // nessuna proprieta', quindi la sottoscrizione al ViewModel non scatta e l'unica cosa che
        // riporta la colonna a 460 e' il ri-applicare a fine trascinamento.
        var p2 = splitter.TranslatePoint(new Point(2, 300), window)!.Value;
        window.MouseDown(p2, MouseButton.Left);
        window.MouseMove(new Point(p2.X + 150, p2.Y));
        window.MouseUp(new Point(p2.X + 150, p2.Y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        vm.Workspace.SidebarWidth.Should().Be(460);
        sidebar.Width.Should().Be(new GridLength(460),
            "il limite superiore deve vedersi a schermo anche quando il ViewModel non cambia");

        // E il ViewModel continua a comandare.
        vm.Workspace.ActivatePanoramica();
        Dispatcher.UIThread.RunJobs();
        sidebar.Width.Should().Be(new GridLength(1, GridUnitType.Star));
    }

}
