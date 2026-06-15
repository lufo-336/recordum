using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Recordum.WPF.Behaviors;

/// <summary>
/// Ghost di trascinamento: uno snapshot della card di riga su un Popup topmost che segue il cursore.
/// Singolo scopo: rendere il ghost e seguirlo. Nessuna logica di dominio.
/// </summary>
public sealed class DragGhost : IDisposable
{
    private const double CornerRadius = 6;
    private const double GhostOpacity = 0.95;
    private const double CursorOffset = 8; // il ghost sta giù-a-destra così il cursore resta visibile

    private readonly Popup _popup;
    private readonly Visual _source; // per la matrice DPI della sorgente di presentazione
    private bool _opened;
    private bool _disposed;

    public DragGhost(FrameworkElement rowCard)
    {
        _source = rowCard;

        // Fotografa la card *ora*, prima che il chiamante la sbiadisca via IsBeingDragged.
        // Snapshot al DPI corrente => nitido; disegnato a dimensione logica => 1:1.
        var image = new Image
        {
            Source = Snapshot(rowCard),
            Width = rowCard.ActualWidth,
            Height = rowCard.ActualHeight,
            Stretch = Stretch.Fill,
        };

        // La card abitudine è translucida per design (~10% tint): l'opacità del ghost deve venire dallo
        // sfondo OPACO del Border, non dal contenuto, o il ghost sembra trasparente.
        var frame = new Border
        {
            Background = Resolve("Brush.SurfaceElevated", "Brush.Background") ?? Brushes.White,
            BorderBrush = Resolve("Brush.Border", "Brush.Accent") ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CornerRadius),
            Opacity = GhostOpacity,
            SnapsToDevicePixels = true,
            Child = image,
        };

        _popup = new Popup
        {
            AllowsTransparency = true,
            IsHitTestVisible = false,
            AllowDrop = false,
            StaysOpen = true,
            Placement = PlacementMode.Absolute,
            PopupAnimation = PopupAnimation.None,
            Child = frame,
        };
    }

    /// <summary>True quando il popup è aperto (cioè dopo la prima <see cref="SetScreenPosition"/>).</summary>
    internal bool IsOpen => _popup.IsOpen;

    /// <summary>Riposiziona il ghost al cursore. <paramref name="physicalCursor"/> è in pixel fisici
    /// dello schermo (da <c>GetCursorPos</c>); viene convertito in DIU per gli offset del Popup.</summary>
    public void SetScreenPosition(Point physicalCursor)
    {
        if (_disposed) return;

        var transform = PresentationSource.FromVisual(_source)?.CompositionTarget?.TransformFromDevice
                        ?? Matrix.Identity;
        var dip = PhysicalToDip(physicalCursor, transform);

        _popup.HorizontalOffset = dip.X + CursorOffset;
        _popup.VerticalOffset = dip.Y + CursorOffset;

        if (!_opened)
        {
            _popup.IsOpen = true; // ritardato alla prima posizione per evitare un flash a (0,0)
            _opened = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _popup.IsOpen = false;
        _popup.Child = null;
    }

    /// <summary>Converte pixel fisici dello schermo in unità WPF (DIU) data la matrice
    /// <c>TransformFromDevice</c> della sorgente di presentazione.</summary>
    internal static Point PhysicalToDip(Point physical, Matrix transformFromDevice)
        => transformFromDevice.Transform(physical);

    private static RenderTargetBitmap Snapshot(FrameworkElement source)
    {
        var dpi = VisualTreeHelper.GetDpi(source);
        var bmp = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(source);
        bmp.Freeze();
        return bmp;
    }

    private static Brush? Resolve(params string[] keys)
    {
        foreach (var key in keys)
            if (Application.Current?.TryFindResource(key) is Brush b)
                return b;
        return null;
    }
}
