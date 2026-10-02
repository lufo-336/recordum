using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Behaviors;

public static class InlineRenameBehavior
{
    public static readonly AttachedProperty<bool> AttachProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Attach", typeof(InlineRenameBehavior));

    public static bool GetAttach(TextBox element) => element.GetValue(AttachProperty);
    public static void SetAttach(TextBox element, bool value) => element.SetValue(AttachProperty, value);

    static InlineRenameBehavior()
    {
        AttachProperty.Changed.AddClassHandler<TextBox>(OnAttachChanged);
    }

    private static void OnAttachChanged(TextBox tb, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<bool>())
        {
            tb.KeyDown += OnKey;
            tb.LostFocus += OnLostFocus;
            tb.PropertyChanged += OnTextBoxPropertyChanged;
        }
        else
        {
            tb.KeyDown -= OnKey;
            tb.LostFocus -= OnLostFocus;
            tb.PropertyChanged -= OnTextBoxPropertyChanged;
        }
    }

    // Avalonia non ha IsVisibleChanged: si guarda la proprietà. Il fuoco si dà differito, perché
    // nell'istante del cambio il TextBox non ha ancora fatto il proprio passaggio di layout.
    private static void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TextBox tb || e.Property != Visual.IsVisibleProperty) return;
        if (e.GetNewValue<bool>() is not true) return;
        Dispatcher.UIThread.Post(() =>
        {
            tb.Focus();
            tb.SelectAll();
        });
    }

    private static void OnKey(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not HomeRowViewModel vm) return;
        switch (e.Key)
        {
            case Key.Enter:
                if (vm.CommitRenameCommand.CanExecute(null)) vm.CommitRenameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (vm.CancelRenameCommand.CanExecute(null)) vm.CancelRenameCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: HomeRowViewModel { IsRenaming: true } vm }
            && vm.CommitRenameCommand.CanExecute(null))
        {
            vm.CommitRenameCommand.Execute(null);
        }
    }
}
