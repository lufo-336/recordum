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
using Avalonia.Styling;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.UI.Controls;

/// <summary>Tessera-trigger che apre un riquadro: sedici colori pronti, i recenti, "Come appare" (chiaro e scuro) e,
/// dietro "Avanzate", selettore libero, tonalità, preferiti ed esadecimale. <see cref="Color"/> è "#RRGGBB" two-way; un esadecimale scritto a mano si valida e uno non
/// valido non arriva mai alla proprietà (il campo torna al valore corrente).</summary>
public partial class ColorSwatchPicker : UserControl
{
    /// <summary>I sedici colori pronti della spec §7.1 (§4.3): l'unica fonte, la leggono anche i test di contrasto.</summary>
    public static readonly ImmutableArray<string> Palette = ImmutableArray.Create(
        "#C2405B", "#D0603A", "#C98A1E", "#9A9A2A", "#5F9A3C", "#2F9C8C", "#2F8FB0", "#2F7FC1",
        "#4F63C4", "#7A5BC4", "#A352A8", "#C2508A", "#8A6A4F", "#6B7280", "#4B5563", "#9A8F80");

    /// <summary>Il nome dell'abitudine per "Come appare"; null = niente anteprima (il segno delle note non ne ha).</summary>
    public static readonly StyledProperty<string?> PreviewNameProperty =
        AvaloniaProperty.Register<ColorSwatchPicker, string?>(nameof(PreviewName));

    public string? PreviewName { get => GetValue(PreviewNameProperty); set => SetValue(PreviewNameProperty, value); }

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

    private const double Gap = 8;
    private const double PanelWidth = 8 * 28 + 7 * Gap;

