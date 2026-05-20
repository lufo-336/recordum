using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Recordum.Core.Errors;
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

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(TreeDragDropBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static Point _start;
    private static bool _armed;

    internal static DropZone ResolveZone(double relative) =>
        relative < 0.4 ? DropZone.Before :
        relative < 0.7 ? DropZone.Onto :
        DropZone.After;

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic) return;
        if (e.NewValue is true)
        {
            ic.PreviewMouseLeftButtonDown += OnDown;
            ic.PreviewMouseMove += OnMove;
            ic.Drop += OnDrop;
            ic.AllowDrop = true;
        }
        else
        {
            ic.PreviewMouseLeftButtonDown -= OnDown;
            ic.PreviewMouseMove -= OnMove;
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
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is HomeRowViewModel src)
        {
            _armed = false;
            DragDrop.DoDragDrop(fe, src, DragDropEffects.Move);
        }
    }

    private static async void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl ic) return;
        if (e.Data.GetData(typeof(HomeRowViewModel)) is not HomeRowViewModel src) return;
        if (e.OriginalSource is not FrameworkElement fe || fe.DataContext is not HomeRowViewModel target) return;
        if (ReferenceEquals(src, target)) return;

        var p = e.GetPosition(fe);
        var zone = ResolveZone(p.Y / Math.Max(1, fe.ActualHeight));
        Guid? newParent = zone == DropZone.Onto && target.IsGroup
            ? target.HabitNodeId
            : target.ParentId;

        try
        {
            await src.MoveToAsync(newParent);
        }
        catch (CyclicHabitNodeMoveException)
        {
            return;
        }

        if (ic.DataContext is HomeViewModel home)
            await home.LoadCommand.ExecuteAsync(null);
    }
}
