using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Workspace;
using Xunit;

namespace Recordum.UI.Tests;

public class WorkspaceViewTests
{
    private static Window Show(Control content, object dataContext)
    {
        var window = new Window { Width = 900, Height = 600, Content = content, DataContext = dataContext };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void The_ribbon_marks_the_active_workspace_and_lists_six_actions()
    {
        var vm = ShellFixture.Shared.NewMainWindowViewModel();
        var ribbon = new WorkspaceRibbon();
        Show(ribbon, vm);

        var buttons = ribbon.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("rail")).ToArray();
        buttons.Should().HaveCount(6, "il rail porta Home, Grafici, Backup, Esporta, Diagnostica, Impostazioni");

        var hosts = ribbon.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("activehost")).ToArray();
        hosts.Should().HaveCount(2);
        hosts[0].Classes.Contains("active").Should().BeTrue("all'avvio il workspace attivo e' la Home");
        hosts[1].Classes.Contains("active").Should().BeFalse();

        vm.CurrentWorkspace = AppWorkspace.Charts;
        Dispatcher.UIThread.RunJobs();
        hosts[0].Classes.Contains("active").Should().BeFalse();
        hosts[1].Classes.Contains("active").Should().BeTrue("passando ai Grafici l'evidenza si sposta");
    }

    [AvaloniaFact]
    public void Only_the_home_button_is_visible_until_its_wave_turns_the_others_on()
    {
        var ribbon = new WorkspaceRibbon();
        Show(ribbon, ShellFixture.Shared.NewMainWindowViewModel());

        var visible = ribbon.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("rail"))
            .ToDictionary(b => b.Name!, b => b.IsVisible);
        visible["HomeButton"].Should().BeTrue();
        visible.Where(kv => kv.Key != "HomeButton").Should().OnlyContain(kv => !kv.Value,
            "Grafici, Backup, Esporta, Diagnostica e Impostazioni stanno dietro launcher/viste non ancora portati");
    }

    [AvaloniaFact]
    public void The_backup_button_opens_a_flyout_whose_three_items_carry_live_commands()
    {
        var vm = ShellFixture.Shared.NewMainWindowViewModel();
        var ribbon = new WorkspaceRibbon();
        Show(ribbon, vm);

        var button = ribbon.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BackupButton");
        var flyout = button.Flyout.Should().BeOfType<MenuFlyout>().Subject;

        flyout.ShowAt(button);
        Dispatcher.UIThread.RunJobs();

        var items = flyout.Items.OfType<MenuItem>().ToArray();
        items.Should().HaveCount(3);
        items.Should().OnlyContain(i => i.Command != null,
            "un MenuFlyout eredita il DataContext dal pulsante: se non lo facesse i Command sarebbero null");
        items[0].Command.Should().BeSameAs(vm.BackupCreateCommand);
        items[2].Command.Should().BeSameAs(vm.ShowBackupHistoryCommand);
        flyout.Hide();
    }

    [AvaloniaFact]
    public void Clicking_empty_rail_space_clears_the_home_selection()
    {
        var vm = ShellFixture.Shared.NewMainWindowViewModel();
        var ribbon = new WorkspaceRibbon();
        var window = Show(ribbon, vm);

        vm.Home.SelectedRow = ShellFixture.Shared.NewHomeRow("Corsa");

        // Meta' altezza del rail: sotto i pulsanti in alto e sopra quelli in basso. Il punto si
        // traduce DAL rail: il rail e' largo 44 dentro una finestra di 900, quindi un punto
        // assoluto (22, 300) cadrebbe fuori da esso.
        var empty = ribbon.TranslatePoint(new Point(22, 300), window)!.Value;
        window.MouseDown(empty, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(empty, global::Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        vm.Home.SelectedRow.Should().BeNull();
    }

    [AvaloniaFact]
    public void Clicking_a_rail_button_does_not_clear_the_selection()
    {
        var vm = ShellFixture.Shared.NewMainWindowViewModel();
        var ribbon = new WorkspaceRibbon();
        var window = Show(ribbon, vm);

        vm.Home.SelectedRow = ShellFixture.Shared.NewHomeRow("Corsa");

        // HomeButton, gia' sulla Home: il comando commuta la sidebar e non tocca la selezione, quindi se la
        // selezione sparisce e' stato il gestore del vuoto a vedere il clic.
        var home = ribbon.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HomeButton");
        var p = home.TranslatePoint(new(16, 16), window)!.Value;
        window.MouseDown(p, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(p, global::Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        vm.Workspace.SidebarCollapsed.Should().BeTrue("il clic e' arrivato davvero al pulsante (Home gia' attiva: commuta la sidebar)");
        vm.Home.SelectedRow.Should().NotBeNull("il clic su un pulsante non e' un clic sul vuoto");
    }

    [AvaloniaFact]
    public void The_welcome_panel_shows_on_panoramica_and_no_subject_template_is_built()
    {
        var vm = ShellFixture.Shared.NewWorkspaceViewModel();
        var view = new WorkspaceView();
        Show(view, vm);

        var welcome = view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "WelcomePanel");
        welcome.IsVisible.Should().BeTrue();

        var host = view.GetVisualDescendants().OfType<ContentControl>().Single(c => c.Name == "SubjectHost");
        host.Content.Should().BeNull();
        view.GetVisualDescendants().OfType<Border>()
            .Any(b => b.Name == "SubjectScreenPlaceholder")
            .Should().BeFalse("con Content null nessun template del soggetto deve materializzarsi");
    }

    [AvaloniaFact]
    public async Task Opening_a_tab_realizes_a_strip_entry_and_builds_the_subject_template()
    {
        var vm = ShellFixture.Shared.NewWorkspaceViewModel();
        var view = new WorkspaceView();
        Show(view, vm);

        await vm.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var strip = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TabStrip");
        strip.GetRealizedContainers().Should().HaveCount(1);
        strip.SelectedItem.Should().BeSameAs(vm.ActiveSubjectTab, "selezione e tab attivo sono la stessa cosa");

        view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "TabTitle").Text.Should().Be("Corsa");
        view.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "SubjectScreenPlaceholder")
            .Should().BeTrue("con un tab aperto il template del soggetto esiste (vuoto, F2 lo riempie)");
        view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "WelcomePanel")
            .IsVisible.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task The_pin_and_close_buttons_of_a_tab_drive_the_view_model()
    {
        var vm = ShellFixture.Shared.NewWorkspaceViewModel();
        var view = new WorkspaceView();
        Show(view, vm);
        await vm.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        Dispatcher.UIThread.RunJobs();

        var tab = vm.Tabs[0];
        tab.IsPreview.Should().BeTrue("una singola apertura e' un'anteprima");

        var pin = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PinButton");
        pin.Command.Should().BeSameAs(vm.TogglePinCommand);
        pin.CommandParameter.Should().BeSameAs(tab);
        pin.Command!.Execute(pin.CommandParameter);
        tab.IsPreview.Should().BeFalse();

        var close = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CloseButton");
        close.Command.Should().BeSameAs(vm.CloseTabCmdCommand);
        close.Command!.Execute(close.CommandParameter);
        vm.Tabs.Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task A_double_tap_on_the_strip_pins_the_active_tab()
    {
        var vm = ShellFixture.Shared.NewWorkspaceViewModel();
        var view = new WorkspaceView();
        var window = Show(view, vm);
        await vm.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var title = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "TabTitle");
        var p = title.TranslatePoint(new(4, 4), window)!.Value;
        window.MouseDown(p, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(p, global::Avalonia.Input.MouseButton.Left);
        window.MouseDown(p, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(p, global::Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        vm.Tabs[0].IsPreview.Should().BeFalse("il doppio tap fissa il tab");
    }

    [AvaloniaFact]
    public async Task A_preview_tab_wears_the_preview_classes()
    {
        var vm = ShellFixture.Shared.NewWorkspaceViewModel();
        var view = new WorkspaceView();
        Show(view, vm);
        await vm.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var title = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "TabTitle");
        var pinIcon = view.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().First(p => p.Classes.Contains("pinicon"));
        title.Classes.Contains("preview").Should().BeTrue();
        pinIcon.Classes.Contains("preview").Should().BeTrue();

        vm.Tabs[0].Pin();
        Dispatcher.UIThread.RunJobs();
        title.Classes.Contains("preview").Should().BeFalse();
        pinIcon.Classes.Contains("preview").Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task Focus_zen_hides_the_tab_strip()
    {
        var vm = ShellFixture.Shared.NewWorkspaceViewModel();
        var view = new WorkspaceView();
        Show(view, vm);
        await vm.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        Dispatcher.UIThread.RunJobs();

        var strip = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TabStrip");
        strip.IsVisible.Should().BeTrue();

        vm.ActiveSubjectTab!.Screen.FocusedColumn = HabitColumn.Panoramica;
        Dispatcher.UIThread.RunJobs();
        strip.IsVisible.Should().BeFalse("in focus zen la striscia dei tab sparisce");
    }
}
