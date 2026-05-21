using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Recordum.WPF.Views;

/// <summary>
/// Bound to <see cref="Icon"/> — a resource key into <c>Resources/Icons.xaml</c>.
/// Resolves the geometry from the application resources and shows it; falls back to empty Path
/// when the key is unknown. Full Lucide picker grid lands in a later milestone.
/// </summary>
public partial class IconPicker : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(string),
        typeof(IconPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIconChanged));

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty SelectedIconGeometryProperty = DependencyProperty.Register(
        nameof(SelectedIconGeometry),
        typeof(Geometry),
        typeof(IconPicker),
        new PropertyMetadata(null));

    public Geometry? SelectedIconGeometry
    {
        get => (Geometry?)GetValue(SelectedIconGeometryProperty);
        private set => SetValue(SelectedIconGeometryProperty, value);
    }

    public IconPicker()
    {
        InitializeComponent();
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (IconPicker)d;
        if (e.NewValue is not string key || string.IsNullOrWhiteSpace(key))
        {
            picker.SelectedIconGeometry = null;
            return;
        }
        if (Application.Current?.TryFindResource(key) is Geometry geometry)
            picker.SelectedIconGeometry = geometry;
        else
            picker.SelectedIconGeometry = null;
    }
}
