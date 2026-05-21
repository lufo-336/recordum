using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Recordum.WPF.Views;

/// <summary>
/// 12-swatch palette plus a free-form hex TextBox. Two-way binds to <see cref="Color"/> (#RRGGBB string).
/// </summary>
public partial class ColorSwatchPicker : UserControl
{
    public static readonly ImmutableArray<string> Palette = ImmutableArray.Create(
        "#ef4444", "#f97316", "#f59e0b", "#84cc16",
        "#10b981", "#06b6d4", "#3b82f6", "#6366f1",
        "#8b5cf6", "#d946ef", "#ec4899", "#94a3b8");

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

    public ColorSwatchPicker()
    {
        InitializeComponent();
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is string hex)
        {
            Color = hex;
            e.Handled = true;
        }
    }
}
