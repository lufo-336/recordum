using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
        new FrameworkPropertyMetadata("#808080", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
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
        => ((ColorSwatchPicker)d).HasRecentColors = e.NewValue is IEnumerable<string> s && s.Any();

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
        if (sender is FrameworkElement fe && fe.DataContext is string hex)
        {
            Color = hex;
            PickerPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void OnTriggerClick(object sender, MouseButtonEventArgs e)
    {
        PickerPopup.IsOpen = !PickerPopup.IsOpen;
        e.Handled = true;
    }
}
