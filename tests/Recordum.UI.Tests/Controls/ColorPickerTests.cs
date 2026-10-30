using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Recordum.UI.Tests;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.UI.Controls;
using Xunit;

namespace Recordum.UI.Tests.Controls;

public class ColorPickerTests
{
    private static Control PanelOf(ColorSwatchPicker picker)
    {
        var trigger = picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null);
        return (Control)((Flyout)trigger.Flyout!).Content!;
    }

    private static IEnumerable<Button> Swatches(Control panel) =>
        panel.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag is "swatch");

    private static Border Ring(Button b) => (Border)b.Content!;

    private static void Enter(TextBox t) =>
        t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Window Host(Control c)
    {
        var w = new Window { Width = 400, Height = 300, Content = c };
        w.Show();
        return w;
    }

    [AvaloniaFact]
    public void Clicking_a_swatch_writes_its_colour_to_the_bound_property()
    {
        var picker = new ColorSwatchPicker { Color = "#808080" };
        var w = Host(picker);
        try
        {
            var swatch = Swatches(PanelOf(picker)).First(s => AutomationProperties.GetName(s) == ColorSwatchPicker.Palette[4]);
            Click(swatch);
            picker.Color.Should().Be(ColorSwatchPicker.Palette[4]);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_chosen_colour_is_marked_selected_and_the_others_are_not()
    {
        var picker = new ColorSwatchPicker { Color = ColorSwatchPicker.Palette[1].ToUpperInvariant() };
        var w = Host(picker);
        try
        {
            var all = Swatches(PanelOf(picker)).ToList();
            var selected = all.Where(s => Ring(s).BorderThickness.Left >= 2).ToList();
            // la tessera può comparire anche nella rampa delle tonalità: tutte le segnate sono il colore scelto
            selected.Should().NotBeEmpty().And.OnlyContain(s => s.Classes.Contains("selected")
                && string.Equals(AutomationProperties.GetName(s), ColorSwatchPicker.Palette[1], StringComparison.OrdinalIgnoreCase));
            all.Except(selected).Should().OnlyContain(s => Ring(s).BorderThickness.Left < 2 && !s.Classes.Contains("selected"));

            picker.Color = ColorSwatchPicker.Palette[7];
            Swatches(PanelOf(picker)).Where(s => Ring(s).BorderThickness.Left >= 2)
                .Should().NotBeEmpty().And.OnlyContain(s => string.Equals(AutomationProperties.GetName(s), ColorSwatchPicker.Palette[7], StringComparison.OrdinalIgnoreCase));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void A_typed_hex_is_validated_an_invalid_one_never_reaches_the_binding()
    {
        var picker = new ColorSwatchPicker { Color = "#808080" };
        var w = Host(picker);
        try
        {
            var hex = PanelOf(picker).GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "HexBox");
            hex.Text = "zzzzzz";
            Enter(hex);
            picker.Color.Should().Be("#808080");
            hex.Text.Should().Be("zzzzzz", "il testo resta com'è, con la cornice d'errore: non si cancella ciò che l'utente ha scritto");

            hex.Text = "12";
            Enter(hex);
            picker.Color.Should().Be("#808080");

            hex.Text = "1a2B3c";
            Enter(hex);
            picker.Color.Should().Be("#1A2B3C");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Recent_and_favourite_swatches_pick_and_the_favourite_commands_get_the_hex()
    {
        var recent = new System.Collections.ObjectModel.ObservableCollection<string>();
        var favs = new System.Collections.ObjectModel.ObservableCollection<string> { "#112233" };
        string? added = null, removed = null;
        var picker = new ColorSwatchPicker
        {
            Color = "#808080",
            RecentColors = recent,
            FavoriteColors = favs,
            AddFavoriteCommand = new Relay(p => added = (string?)p),
            RemoveFavoriteCommand = new Relay(p => removed = (string?)p),
        };
        var w = Host(picker);
        try
        {
            recent.Add("#AA0000"); // arriva dopo il binding, come nel ViewModel
            var panel = PanelOf(picker);
            Click(Swatches(panel).First(s => AutomationProperties.GetName(s) == "#AA0000"));
            picker.Color.Should().Be("#AA0000");

            Click(panel.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "AddFavorite"));
            added.Should().Be("#AA0000");
            Click(panel.GetLogicalDescendants().OfType<Button>().Single(b => b.Tag is "remove"));
            removed.Should().Be("#112233");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_remove_favourite_button_shows_the_Lucide_X_path_not_a_text_glyph()
    {
        var picker = new ColorSwatchPicker { FavoriteColors = new[] { "#112233" } };
        var w = Host(picker);
        try
        {
            var remove = PanelOf(picker).GetLogicalDescendants().OfType<Button>().Single(b => b.Tag is "remove");
            remove.Content.Should().BeOfType<Avalonia.Controls.Shapes.Path>().Which.Data.Should().NotBeNull();
            AutomationProperties.GetName(remove).Should().Contain(Recordum.Presentation.Resources.Strings.ColorPicker_RemoveFavorite);
            ToolTip.GetTip(remove).Should().Be(Recordum.Presentation.Resources.Strings.ColorPicker_RemoveFavorite);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Every_swatch_and_every_button_has_an_accessible_name()
    {
        var picker = new ColorSwatchPicker { FavoriteColors = new[] { "#112233" }, RecentColors = new[] { "#445566" } };
        var w = Host(picker);
        try
        {
            var buttons = PanelOf(picker).GetLogicalDescendants().OfType<Button>().ToList();
            buttons.Should().NotBeEmpty();
            buttons.Should().OnlyContain(b => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)));
            picker.GetVisualDescendants().OfType<Button>().Should().OnlyContain(b => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)));
            buttons.Should().NotContain(b => b.Name == "Eyedropper", "su Avalonia non c'è il contagocce (decisione 5)");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Dragging_in_the_square_and_on_the_strip_changes_the_colour()
    {
        var hsv = new HsvColorPicker { Color = "#808080", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var w = Host(hsv);
        try
        {
            Dispatcher_Pump();
            var sv = hsv.FindControl<Border>("SvArea")!;
            var hue = hsv.FindControl<Border>("HueArea")!;
            Point In(Control c, double fx, double fy) =>
                c.TranslatePoint(new Point(c.Bounds.Width * fx, c.Bounds.Height * fy), w)!.Value;

            // trascinamento: giù sul centro, poi muove all'angolo in alto a destra (S=1, V=1, H=0 → rosso)
            w.MouseDown(In(sv, 0.5, 0.5), MouseButton.Left);
            w.MouseMove(In(sv, 0.999, 0.001));
            w.MouseUp(In(sv, 0.999, 0.001), MouseButton.Left);
            hsv.Color.Should().Match(c => c == "#FF0000" || c == "#FE0000" || c == "#FF0100", "angolo S=1 V=1 con hue 0");

            // la striscia sposta la tonalità: a metà strada il ciano
            w.MouseDown(In(hue, 0.5, 0.5), MouseButton.Left);
            w.MouseUp(In(hue, 0.5, 0.5), MouseButton.Left);
            var c2 = Avalonia.Media.Color.Parse(hsv.Color);
            c2.R.Should().BeLessThan(10);
            c2.G.Should().BeGreaterThan(240);
            c2.B.Should().BeGreaterThan(240);
        }
        finally { w.Close(); }

        static void Dispatcher_Pump()
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [AvaloniaFact]
    public void Setting_the_colour_from_outside_moves_the_square_thumb()
    {
        var hsv = new HsvColorPicker { Color = "#FF0000", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var w = Host(hsv);
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var sv = hsv.FindControl<Border>("SvArea")!;
            var thumb = hsv.FindControl<Control>("SvThumb")!;
            var before = Canvas.GetLeft(thumb);
            hsv.Color = "#800000"; // stessa S e H, V dimezzata: cambia il Top, non il Left
            Canvas.GetTop(thumb).Should().BeGreaterThan(sv.Bounds.Height * 0.4);
            Canvas.GetLeft(thumb).Should().BeApproximately(before, 0.5);
        }
        finally { w.Close(); }
    }

    private static T In<T>(Control panel, string name) where T : Control =>
        panel.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    private static object? Danger(Control c) => c.TryFindResource("Brush.Danger", c.ActualThemeVariant, out var r) ? r : null;

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    [AvaloniaFact]
    public void Palette_is_the_specs_sixteen()
    {
        ColorSwatchPicker.Palette.Should().Equal("#C2405B", "#D0603A", "#C98A1E", "#9A9A2A", "#5F9A3C", "#2F9C8C", "#2F8FB0", "#2F7FC1",
            "#4F63C4", "#7A5BC4", "#A352A8", "#C2508A", "#8A6A4F", "#6B7280", "#4B5563", "#9A8F80");
    }

    [AvaloniaFact]
    public void Sixteen_circles_in_two_rows_of_eight()
    {
        var picker = new ColorSwatchPicker { Color = ColorSwatchPicker.Palette[0] };
        var w = Host(picker);
        try
        {
            var grid = In<Panel>(PanelOf(picker), "PaletteGrid");
            // disposto su una finestra: il pannello del flyout non si misura finché non è mostrato
            var panel = PanelOf(picker);
            ((Flyout)picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null).Flyout!).Content = null;
            var host = new Window { Width = 400, Height = 600, Content = panel };
            host.Show();
            Pump();
            var circles = grid.Children.OfType<Button>().ToList();
            circles.Should().HaveCount(16).And.OnlyContain(b => b.Bounds.Width == 28 && b.Bounds.Height == 28);
            circles.Select(b => Math.Round(b.TranslatePoint(default, host)!.Value.Y)).Distinct().Should().HaveCount(2);
            circles.Count(b => Math.Round(b.TranslatePoint(default, host)!.Value.Y) == Math.Round(circles[0].TranslatePoint(default, host)!.Value.Y)).Should().Be(8);
            host.Close();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Choosing_a_circle_sets_the_colour_and_rings_it()
    {
        var picker = new ColorSwatchPicker { Color = "#808080" };
        var w = Host(picker);
        try
        {
            var grid = In<Panel>(PanelOf(picker), "PaletteGrid");
            var c = grid.Children.OfType<Button>().ElementAt(5);
            Click(c);
            picker.Color.Should().Be(ColorSwatchPicker.Palette[5]);
            Ring(c).BorderThickness.Left.Should().Be(2);
            grid.Children.OfType<Button>().Where(b => Ring(b).BorderThickness.Left >= 2).Should().ContainSingle();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Advanced_is_closed_by_default()
    {
        var picker = new ColorSwatchPicker { Color = ColorSwatchPicker.Palette[3] };
        var w = Host(picker);
        try { In<Control>(PanelOf(picker), "AdvancedPanel").IsVisible.Should().BeFalse(); }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Advanced_opens_for_a_free_colour_but_not_for_a_recent_one()
    {
        var recent = new System.Collections.ObjectModel.ObservableCollection<string> { "#AA0000" };
        var picker = new ColorSwatchPicker { Color = "#123456", RecentColors = recent };
        var w = Host(picker);
        try
        {
            var panel = PanelOf(picker);
            In<Control>(panel, "AdvancedPanel").IsVisible.Should().BeTrue();
            picker.Color = "#AA0000";
            In<Control>(panel, "AdvancedPanel").IsVisible.Should().BeFalse("un recente non è un colore libero");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_advanced_row_toggles_the_panel()
    {
        var picker = new ColorSwatchPicker { Color = ColorSwatchPicker.Palette[0] };
        var w = Host(picker);
        try
        {
            var panel = PanelOf(picker);
            Click(In<Button>(panel, "AdvancedToggle"));
            In<Control>(panel, "AdvancedPanel").IsVisible.Should().BeTrue();
            AutomationProperties.GetName(In<Button>(panel, "AdvancedToggle")).Should().Be(Recordum.Presentation.Resources.Strings.ColorPicker_Advanced);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Preview_shows_light_and_dark_rows()
    {
        var picker = new ColorSwatchPicker { Color = "#C2405B", PreviewName = "Corsa" };
        var w = Host(picker);
        try
        {
            var panel = PanelOf(picker);
            In<Control>(panel, "PreviewSection").IsVisible.Should().BeTrue();
            var rows = panel.GetLogicalDescendants().OfType<RowPreview>().ToList();
            rows.Should().HaveCount(2);
            rows.Select(r => r.Variant).Should().BeEquivalentTo(new ThemeVariant?[] { ThemeVariant.Light, ThemeVariant.Dark });
            rows.Should().OnlyContain(r => r.Model != null && r.Model.Name == "Corsa" && r.Model.Color == "#C2405B");
            picker.Color = "#2F7FC1";
            rows.Should().OnlyContain(r => r.Model!.Color == "#2F7FC1");
            picker.PreviewName = "Altro";
            rows.Should().OnlyContain(r => r.Model!.Name == "Altro");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void No_preview_without_a_name()
    {
        var picker = new ColorSwatchPicker { Color = "#C2405B" };
        var w = Host(picker);
        try { In<Control>(PanelOf(picker), "PreviewSection").IsVisible.Should().BeFalse(); }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Favorites_live_in_advanced()
    {
        var favs = new System.Collections.ObjectModel.ObservableCollection<string>();
        var picker = new ColorSwatchPicker
        {
            Color = "#123456",
            FavoriteColors = favs,
            AddFavoriteCommand = new Relay(p => favs.Add((string)p!)),
        };
        var w = Host(picker);
        try
        {
            var panel = PanelOf(picker);
            var advanced = In<Control>(panel, "AdvancedPanel");
            Click(In<Button>(panel, "AddFavorite"));
            advanced.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag is "swatch" && AutomationProperties.GetName(b) == "#123456").Should().NotBeEmpty();
            In<Button>(panel, "AddFavorite").GetLogicalAncestors().Should().Contain(advanced);
        }
        finally { w.Close(); }
    }

    private static IBrush? Frame(TextBox t) => t.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement").BorderBrush;

    private static Color Solid(IBrush? b) => ((ISolidColorBrush)b!).Color;

    private static void Show(ColorSwatchPicker picker)
    {
        var trigger = picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null);
        ((Flyout)trigger.Flyout!).ShowAt(trigger);
        Pump();
    }

    [AvaloniaFact]
    public void Invalid_hex_keeps_the_colour_and_the_frame_is_danger_even_while_focused()
    {
        var picker = new ColorSwatchPicker { Color = "#808080" };
        var w = Host(picker);
        try
        {
            Show(picker);
            var hex = In<TextBox>(PanelOf(picker), "HexBox");
            hex.Focus();
            Pump();
            var focusFrame = Solid(Frame(hex));
            var danger = Solid((IBrush)Danger(hex)!);
            focusFrame.Should().NotBe(danger);
            hex.Text = "12345";
            Enter(hex);
            Pump();
            picker.Color.Should().Be("#808080");
            hex.IsFocused.Should().BeTrue();
            hex.Classes.Should().Contain("invalid");
            Solid(Frame(hex)).Should().Be(danger);

            hex.Text = "12345A";
            Enter(hex);
            Pump();
            picker.Color.Should().Be("#12345A");
            hex.Classes.Should().NotContain("invalid");
            Solid(Frame(hex)).Should().Be(focusFrame, "valido: torna la cornice di fuoco");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Reopening_resets_a_stale_invalid_hex()
    {
        var picker = new ColorSwatchPicker { Color = "#808080" };
        var w = Host(picker);
        try
        {
            Show(picker);
            var hex = In<TextBox>(PanelOf(picker), "HexBox");
            hex.Text = "zz";
            Enter(hex);
            hex.Classes.Should().Contain("invalid");
            var flyout = (Flyout)picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null).Flyout!;
            flyout.Hide();
            Pump();
            Show(picker);
            hex.Text.Should().Be("808080");
            hex.Classes.Should().NotContain("invalid");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_shade_ramp_spans_the_panel_width()
    {
        var picker = new ColorSwatchPicker { Color = "#123456" };
        var w = Host(picker);
        try
        {
            Show(picker);
            var panel = PanelOf(picker);
            var shades = panel.GetLogicalDescendants().OfType<WrapPanel>().Single(p => p.Children.Count == 7);
            shades.Children.Sum(c => c.Bounds.Width).Should().BeApproximately(8 * 28 + 7 * 8, 0.5);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Focus_starts_on_the_selected_circle()
    {
        var picker = new ColorSwatchPicker { Color = ColorSwatchPicker.Palette[6] };
        var w = Host(picker);
        try
        {
            Pump();
            var trigger = picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null);
            ((Flyout)trigger.Flyout!).ShowAt(trigger);
            Pump();
            var focused = TopLevel.GetTopLevel(PanelOf(picker))!.FocusManager!.GetFocusedElement();
            focused.Should().BeOfType<Button>();
            AutomationProperties.GetName((Button)focused!).Should().Be(ColorSwatchPicker.Palette[6]);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Screenshots_of_the_picker()
    {
        foreach (var (variant, open) in new[] { (ThemeVariant.Light, false), (ThemeVariant.Light, true), (ThemeVariant.Dark, false), (ThemeVariant.Dark, true) })
        {
            var recent = new System.Collections.ObjectModel.ObservableCollection<string> { "#2F7FC1", "#123456" };
            var favs = new System.Collections.ObjectModel.ObservableCollection<string> { "#C98A1E" };
            var picker = new ColorSwatchPicker { Color = open ? "#1B6F5A" : "#2F7FC1", PreviewName = "Corsa", RecentColors = recent, FavoriteColors = favs };
            var host = Host(picker);
            var panel = PanelOf(picker);
            ((Flyout)picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null).Flyout!).Content = null;
            var win = new Window { Width = 340, Height = open ? 760 : 420, RequestedThemeVariant = variant, Content = new Border { Padding = new Thickness(16), Child = panel } };
            win.Show();
            Pump();
            In<Control>(panel, "AdvancedPanel").IsVisible.Should().Be(open);
            ScreenshotProbeTests.Capture(win, $"picker-{variant.Key}-{(open ? "open" : "closed")}");
            win.Close();
            host.Close();
        }
    }

    private sealed class Relay(Action<object?> run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run(parameter);
    }
}
