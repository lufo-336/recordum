using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
    private static DragAdorner? _dragAdorner;

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
            var layer = AdornerLayer.GetAdornerLayer(ic);
            _dragAdorner = new DragAdorner(ic, fe);
            layer?.Add(_dragAdorner);
            src.IsBeingDragged = true;
            try
            {
                DragDrop.DoDragDrop(fe, src, DragDropEffects.Move);
            }
            finally
            {
                // DoDragDrop is blocking; this runs on drop OR cancel, wherever it ends.
                if (layer is not null && _dragAdorner is not null) layer.Remove(_dragAdorner);
                _dragAdorner = null;
                src.IsBeingDragged = false;
                SetIndicator(null, false, RowDropEdge.None);
            }
        }
    }

    // Drop affordance: a single indicator at a time — either the group "into" highlight
    // (IsDropTarget) or an insertion edge (DropEdge) on the hovered sibling.
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        _dragAdorner?.SetPosition(e.GetPosition(sender as IInputElement));
        if (e.Data.GetData(typeof(HomeRowViewModel)) is HomeRowViewModel src
            && e.OriginalSource is FrameworkElement fe
            && fe.DataContext is HomeRowViewModel row
            && !ReferenceEquals(row, src))
        {
            var zone = ResolveZone(e.GetPosition(fe).Y / Math.Max(1, fe.ActualHeight));
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
