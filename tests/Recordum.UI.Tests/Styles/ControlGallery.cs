using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Recordum.UI.Tests.Styles;

/// <summary>Una vista di prova costruita in C# (niente XAML nel progetto di produzione): i controlli
/// globali di Recordum, ognuno nel proprio angolo del canvas così i popup aperti (menu/combo/tooltip)
/// non si accavallano nello screenshot. Serve a GUARDARE gli stili nei due temi.</summary>
internal static class ControlGallery
{
    public sealed class Parts
    {
        public required Control Root { get; init; }
        public required Button PlainButton { get; init; }
        public required Button PrimaryButton { get; init; }
        public required TextBox Field { get; init; }
        public required CheckBox Checked { get; init; }
        public required CheckBox Unchecked { get; init; }
        public required RadioButton SegmentOn { get; init; }
        public required RadioButton SegmentOff { get; init; }
        public required ComboBox Combo { get; init; }
        public required ListBox List { get; init; }
        public required ScrollViewer Scroll { get; init; }
        public required Border MenuHost { get; init; }
        public required Border TipHost { get; init; }
        public required ContextMenu Menu { get; init; }
    }

    private static Control At(double x, double y, Control c)
    {
        Canvas.SetLeft(c, x);
        Canvas.SetTop(c, y);
        return c;
    }

    public static Parts Build()
    {
        var plain = new Button { Content = "Annulla" };
        var primary = new Button { Content = "Salva", Classes = { "primary" } };
        var field = new TextBox { Text = "Nome abitudine", Width = 180 };
        var on = new CheckBox { Content = "Ripetibile", IsChecked = true };
        var off = new CheckBox { Content = "Nascondi completate", IsChecked = false };
        var segOn = new RadioButton { Content = "Attive", GroupName = "g", IsChecked = true, Classes = { "segment" } };
        var segOff = new RadioButton { Content = "Archiviate", GroupName = "g", Classes = { "segment" } };
        var combo = new ComboBox { Width = 160, ItemsSource = new[] { "Manuale", "Nome", "Streak" }, SelectedIndex = 1 };
        var list = new ListBox { Width = 160, Height = 90, ItemsSource = new[] { "Home", "Grafici", "Backup" }, SelectedIndex = 1 };
        var scroll = new ScrollViewer
        {
            Height = 70, Width = 200,
            Content = new StackPanel { Children = { new TextBlock { Text = "Riga 1" }, new TextBlock { Text = "Riga 2" }, new TextBlock { Text = "Riga 3" }, new TextBlock { Text = "Riga 4" }, new TextBlock { Text = "Riga 5" }, new TextBlock { Text = "Riga 6" }, new TextBlock { Text = "Riga 7" }, new TextBlock { Text = "Riga 8" } } },
        };
        var menuHost = new Border { Width = 120, Height = 28, Background = Brushes.Transparent, Child = new TextBlock { Text = "menu qui" } };
        var tipHost = new Border { Width = 120, Height = 28, Background = Brushes.Transparent, Child = new TextBlock { Text = "tooltip qui" } };
        ToolTip.SetTip(tipHost, "Fissa in cima alla lista");
        ToolTip.SetPlacement(tipHost, PlacementMode.Bottom);
        var menu = new ContextMenu
        {
            ItemsSource = new object[]
            {
                new MenuItem { Header = "Rinomina" },
                new MenuItem { Header = "Fissa", ItemsSource = new object[] { new MenuItem { Header = "Nessuno" }, new MenuItem { Header = "Locale" } } },
                new Separator(),
                new MenuItem { Header = "Archivia" },
                new MenuItem { Header = "Elimina", IsEnabled = false },
            },
        };
        menuHost.ContextMenu = menu;

        // NOTA headless: un ContextMenu aperto via Open(target) non rispetta il PlacementTarget nello
        // stesso modo dei Flyout/Popup dei ComboBox — nello screenshot appare ancorato vicino
        // all'angolo alto-sinistra della finestra indipendentemente da dove sta menuHost. Per questo
        // menuHost vive DA SOLO in cima al canvas e tutto il resto sta sotto, fuori dalla sua zona
        // d'atterraggio: non è un bug del porting, è una scorciatoia headless da NON copiare nella
        // vista reale (lì il Flyout segue davvero la riga).
        var canvas = new Canvas { Width = 900, Height = 620 };
        canvas.Children.Add(At(500, 20, menuHost));
        canvas.Children.Add(At(20, 190, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { plain, primary, field } }));
        canvas.Children.Add(At(20, 240, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { on, off } }));
        canvas.Children.Add(At(20, 280, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { segOn, segOff } }));
        canvas.Children.Add(At(20, 330, list));
        canvas.Children.Add(At(220, 330, scroll));
        canvas.Children.Add(At(460, 190, combo));
        canvas.Children.Add(At(460, 430, tipHost));

        return new Parts
        {
            Root = new Border { Child = canvas, Width = 900, Height = 620 },
            PlainButton = plain, PrimaryButton = primary, Field = field, Checked = on, Unchecked = off,
            SegmentOn = segOn, SegmentOff = segOff, Combo = combo, List = list, Scroll = scroll,
            MenuHost = menuHost, TipHost = tipHost, Menu = menu,
        };
    }
}
