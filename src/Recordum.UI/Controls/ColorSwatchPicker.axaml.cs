using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.UI.Controls;

/// <summary>Porting di <c>Views/ColorSwatchPicker.xaml(.cs)</c>: tessera-trigger che apre un pannello con
/// il selettore HSV, la rampa di tonalità, la palette curata, i colori recenti, i preferiti e un campo
/// esadecimale. <see cref="Color"/> è "#RRGGBB" two-way; un esadecimale scritto a mano si valida e uno non
/// valido non arriva mai alla proprietà (il campo torna al valore corrente).</summary>
public partial class ColorSwatchPicker : UserControl
{
    public static readonly ImmutableArray<string> Palette = ImmutableArray.Create(
        "#fca5a5", "#ef4444", "#b91c1c",   // red
        "#fdba74", "#f97316", "#c2410c",   // orange
        "#fcd34d", "#f59e0b", "#b45309",   // amber
        "#bef264", "#84cc16", "#4d7c0f",   // lime
        "#6ee7b7", "#10b981", "#047857",   // emerald
        "#67e8f9", "#06b6d4", "#0e7490",   // cyan
        "#93c5fd", "#3b82f6", "#1d4ed8",   // blue
        "#a5b4fc", "#6366f1", "#4338ca",   // indigo
        "#c4b5fd", "#8b5cf6", "#6d28d9",   // violet
        "#f0abfc", "#d946ef", "#a21caf",   // fuchsia
        "#f9a8d4", "#ec4899", "#be185d",   // pink
        "#cbd5e1", "#94a3b8", "#475569");  // slate (neutrals)