    private readonly Flyout _flyout = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };
    private readonly HsvColorPicker _hsv = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly WrapPanel _shades = new();
    private readonly WrapPanel _paletteGrid = new() { Name = "PaletteGrid", ItemSpacing = Gap, LineSpacing = Gap };
    private readonly TextBlock _recentLabel = Label(Strings.ColorPicker_Recent, new Thickness(0, 14, 0, 6));
    private readonly WrapPanel _recent = new() { Name = "RecentGrid", ItemSpacing = Gap, LineSpacing = Gap };
    private readonly WrapPanel _favorites = new();
    private readonly TextBox _hex = new() { Name = "HexBox", Width = 84, MaxLength = 7, FontFamily = new FontFamily("Consolas, DejaVu Sans Mono, monospace") };
    private readonly Border _preview = new() { Width = 24, Height = 24, Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius(5) };
    private readonly StackPanel _previewSection = new() { Name = "PreviewSection", Margin = new Thickness(0, 14, 0, 0), Spacing = 6, IsVisible = false };
    private readonly RowPreview _previewLight = new() { Variant = ThemeVariant.Light };
    private readonly RowPreview _previewDark = new() { Variant = ThemeVariant.Dark };
    private readonly StackPanel _advanced = new() { Name = "AdvancedPanel", IsVisible = false };
    private readonly Button _advancedToggle = new() { Name = "AdvancedToggle" };
    private readonly Avalonia.Controls.Shapes.Path _chevron = new() { Width = 12, Height = 12, Stretch = Stretch.Uniform, StrokeThickness = 1.5, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };

    public ColorSwatchPicker()
    {
        InitializeComponent();
        AutomationProperties.SetName(Trigger, Strings.HabitBuilder_Color);
        ToolTip.SetTip(Trigger, Strings.HabitBuilder_Color);
        _flyout.Content = BuildPanel();
        _flyout.Opening += (_, _) => { SyncAdvanced(); _hex.Text = Color.TrimStart('#'); _hex.Classes.Set("invalid", false); };
        _flyout.Opened += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(FocusSelected);
        Trigger.Flyout = _flyout;
        Refresh();
    }

    private StackPanel BuildPanel()
    {
        _hsv.PropertyChanged += (_, e) =>
        {
            if (e.Property == HsvColorPicker.ColorProperty && e.NewValue is string c) Color = c;
        };

        foreach (var hex in Palette) _paletteGrid.Children.Add(Swatch(hex, 28, radius: 14, margin: new Thickness(0)));

        // Come appare: la stessa riga della Home in chiaro e in scuro, con un valore di campione.
        var previewRows = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = Gap };
        Grid.SetColumn(_previewDark, 1);
        previewRows.Children.Add(_previewLight);
        previewRows.Children.Add(_previewDark);
        _previewSection.Children.Add(Label(Strings.ColorPicker_Preview));
        _previewSection.Children.Add(previewRows);

        // Avanzate ›: una riga che apre, sotto e nello stesso riquadro, tutto ciò che non serve a chi sceglie un colore pronto.
        _chevron.SetResourceReference(Avalonia.Controls.Shapes.Shape.StrokeProperty, "Brush.TextMuted");
        var toggleLabel = new TextBlock { Text = Strings.ColorPicker_Advanced, VerticalAlignment = VerticalAlignment.Center };
        toggleLabel.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        toggleLabel.SetResourceReference(TextBlock.FontSizeProperty, "FontSize.Small");
        _advancedToggle.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { toggleLabel, _chevron } };
        _advancedToggle.Background = Brushes.Transparent;
        _advancedToggle.BorderThickness = new Thickness(0);
        _advancedToggle.Padding = new Thickness(0, 4);
        _advancedToggle.Margin = new Thickness(0, 10, 0, 0);
        AutomationProperties.SetName(_advancedToggle, Strings.ColorPicker_Advanced);
        _advancedToggle.Click += (_, _) => SetAdvanced(!_advanced.IsVisible);

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

        _recentLabel.IsVisible = _recent.IsVisible = false;

        _advanced.Children.Add(new Border { Margin = new Thickness(0, 6, 0, 0), Child = _hsv });
        _advanced.Children.Add(Label(Strings.ColorPicker_Shades, new Thickness(0, 12, 0, 4)));
        _advanced.Children.Add(_shades);
        _advanced.Children.Add(favHeader);
        _advanced.Children.Add(_favorites);
        _advanced.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { hash, _hex, _preview },
        });

        return new StackPanel
        {
            Width = PanelWidth,
            Children = { _paletteGrid, _recentLabel, _recent, _previewSection, _advancedToggle, _advanced },
        };
    }

    private void SetAdvanced(bool open)
    {
        _advanced.IsVisible = open;
        _chevron.Data = Application.Current?.TryFindResource(open ? "Icon.ChevronDown" : "Icon.ChevronRight", out var g) == true ? g as Geometry : null;
    }

    // Avanzate è chiusa, salvo che il colore corrente non sia né nella palette né fra i recenti: chi ha un colore
    // libero lo vede subito. Mentre il riquadro è aperto non si muove da solo (trascinare nel selettore cambia Color).
    private void SyncAdvanced()
    {
        if (_flyout.IsOpen) return;
        var known = Palette.Contains(Color, StringComparer.OrdinalIgnoreCase)
                    || (RecentColors ?? []).Contains(Color, StringComparer.OrdinalIgnoreCase);
        SetAdvanced(!known);
    }

    private void FocusSelected()
    {
        var swatches = _paletteGrid.Children.Concat(_recent.Children).OfType<Button>().ToList();
        (swatches.FirstOrDefault(b => b.Classes.Contains("selected")) ?? swatches.FirstOrDefault())?.Focus();
    }

    private void UpdatePreview()
    {
        _previewSection.IsVisible = PreviewName is not null;
        if (PreviewName is null) return;
        var model = SamplePreviewFactory.HomeRowFor(HabitType.Action, KeyFigureKind.DoneToday, HomeStrip.None, PreviewName, Color, null);
        _previewLight.Model = _previewDark.Model = model;
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
        else if (change.Property == PreviewNameProperty) UpdatePreview();
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
        _hex.Classes.Set("invalid", false);

        _shades.Children.Clear();
        foreach (var c in ShadeRamp.For(Color)) _shades.Children.Add(Swatch(c, width: PanelWidth / 7, height: 22, radius: 0, margin: new Thickness(0)));

        foreach (var s in AllSwatches()) Mark(s);
        UpdatePreview();
        SyncAdvanced();
    }

    private IEnumerable<Button> AllSwatches() =>
        new Panel[] { _paletteGrid, _recent, _favorites, _shades }
            .SelectMany(p => p.Children).SelectMany(c => c is Grid g ? g.Children : [c]).OfType<Button>().Where(b => b.Tag is "swatch");

    private void FillRecent()
    {
        _recent.Children.Clear();
        foreach (var c in RecentColors ?? []) _recent.Children.Add(Swatch(c, 24, radius: 12, margin: new Thickness(0)));
        SyncAdvanced();
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
            _favorites.Children.Add(new Grid { Margin = new Thickness(2), Children = { Swatch(c, margin: new Thickness(0)), remove } });
        }
    }

    private Button Swatch(string hex, double width = 20, double height = 0, double radius = 4, Thickness? margin = null)
    {
        // Esterno = l'anello (2 px di vuoto + 2 px di inchiostro quando scelto), interno = il colore.
        var fill = new Border { Background = Brush(hex), CornerRadius = new CornerRadius(radius) };
        var ring = new Border { Child = fill, CornerRadius = new CornerRadius(radius > 0 ? radius : 0), Background = Brushes.Transparent };
        var b = new Button
        {
            Tag = "swatch",
            Width = width,
            Height = height == 0 ? width : height,
            MinHeight = 0,
            Padding = new Thickness(0),
            Margin = margin ?? new Thickness(2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = ring,
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
        var ring = (Border)b.Content!;
        var on = string.Equals(AutomationProperties.GetName(b), Color, StringComparison.OrdinalIgnoreCase);
        b.Classes.Set("selected", on);
        ring.BorderThickness = new Thickness(on ? 2 : 0);
        ring.Padding = new Thickness(on ? 2 : 0);
        if (on) ring.SetResourceReference(Border.BorderBrushProperty, "Brush.Ink");
        else ring.ClearValue(Border.BorderBrushProperty);
        // il colore chiaro su fondo chiaro ha bisogno di un filo: una tessera non scelta ha un bordo sottile
        var fill = (Border)ring.Child!;
        fill.BorderThickness = new Thickness(on ? 0 : 1);
        fill.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
    }

    private void CommitHex()
    {
        var text = (_hex.Text ?? string.Empty).Trim();
        if (HexRx.IsMatch(text))
        {
            Color = "#" + text.TrimStart('#').ToUpperInvariant();
            _hex.Text = Color.TrimStart('#');
            _hex.Classes.Set("invalid", false);
        }
        else
        {
            // Non valido: Color non cambia, il testo resta com'è e la casella lo dice con la cornice d'errore.
            _hex.Classes.Set("invalid", true);
        }
    }

    private static IBrush Brush(string hex) =>
        Avalonia.Media.Color.TryParse(hex, out var c) ? new ImmutableSolidColorBrush(c) : Brushes.Gray;
}
