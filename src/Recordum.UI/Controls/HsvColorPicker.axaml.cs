using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using HsvColor = Recordum.Presentation.Services.HsvColor;

namespace Recordum.UI.Controls;

/// <summary>Porting di <c>Views/HsvColorPicker.xaml(.cs)</c>: quadrato S/V + striscia H, entrambi trascinabili.
/// <see cref="Color"/> è "#RRGGBB" two-way. Senza contagocce (F2b decisione 5).</summary>
public partial class HsvColorPicker : UserControl
{
    public static readonly StyledProperty<string> ColorProperty =
        AvaloniaProperty.Register<HsvColorPicker, string>(nameof(Color), "#808080", defaultBindingMode: BindingMode.TwoWay);

    public string Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private double _h;
    private double _s = 1;
    private double _v = 1;
    private bool _updatingColor; // la nostra scrittura non deve riposizionare i cursori

    public HsvColorPicker()
    {
        InitializeComponent();
        SvArea.PointerPressed += (_, e) => Drag(e, SvArea, ApplySv, start: true);
        SvArea.PointerMoved += (_, e) => Drag(e, SvArea, ApplySv, start: false);
        SvArea.PointerReleased += (_, e) => Release(e, SvArea);
        HueArea.PointerPressed += (_, e) => Drag(e, HueArea, ApplyHue, start: true);
        HueArea.PointerMoved += (_, e) => Drag(e, HueArea, ApplyHue, start: false);
        HueArea.PointerReleased += (_, e) => Release(e, HueArea);
        SvArea.SizeChanged += (_, _) => UpdateVisuals();
        HueArea.SizeChanged += (_, _) => UpdateVisuals();
        SyncFromColor();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty && !_updatingColor) SyncFromColor();
    }

    private void SyncFromColor()
    {
        if (HsvColor.FromHex(Color) is { } hsv) (_h, _s, _v) = hsv;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var (r, g, b) = HsvColor.HsvToRgb(_h, 1, 1);
        SvHueLayer.Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, b));
        if (SvArea.Bounds.Width > 0 && SvArea.Bounds.Height > 0)
        {
            // L'area utile sta dentro il bordo da 1: la posizione del puntatore è relativa al Border intero.
            Canvas.SetLeft(SvThumb, _s * SvArea.Bounds.Width - SvThumb.Width / 2 - 1);
            Canvas.SetTop(SvThumb, (1 - _v) * SvArea.Bounds.Height - SvThumb.Height / 2 - 1);
        }
        if (HueArea.Bounds.Width > 0)
            Canvas.SetLeft(HueThumb, _h / 360.0 * HueArea.Bounds.Width - HueThumb.Width / 2 - 1);
    }

    private static void Drag(PointerEventArgs e, Border area, Action<Point> apply, bool start)
    {
        if (start)
        {
            if (!e.GetCurrentPoint(area).Properties.IsLeftButtonPressed) return;
            e.Pointer.Capture(area);
        }
        else if (!ReferenceEquals(e.Pointer.Captured, area)) return;
        apply(e.GetPosition(area));
        e.Handled = true;
    }

    private static void Release(PointerReleasedEventArgs e, Border area)
    {
        if (ReferenceEquals(e.Pointer.Captured, area)) e.Pointer.Capture(null);
    }

    private void ApplySv(Point p)
    {
        if (SvArea.Bounds.Width <= 0 || SvArea.Bounds.Height <= 0) return;
        _s = Math.Clamp(p.X / SvArea.Bounds.Width, 0, 1);
        _v = Math.Clamp(1 - p.Y / SvArea.Bounds.Height, 0, 1);
        PushColor();
    }

    private void ApplyHue(Point p)
    {
        if (HueArea.Bounds.Width <= 0) return;
        _h = Math.Clamp(p.X / HueArea.Bounds.Width, 0, 1) * 360;
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
