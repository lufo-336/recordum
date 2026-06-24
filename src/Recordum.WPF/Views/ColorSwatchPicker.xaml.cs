using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Recordum.WPF.Views;

/// <summary>
/// Compact trigger swatch that opens a Popup with a curated palette grid, a recent-colors row,
/// and a hex entry. Two-way binds to <see cref="Color"/> (#RRGGBB string).
/// </summary>
public partial class ColorSwatchPicker : UserControl
{
    public static readonly ImmutableArray<string> Palette = ImmutableArray.Create(
        // hue ramps: light / base / dark  (Tailwind 300/500/700-ish)
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

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(string),
        typeof(ColorSwatchPicker),
        new FrameworkPropertyMetadata("#808080", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public ObservableCollection<string> Shades { get; } = new();

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ColorSwatchPicker)d;
        self.Shades.Clear();
        foreach (var c in Services.ShadeRamp.For((string)e.NewValue)) self.Shades.Add(c);
    }

    public static readonly DependencyProperty RecentColorsProperty = DependencyProperty.Register(
        nameof(RecentColors),
        typeof(IEnumerable<string>),
        typeof(ColorSwatchPicker),
        new PropertyMetadata(System.Array.Empty<string>(), OnRecentColorsChanged));

    public IEnumerable<string> RecentColors
    {
        get => (IEnumerable<string>)GetValue(RecentColorsProperty);
        set => SetValue(RecentColorsProperty, value);
    }

    // Computed DP (not a plain CLR property) so the recents-row Visibility binding refreshes
    // whenever RecentColors changes — a plain getter wouldn't notify the binding engine.
    public static readonly DependencyProperty HasRecentColorsProperty = DependencyProperty.Register(
        nameof(HasRecentColors),
        typeof(bool),
        typeof(ColorSwatchPicker),
        new PropertyMetadata(false));

    public bool HasRecentColors
    {
        get => (bool)GetValue(HasRecentColorsProperty);
        private set => SetValue(HasRecentColorsProperty, value);
    }

    private static void OnRecentColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ColorSwatchPicker)d;
        // The bound collection is often populated async AFTER this binding resolves (editor LoadAsync),
        // so track CollectionChanged too — otherwise HasRecentColors stays stuck at its bind-time value
        // and the recents row never appears.
        if (e.OldValue is INotifyCollectionChanged oldNcc) oldNcc.CollectionChanged -= picker.OnRecentColorsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newNcc) newNcc.CollectionChanged += picker.OnRecentColorsCollectionChanged;
        picker.RefreshHasRecentColors();
    }

    private void OnRecentColorsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshHasRecentColors();

    private void RefreshHasRecentColors() => HasRecentColors = RecentColors?.Any() == true;

    public static readonly DependencyProperty FavoriteColorsProperty = DependencyProperty.Register(
        nameof(FavoriteColors),
        typeof(IEnumerable<string>),
        typeof(ColorSwatchPicker),
        new PropertyMetadata(System.Array.Empty<string>()));

    public IEnumerable<string> FavoriteColors
    {
        get => (IEnumerable<string>)GetValue(FavoriteColorsProperty);
        set => SetValue(FavoriteColorsProperty, value);
    }

    public static readonly DependencyProperty AddFavoriteCommandProperty = DependencyProperty.Register(
        nameof(AddFavoriteCommand),
        typeof(System.Windows.Input.ICommand),
        typeof(ColorSwatchPicker),
        new PropertyMetadata(null));

    public System.Windows.Input.ICommand? AddFavoriteCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(AddFavoriteCommandProperty);
        set => SetValue(AddFavoriteCommandProperty, value);
    }

    public static readonly DependencyProperty RemoveFavoriteCommandProperty = DependencyProperty.Register(
        nameof(RemoveFavoriteCommand),
        typeof(System.Windows.Input.ICommand),
        typeof(ColorSwatchPicker),
        new PropertyMetadata(null));

    public System.Windows.Input.ICommand? RemoveFavoriteCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(RemoveFavoriteCommandProperty);
        set => SetValue(RemoveFavoriteCommandProperty, value);
    }

    public ColorSwatchPicker()
    {
        InitializeComponent();
        // The Popup content lives in a separate visual tree, so RelativeSource FindAncestor can't
        // reach this control from inside it. Point the popup's DataContext at the control instead,
        // and the popup-internal bindings use plain paths (Color / RecentColors / HasRecentColors).
        PickerPopup.DataContext = this;
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        PickFrom(sender);
        e.Handled = true;
    }

    private void OnSwatchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            PickFrom(sender);
            e.Handled = true;
        }
    }

    private void PickFrom(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is string hex)
        {
            Color = hex;
            PickerPopup.IsOpen = false;
            Trigger.Focus();
        }
    }

    private void OnTriggerClick(object sender, MouseButtonEventArgs e)
    {
        PickerPopup.IsOpen = !PickerPopup.IsOpen;
        e.Handled = true;
    }

    private void OnTriggerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            PickerPopup.IsOpen = !PickerPopup.IsOpen;
            e.Handled = true;
        }
    }

    // Move keyboard focus into the popup when it opens so keyboard users land on a swatch
    // (Tab then cycles, Enter/Space picks, Escape closes).
    private void OnPopupOpened(object? sender, System.EventArgs e)
        => Dispatcher.BeginInvoke(
            new System.Action(() => (FirstFocusable(PaletteItems) ?? (IInputElement)PopupRoot).Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

    private void OnPopupKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            PickerPopup.IsOpen = false;
            Trigger.Focus();
            e.Handled = true;
        }
    }

    private static IInputElement? FirstFocusable(DependencyObject root)
    {
        if (root is UIElement { Focusable: true, IsEnabled: true, Visibility: Visibility.Visible } ue)
            return ue;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FirstFocusable(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }
}
