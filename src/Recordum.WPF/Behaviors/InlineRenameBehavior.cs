using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Recordum.Presentation.ViewModels;

namespace Recordum.WPF.Behaviors;

public static class InlineRenameBehavior
{
    public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached(
        "Attach",
        typeof(bool),
        typeof(InlineRenameBehavior),
        new PropertyMetadata(false, OnAttachChanged));

    public static void SetAttach(DependencyObject d, bool value) => d.SetValue(AttachProperty, value);
    public static bool GetAttach(DependencyObject d) => (bool)d.GetValue(AttachProperty);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if (e.NewValue is true)
        {
            tb.KeyDown += OnKey;
            tb.LostFocus += OnLostFocus;
            tb.IsVisibleChanged += OnVisibleChanged;
        }
        else
        {
            tb.KeyDown -= OnKey;
            tb.LostFocus -= OnLostFocus;
            tb.IsVisibleChanged -= OnVisibleChanged;
        }
    }

    private static void OnVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && e.NewValue is true)
        {
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }));
        }
    }

    private static void OnKey(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not HomeRowViewModel vm) return;
        switch (e.Key)
        {
            case Key.Enter:
                if (vm.CommitRenameCommand.CanExecute(null))
                    vm.CommitRenameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (vm.CancelRenameCommand.CanExecute(null))
                    vm.CancelRenameCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is HomeRowViewModel vm && vm.IsRenaming
            && vm.CommitRenameCommand.CanExecute(null))
        {
            vm.CommitRenameCommand.Execute(null);
        }
    }
}
