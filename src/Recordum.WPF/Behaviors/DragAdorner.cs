using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Recordum.WPF.Behaviors;

/// <summary>
/// A snapshot of the dragged row, on an opaque card, that follows the cursor.
/// Single purpose: render the ghost. No domain logic.
/// </summary>
public sealed class DragAdorner : Adorner
{
    private const double CornerRadius = 6;
    private const double GhostOpacity = 0.95;

    private readonly Rect _bounds;
    private readonly ImageSource _snapshot;
    private readonly Brush _surface;
    private readonly Pen _borderPen;
    private Point _offset;

    public DragAdorner(UIElement adornedElement, FrameworkElement source) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _bounds = new Rect(new Size(source.ActualWidth, source.ActualHeight));

        // Freeze a bitmap of the row *now*, before the caller dims the original via IsBeingDragged.
        // A live VisualBrush would re-render the dimmed source every frame; a frozen snapshot is
        // crisp and cheaper to paint.
        var dpi = VisualTreeHelper.GetDpi(source);
        var snapshot = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        snapshot.Render(source);
        snapshot.Freeze();
        _snapshot = snapshot;

        // The habit row is translucent by design (~10% tint), so it must sit on an OPAQUE card or
        // the ghost looks see-through. Use the current theme's elevated surface + border.
        _surface = Resolve("Brush.SurfaceElevated", "Brush.Background") ?? Brushes.White;
        var borderBrush = Resolve("Brush.Border", "Brush.Accent") ?? Brushes.Gray;
        _borderPen = new Pen(borderBrush, 1);
        _borderPen.Freeze();
    }

    private static Brush? Resolve(params string[] keys)
    {
        foreach (var key in keys)
            if (Application.Current?.TryFindResource(key) is Brush b)
                return b;
        return null;
    }

    /// <summary>Position is the cursor point relative to the adorned element; the ghost is offset
    /// slightly down-right so the cursor stays visible.</summary>
    public void SetPosition(Point cursor)
    {
        _offset = new Point(cursor.X + 8, cursor.Y + 8);
        // The ghost is repositioned via _offset inside OnRender, so a render invalidation is all
        // that's needed — no adorner-layer arrange pass (we don't override GetDesiredTransform).
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(_offset, _bounds.Size);
        drawingContext.PushOpacity(GhostOpacity);
        // Opaque card behind the row content, with the row snapshot clipped to the rounded corners.
        drawingContext.PushClip(new RectangleGeometry(rect, CornerRadius, CornerRadius));
        drawingContext.DrawRectangle(_surface, null, rect);
        drawingContext.DrawImage(_snapshot, rect);
        drawingContext.Pop();
        // Border on top (outside the clip so the full stroke shows) to read as a lifted card.
        drawingContext.DrawRoundedRectangle(null, _borderPen, rect, CornerRadius, CornerRadius);
        drawingContext.Pop();
    }
}