    public static readonly StyledProperty<string> ColorProperty =
        AvaloniaProperty.Register<ColorSwatchPicker, string>(nameof(Color), "#808080", defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable<string>?> RecentColorsProperty =
        AvaloniaProperty.Register<ColorSwatchPicker, IEnumerable<string>?>(nameof(RecentColors));

    public static readonly StyledProperty<IEnumerable<string>?> FavoriteColorsProperty =
        AvaloniaProperty.Register<ColorSwatchPicker, IEnumerable<string>?>(nameof(FavoriteColors));

    public static readonly StyledProperty<ICommand?> AddFavoriteCommandProperty =
        AvaloniaProperty.Register<ColorSwatchPicker, ICommand?>(nameof(AddFavoriteCommand));

    public static readonly StyledProperty<ICommand?> RemoveFavoriteCommandProperty =
        AvaloniaProperty.Register<ColorSwatchPicker, ICommand?>(nameof(RemoveFavoriteCommand));

    public string Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IEnumerable<string>? RecentColors { get => GetValue(RecentColorsProperty); set => SetValue(RecentColorsProperty, value); }
    public IEnumerable<string>? FavoriteColors { get => GetValue(FavoriteColorsProperty); set => SetValue(FavoriteColorsProperty, value); }
    public ICommand? AddFavoriteCommand { get => GetValue(AddFavoriteCommandProperty); set => SetValue(AddFavoriteCommandProperty, value); }
    public ICommand? RemoveFavoriteCommand { get => GetValue(RemoveFavoriteCommandProperty); set => SetValue(RemoveFavoriteCommandProperty, value); }

    private static readonly Regex HexRx = new("^#?[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly Flyout _flyout = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };
    private readonly HsvColorPicker _hsv = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly WrapPanel _shades = new();
    private readonly WrapPanel _paletteGrid = new();
    private readonly TextBlock _recentLabel = new() { Text = Strings.ColorPicker_Recent, Margin = new Thickness(0, 8, 0, 2) };
    private readonly WrapPanel _recent = new();
    private readonly WrapPanel _favorites = new();
    private readonly TextBox _hex = new() { Name = "HexBox", Width = 84, MaxLength = 7, FontFamily = new FontFamily("Consolas, DejaVu Sans Mono, monospace") };
    private readonly Border _preview = new() { Width = 24, Height = 24, Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius(5) };

    public ColorSwatchPicker()
    {
        InitializeComponent();
        AutomationProperties.SetName(Trigger, Strings.HabitBuilder_Color);
        ToolTip.SetTip(Trigger, Strings.HabitBuilder_Color);
        _flyout.Content = BuildPanel();
        _flyout.Opened += (_, _) => _hex.Focus();
        Trigger.Flyout = _flyout;
        Refresh();
    }

    private StackPanel BuildPanel()
    {
        _hsv.PropertyChanged += (_, e) =>
        {
            if (e.Property == HsvColorPicker.ColorProperty && e.NewValue is string c) Color = c;
        };

        foreach (var hex in Palette) _paletteGrid.Children.Add(Swatch(hex));

        var add = new Button { Name = "AddFavorite", Content = Strings.ColorPicker_AddFavorite, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Right };
        add.SetResourceReference(TemplatedControl.ForegroundProperty, "Brush.Accent");
        AutomationProperties.SetName(add, Strings.ColorPicker_AddFavorite);
        add.Click += (_, _) => { if (AddFavoriteCommand is { } cmd && cmd.CanExecute(Color)) cmd.Execute(Color); };

        var favHeader = new DockPanel { Margin = new Thickness(0, 10, 0, 2) };
        var favLabel = Label(Strings.ColorPicker_Favorites);
        DockPanel.SetDock(favLabel, Dock.Left);
        DockPanel.SetDock(add, Dock.Right);
        favHeader.Children.Add(favLabel);
        favHeader.Children.Add(add);

        AutomationProperties.SetName(_hex, Strings.ColorPicker_Hex);
        _hex.LostFocus += (_, _) => CommitHex();
        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitHex(); e.Handled = true; } };
        var hash = new TextBlock { Text = "#", VerticalAlignment = VerticalAlignment.Center };
        hash.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        _preview.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        _preview.BorderThickness = new Thickness(1);

        _recentLabel.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        _recentLabel.SetResourceReference(TextBlock.FontSizeProperty, "FontSize.Small");

        var panel = new StackPanel
        {
            Width = 232,
            Children =
            {
                _hsv,
                Label(Strings.ColorPicker_Shades, new Thickness(0, 12, 0, 4)),
                _shades,
                new Border { Margin = new Thickness(0, 12, 0, 0), Child = _paletteGrid },
                _recentLabel,
                _recent,
                favHeader,
                _favorites,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = { hash, _hex, _preview },
                },
            },
        };
        return panel;
    }

    private static TextBlock Label(string text, Thickness? margin = null)
    {
        var t = new TextBlock { Text = text, Margin = margin ?? default };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        t.SetResourceReference(TextBlock.FontSizeProperty, "FontSize.Small");
        return t;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty) Refresh();
        else if (change.Property == RecentColorsProperty)
        {
            Watch(change.OldValue as INotifyCollectionChanged, change.NewValue as INotifyCollectionChanged, OnRecentChanged);
            FillRecent();
        }
        else if (change.Property == FavoriteColorsProperty)
        {
            Watch(change.OldValue as INotifyCollectionChanged, change.NewValue as INotifyCollectionChanged, OnFavoritesChanged);
            FillFavorites();
        }
    }

    // La collezione legata si riempie spesso DOPO il binding (LoadAsync del ViewModel): serve ascoltarla.
    private static void Watch(INotifyCollectionChanged? old, INotifyCollectionChanged? @new, NotifyCollectionChangedEventHandler h)
    {
        if (old is not null) old.CollectionChanged -= h;
        if (@new is not null) @new.CollectionChanged += h;
    }

    private void OnRecentChanged(object? s, NotifyCollectionChangedEventArgs e) => FillRecent();
    private void OnFavoritesChanged(object? s, NotifyCollectionChangedEventArgs e) => FillFavorites();

    private void Refresh()
    {
        TriggerSwatch.Background = Brush(Color);
        _preview.Background = Brush(Color);
        if (!string.Equals(_hsv.Color, Color, StringComparison.OrdinalIgnoreCase)) _hsv.Color = Color;
        _hex.Text = Color.TrimStart('#');

        _shades.Children.Clear();
        foreach (var c in ShadeRamp.For(Color)) _shades.Children.Add(Swatch(c, width: 232.0 / 7, height: 22, radius: 0, margin: default));

        foreach (var s in AllSwatches()) Mark(s);
    }

    private IEnumerable<Button> AllSwatches() =>
        new Panel[] { _paletteGrid, _recent, _favorites, _shades }
            .SelectMany(p => p.Children).SelectMany(c => c is Grid g ? g.Children : [c]).OfType<Button>().Where(b => b.Tag is "swatch");

    private void FillRecent()
    {
        _recent.Children.Clear();
        foreach (var c in RecentColors ?? []) _recent.Children.Add(Swatch(c));
        var any = _recent.Children.Count > 0;
        _recent.IsVisible = _recentLabel.IsVisible = any;
    }

    private void FillFavorites()
    {
        _favorites.Children.Clear();
        foreach (var c in FavoriteColors ?? [])
        {
            var glyph = new Avalonia.Controls.Shapes.Path
            {
                Width = 8, Height = 8, Stretch = Stretch.Uniform, StrokeThickness = 1.5, StrokeLineCap = PenLineCap.Round,
                Data = Application.Current?.TryFindResource("Icon.X", out var x) == true ? x as Geometry : null,
            };
            glyph.SetResourceReference(Avalonia.Controls.Shapes.Shape.StrokeProperty, "Brush.TextMuted");
            var remove = new Button
            {
                Tag = "remove",
                Content = glyph,
                Width = 13,
                Height = 13,
                MinHeight = 0,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -5, -5, 0),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            remove.SetResourceReference(TemplatedControl.BackgroundProperty, "Brush.SurfaceElevated");
            remove.SetResourceReference(TemplatedControl.BorderBrushProperty, "Brush.Border");
            remove.SetResourceReference(TemplatedControl.ForegroundProperty, "Brush.TextMuted");
            AutomationProperties.SetName(remove, $"{Strings.ColorPicker_RemoveFavorite} {c}");
            ToolTip.SetTip(remove, Strings.ColorPicker_RemoveFavorite);
            var captured = c;
            remove.Click += (_, _) => { if (RemoveFavoriteCommand is { } cmd && cmd.CanExecute(captured)) cmd.Execute(captured); };
            _favorites.Children.Add(new Grid { Margin = new Thickness(2), Children = { Swatch(c, margin: default), remove } });
        }
    }

    private Button Swatch(string hex, double width = 20, double height = 20, double radius = 4, Thickness? margin = null)
    {
        var inner = new Border { Background = Brush(hex), CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
        var b = new Button
        {
            Tag = "swatch",
            Width = width,
            Height = height,
            MinHeight = 0,
            Padding = new Thickness(0),
            Margin = margin ?? new Thickness(2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = inner,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        AutomationProperties.SetName(b, hex);
        b.Click += (_, _) => { Color = hex; _flyout.Hide(); Trigger.Focus(); };
        Mark(b);
        return b;
    }

    // Il segno di selezione sta sull'anello della tessera interna: il Button di Fluent ridisegna sfondo e
    // bordo al passaggio del mouse, un Border dentro il contenuto no.
    private void Mark(Button b)
    {
        var inner = (Border)b.Content!;
        var on = string.Equals(AutomationProperties.GetName(b), Color, StringComparison.OrdinalIgnoreCase);
        b.Classes.Set("selected", on);
        inner.BorderThickness = new Thickness(on ? 2 : 1);
        inner.SetResourceReference(Border.BorderBrushProperty, on ? "Brush.Accent" : "Brush.Border");
    }

    private void CommitHex()
    {
        var text = (_hex.Text ?? string.Empty).Trim();
        if (HexRx.IsMatch(text)) Color = "#" + text.TrimStart('#').ToUpperInvariant();
        _hex.Text = Color.TrimStart('#'); // valido: normalizzato; non valido: torna al valore corrente
    }

    private static IBrush Brush(string hex) =>
        Avalonia.Media.Color.TryParse(hex, out var c) ? new ImmutableSolidColorBrush(c) : Brushes.Gray;
}
