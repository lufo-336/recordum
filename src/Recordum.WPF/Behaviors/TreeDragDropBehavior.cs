using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Recordum.WPF.Interop;
using Recordum.WPF.ViewModels;

namespace Recordum.WPF.Behaviors;

public static class TreeDragDropBehavior
{
    public enum DropZone
    {
        Before,
        Onto,
        After,
    }

    /// <summary>Resolved drop intent: new parent, the sibling to anchor next to (null = append), and side.</summary>
    public readonly record struct DropPlan(Guid? Parent, Guid? Anchor, bool PlaceAfter);

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(TreeDragDropBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static Point _start;
    private static bool _armed;
    private static HomeRowViewModel? _indicated;
    private static DragGhost? _ghost;

    internal static DropZone ResolveZone(double relative) =>
        relative < 0.4 ? DropZone.Before :
        relative < 0.7 ? DropZone.Onto :
        DropZone.After;

    /// <summary>
    /// Pure mapping from a hovered zone+target to a drop plan.
    /// Onto a group → append inside it. Onto a habit → sibling after it. Before/After → reorder around the target.
    /// </summary>
    public static DropPlan ResolveDrop(DropZone zone, bool targetIsGroup, Guid targetId, Guid? targetParentId) => zone switch
    {
        DropZone.Onto when targetIsGroup => new DropPlan(targetId, null, false),
        DropZone.Before => new DropPlan(targetParentId, targetId, false),
        // After, and Onto-on-a-habit, both place the row right after the target among its siblings.
        _ => new DropPlan(targetParentId, targetId, true),
    };

    /// <summary>Risale l'albero visuale dal punto dato al primo <see cref="FrameworkElement"/> il cui
    /// <c>Style</c> è (reference-equal) lo <paramref name="style"/> dato. Usato per trovare la card di
    /// riga (Border "Recordum.HabitBlock") partendo dal sotto-elemento sotto il cursore.</summary>
    internal static FrameworkElement? FindAncestorWithStyle(DependencyObject? origin, Style style)
    {
        // VisualTreeHelper.GetParent lancia su un ContentElement (es. Run): risali solo finché sei su un
        // Visual/Visual3D, così il metodo è totale (ritorna null) anche se il chiamante passa un non-Visual.
        for (var d = origin; d is Visual or Visual3D; d = VisualTreeHelper.GetParent(d))
            if (d is FrameworkElement fe && ReferenceEquals(fe.Style, style))
                return fe;
        return null;
    }

    /// <summary>La card di riga da fotografare: il Border "Recordum.HabitBlock" antenato del punto
    /// afferrato. Fallback al container della riga (ContentPresenter) se lo Style non si risolve — comunque
    /// a livello-riga, mai un sotto-elemento. Null solo in casi difensivi non attesi.</summary>
    private static FrameworkElement? FindRowCard(DependencyObject? origin, ItemsControl ic)
    {
        if (Application.Current?.TryFindResource("Recordum.HabitBlock") is Style cardStyle
            && FindAncestorWithStyle(origin, cardStyle) is { } card)
            return card;
        // Fallback: ContainerFromElement risolve solo container dell'`ic` radice → null per righe in un
        // ItemsControl annidato (gruppi). Quelle righe contano sul percorso style-match qui sopra: se lo
        // Style "Recordum.HabitBlock" cambia/sparisce, le righe annidate resterebbero senza ghost.
        return ItemsControl.ContainerFromElement(ic, origin) as FrameworkElement;
    }

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic) return;
        if (e.NewValue is true)
        {
            ic.PreviewMouseLeftButtonDown += OnDown;
            ic.PreviewMouseMove += OnMove;
            ic.DragOver += OnDragOver;
            ic.DragLeave += OnDragLeave;
            ic.Drop += OnDrop;
            ic.AllowDrop = true;
        }
        else
        {
            ic.PreviewMouseLeftButtonDown -= OnDown;
            ic.PreviewMouseMove -= OnMove;
            ic.DragOver -= OnDragOver;
            ic.DragLeave -= OnDragLeave;
            ic.Drop -= OnDrop;
            ic.AllowDrop = false;
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(sender as IInputElement);
        _armed = true;
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (!_armed || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(sender as IInputElement);
        if ((p - _start).Length < 5) return;
        if (sender is ItemsControl ic
            && e.OriginalSource is FrameworkElement fe
            && fe.DataContext is HomeRowViewModel src)
        {
            _armed = false;
            var card = FindRowCard(fe, ic);
            _ghost = card is not null ? new DragGhost(card) : null;
            src.IsBeingDragged = true;

            // QueryContinueDrag scatta a ogni movimento del mouse (molto più fitto di DragOver) e ovunque
            // sullo schermo; i suoi args non portano la posizione, quindi la leggiamo da GetCursorPos.
            void OnQueryContinue(object? s, QueryContinueDragEventArgs args)
            {
                if (_ghost is not null && NativeMethods.TryGetCursorPos() is { } pos)
                    _ghost.SetScreenPosition(pos);
            }
            // `fe` funge anche da sorgente passata a DoDragDrop: QueryContinueDrag scatta su QUELL'elemento,
            // quindi l'handler va agganciato qui. Se un refactor separasse sorgente e hover, andrebbe rivisto.
            fe.QueryContinueDrag += OnQueryContinue;
            try
            {
                DragDrop.DoDragDrop(fe, src, DragDropEffects.Move);
            }
            finally
            {
                // DoDragDrop è bloccante; questo gira al drop O all'annullamento, ovunque finisca.
                fe.QueryContinueDrag -= OnQueryContinue;
                _ghost?.Dispose();
                _ghost = null;
                src.IsBeingDragged = false;
                SetIndicator(null, false, RowDropEdge.None);
            }
        }
    }

    // Drop affordance: a single indicator at a time — either the group "into" highlight
    // (IsDropTarget) or an insertion edge (DropEdge) on the hovered sibling.
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(HomeRowViewModel)) is HomeRowViewModel src
            && sender is ItemsControl ic
            && FindRowCard(e.OriginalSource as DependencyObject, ic) is { DataContext: HomeRowViewModel row } card
            && !ReferenceEquals(row, src))
        {
            // Zone from the row CARD, not whatever child (text / sparkline) raised the event, so the
            // Before/Onto/After thirds map to the real row height — same card the ghost uses (CR17).
            var zone = ResolveZone(e.GetPosition(card).Y / Math.Max(1, card.ActualHeight));
            if (zone == DropZone.Onto && row.IsGroup)
                SetIndicator(row, isGroupTarget: true, RowDropEdge.None);
            else
                SetIndicator(row, isGroupTarget: false, zone == DropZone.Before ? RowDropEdge.Before : RowDropEdge.After);
        }
        else
        {
            SetIndicator(null, false, RowDropEdge.None);
        }
    }

    private static void OnDragLeave(object sender, DragEventArgs e) => SetIndicator(null, false, RowDropEdge.None);

    private static void SetIndicator(HomeRowViewModel? row, bool isGroupTarget, RowDropEdge edge)
    {
        if (_indicated is not null)
        {
            _indicated.IsDropTarget = false;
            _indicated.DropEdge = RowDropEdge.None;
        }
        _indicated = row;
        if (_indicated is not null)
        {
            _indicated.IsDropTarget = isGroupTarget;
            _indicated.DropEdge = edge;
        }
    }

    private static async void OnDrop(object sender, DragEventArgs e)
    {
        SetIndicator(null, false, RowDropEdge.None);
        if (sender is not ItemsControl ic) return;
        if (e.Data.GetData(typeof(HomeRowViewModel)) is not HomeRowViewModel src) return;
        if (e.OriginalSource is not FrameworkElement fe || fe.DataContext is not HomeRowViewModel target) return;
        if (ReferenceEquals(src, target)) return;
        if (ic.DataContext is not HomeViewModel home) return;

        var zone = ResolveZone(e.GetPosition(fe).Y / Math.Max(1, fe.ActualHeight));
        var plan = ResolveDrop(zone, target.IsGroup, target.HabitNodeId, target.ParentId);
        await home.ApplyDropAsync(src, plan);
    }
}
