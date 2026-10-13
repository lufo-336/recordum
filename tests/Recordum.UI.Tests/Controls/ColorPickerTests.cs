using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
            hex.Text.Should().Be("808080", "il campo torna al valore valido");

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

    private sealed class Relay(Action<object?> run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run(parameter);
    }
}
