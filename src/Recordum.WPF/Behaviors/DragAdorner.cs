using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Recordum.WPF.Behaviors;

/// <summary>
/// A semi-transparent snapshot of the dragged row that follows the cursor.
/// Single purpose: render the ghost. No domain logic.
/// </summary>
public sealed class DragAdorner : Adorner
{
    private readonly Rect _bounds;
    private readonly Brush _ghost;
    private Point _offset;

    public DragAdorner(UIElement adornedElement, FrameworkElement source) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _bounds = new Rect(new Size(source.ActualWidth, source.ActualHeight));
        // ~0.55 effective opacity (0.65 brush x 0.85 in OnRender) — a faint ghost, restrained per the design brief.
        _ghost = new VisualBrush(source) { Opacity = 0.65, Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
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
        drawingContext.PushOpacity(0.85);
        drawingContext.DrawRectangle(_ghost, null, new Rect(_offset, _bounds.Size));
        drawingContext.Pop();
    }
}
