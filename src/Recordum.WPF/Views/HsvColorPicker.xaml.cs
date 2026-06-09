using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Recordum.WPF.Services;

namespace Recordum.WPF.Views;

/// <summary>
/// HSV color picker: a saturation/value square + a hue strip, both draggable. Two-way binds to
/// <see cref="Color"/> (#RRGGBB). Lets users pick any color visually instead of typing hex.
/// </summary>
public partial class HsvColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(string),
        typeof(HsvColorPicker),
        new FrameworkPropertyMetadata("#808080", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private double _h;
    private double _s = 1;
    private double _v = 1;
    private bool _updatingColor; // guards the Color round-trip from re-syncing the thumbs

    public HsvColorPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncFromColor();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (HsvColorPicker)d;
        if (picker._updatingColor) return; // our own write — thumbs already positioned
        picker.SyncFromColor();
    }

    private void SyncFromColor()
    {
        if (HsvColor.FromHex(Color) is { } hsv)
        {
            _h = hsv.H;
            _s = hsv.S;
            _v = hsv.V;
        }
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var (r, g, b) = HsvColor.HsvToRgb(_h, 1, 1);
        SvHueLayer.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));

        if (SvArea.ActualWidth > 0 && SvArea.ActualHeight > 0)
        {
            Canvas.SetLeft(SvThumb, _s * SvArea.ActualWidth - SvThumb.Width / 2);
            Canvas.SetTop(SvThumb, (1 - _v) * SvArea.ActualHeight - SvThumb.Height / 2);
        }
        if (HueArea.ActualWidth > 0)
            Canvas.SetLeft(HueThumb, (_h / 360.0) * HueArea.ActualWidth - HueThumb.Width / 2);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisuals();

    // --- Saturation/Value square ---

    private void OnSvMouseDown(object sender, MouseButtonEventArgs e)
    {
        SvArea.CaptureMouse();
        ApplySv(e.GetPosition(SvArea));
    }

    private void OnSvMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && SvArea.IsMouseCaptured)
            ApplySv(e.GetPosition(SvArea));
    }

    private void OnSvMouseUp(object sender, MouseButtonEventArgs e) => SvArea.ReleaseMouseCapture();

    private void ApplySv(Point p)
    {
        if (SvArea.ActualWidth <= 0 || SvArea.ActualHeight <= 0) return;
        _s = Math.Clamp(p.X / SvArea.ActualWidth, 0, 1);
        _v = Math.Clamp(1 - p.Y / SvArea.ActualHeight, 0, 1);
        PushColor();
    }

    // --- Hue strip ---

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        HueArea.CaptureMouse();
        ApplyHue(e.GetPosition(HueArea));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && HueArea.IsMouseCaptured)
            ApplyHue(e.GetPosition(HueArea));
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e) => HueArea.ReleaseMouseCapture();

    private void ApplyHue(Point p)
    {
        if (HueArea.ActualWidth <= 0) return;
        _h = Math.Clamp(p.X / HueArea.ActualWidth, 0, 1) * 360;
        PushColor();
    }

    private void PushColor()
    {
        _updatingColor = true;
        try { Color = HsvColor.ToHex(_h, _s, _v); }
        finally { _updatingColor = false; }
        UpdateVisuals();
    }
}
