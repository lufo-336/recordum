using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Recordum.UI.Controls;
using Recordum.UI.Views;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

public class HomeRowViewTests
{
    private static Control Show(Control view)
    {
        var w = new Window { Width = 600, Height = 200, Content = view };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    [AvaloniaFact]
    public void The_template_builds_a_group_row_for_a_group_and_a_habit_row_otherwise()
    {
        var group = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), type: null));
        var habit = RowFixture.Vm(RowFixture.Row(Guid.NewGuid()));
        HomeRowTemplate.Instance.Match(group).Should().BeTrue();
        HomeRowTemplate.Instance.Match("x").Should().BeFalse();
        HomeRowTemplate.Instance.Build(group).Should().BeOfType<GroupRow>();
        HomeRowTemplate.Instance.Build(habit).Should().BeOfType<HabitRow>();
    }

    [AvaloniaFact]
    public void The_rename_editor_replaces_the_label_only_while_renaming()
    {
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), "Corsa"));
        var view = Show(new HabitRow { DataContext = vm });
        var label = view.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == "Corsa");
        var box = view.GetLogicalDescendants().OfType<TextBox>().Single();
        label.IsVisible.Should().BeTrue();
        box.IsVisible.Should().BeFalse();

        vm.BeginRenameCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        label.IsVisible.Should().BeFalse();
        box.IsVisible.Should().BeTrue();
        box.Text.Should().Be("Corsa");
    }

    [AvaloniaFact]
    public void Enter_in_the_rename_editor_commits()
    {
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), "Corsa"));
        var view = Show(new HabitRow { DataContext = vm });
        vm.BeginRenameCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetLogicalDescendants().OfType<TextBox>().Single();
        box.Text = "Nuoto";
        Dispatcher.UIThread.RunJobs();

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = box });
        Dispatcher.UIThread.RunJobs();

        vm.IsRenaming.Should().BeFalse();
        vm.Name.Should().Be("Nuoto");
    }

    [AvaloniaFact]
    public void The_context_menu_of_a_habit_row_offers_the_pin_submenu()
    {
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid()));
        var view = Show(new HabitRow { DataContext = vm });
        var grid = view.GetLogicalDescendants().OfType<Grid>().Single(g => g.Name == "MenuHost");
        grid.ContextMenu.Should().BeNull("il menu nasce alla prima richiesta, non con la riga (F1-PERF1)");
        // La richiesta vera (tasto destro, Shift+F10) lo costruisce e lo apre. Aperto: un ContextMenu
        // chiuso non eredita il DataContext e ogni voce resta al default IsVisible=true, quindi
        // "contiene Pin" passerebbe anche con i binding rotti. Si asserisce pure ciò che deve mancare
        // (Ripristina/Riattiva) e si prova un tab dove Pin sparisce.
        grid.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        var menu = grid.ContextMenu!;
        menu.IsOpen.Should().BeTrue();

        IEnumerable<object?> Visible() => menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).Select(i => i.Header);
        var pin = Recordum.Presentation.Resources.Strings.Home_ContextMenu_Pin;
        Visible().Should().Contain(pin)
            .And.NotContain(Recordum.Presentation.Resources.Strings.Home_ContextMenu_Restore)
            .And.NotContain(Recordum.Presentation.Resources.Strings.Home_ContextMenu_Unarchive);

        vm.CurrentTab = HomeTab.Trash;
        Dispatcher.UIThread.RunJobs();
        Visible().Should().NotContain(pin)
            .And.Contain(Recordum.Presentation.Resources.Strings.Home_ContextMenu_Restore);
        menu.Close();
    }

    // F1-PERF1: la Home è una lista piatta e la cornice del gruppo si disegna a fette (GroupFrames).
    // Un difetto di disegno si misura: si leggono i pixel resi, non le proprietà.
    [AvaloniaFact]
    public void The_group_frame_is_drawn_across_its_rows_only_while_the_group_is_expanded()
    {
        var g = Guid.NewGuid();
        var (vm, _, _) = HomeFixture.Build(
            RowFixture.Row(g, "Gruppo", "#E11D48", type: null, childCount: 1),
            RowFixture.Row(Guid.NewGuid(), "Figlia", "#3B82F6", parent: g));
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 600, Height = 300, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        var group = view.GetVisualDescendants().OfType<GroupRow>().Single();
        var child = view.GetVisualDescendants().OfType<HabitRow>().Single();
        Point At(Control row, double x) => row.TranslatePoint(new Point(x, row.Bounds.Height / 2), w)!.Value;

        var frame = w.CaptureRenderedFrame()!;
        var background = Pixel(frame, new Point(w.Width - 2, w.Height - 2));
        var fill = Over(background, 10);
        var border = Over(fill, 80);
        Pixel(frame, At(group, 0)).Should().Match<Color>(c => Near(c, border), "il bordo sinistro sulla riga del gruppo");
        Pixel(frame, At(child, 0)).Should().Match<Color>(c => Near(c, border), "il bordo continua sulla figlia");
        Pixel(frame, At(child, GroupFrames.IndentStep / 2)).Should().Match<Color>(c => Near(c, fill),
            "fra il bordo e la card rientrata della figlia c'è il fondo della cornice");

        vm.Rows.Single(r => r.IsGroup).IsExpanded = false;
        Pump();
        group = view.GetVisualDescendants().OfType<GroupRow>().Single();
        frame = w.CaptureRenderedFrame()!;
        Pixel(frame, At(group, 0)).Should().Match<Color>(c => Near(c, background), "chiuso, il gruppo non ha cornice");
        w.Close();

        // Il colore del gruppo (#E11D48) con l'alfa dato, steso sopra `under`.
        static Color Over(Color under, byte alpha)
        {
            byte Mix(byte u, byte o) => (byte)Math.Round(u + (o - u) * alpha / 255.0);
            return Color.FromRgb(Mix(under.R, 0xE1), Mix(under.G, 0x1D), Mix(under.B, 0x48));
        }
    }

    private static bool Near(Color a, Color b) =>
        Math.Abs(a.R - b.R) <= 4 && Math.Abs(a.G - b.G) <= 4 && Math.Abs(a.B - b.B) <= 4;

    private static Color Pixel(Bitmap frame, Point at)
    {
        var buffer = new byte[4];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(new PixelRect((int)at.X, (int)at.Y, 1, 1), handle.AddrOfPinnedObject(), 4, 4);
        }
        finally { handle.Free(); }
        // L'ordine dei byte dipende dal formato del frame (in headless con Skia è Rgba8888).
        return frame.Format == Avalonia.Platform.PixelFormats.Bgra8888
            ? Color.FromRgb(buffer[2], buffer[1], buffer[0])
            : Color.FromRgb(buffer[0], buffer[1], buffer[2]);
    }

    [AvaloniaFact]
    public void A_nested_row_card_is_indented_by_its_level()
    {
        var root = Guid.NewGuid();
        var inner = Guid.NewGuid();
        var (vm, _, _) = HomeFixture.Build(
            RowFixture.Row(root, "Radice", type: null, childCount: 1),
            RowFixture.Row(inner, "Interno", type: null, parent: root, childCount: 1),
            RowFixture.Row(Guid.NewGuid(), "Foglia", parent: inner));
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 600, Height = 300, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();

        var leaf = view.GetVisualDescendants().OfType<HabitRow>().Single();
        var card = leaf.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("habitblock"));
        card.TranslatePoint(new Point(0, 0), leaf)!.Value.X.Should().Be(2 * GroupFrames.IndentStep);
        w.Close();
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
