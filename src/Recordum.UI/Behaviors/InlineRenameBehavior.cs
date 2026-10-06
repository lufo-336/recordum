using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Windows.Input;
using Avalonia.Threading;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Behaviors;

public static class InlineRenameBehavior
{
    public static readonly AttachedProperty<bool> AttachProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Attach", typeof(InlineRenameBehavior));

    public static bool GetAttach(TextBox element) => element.GetValue(AttachProperty);
    public static void SetAttach(TextBox element, bool value) => element.SetValue(AttachProperty, value);

    // Per chi non e' una riga Home (i chip delle serie): i comandi stanno sul genitore e prendono
    // come parametro il DataContext del TextBox.
    public static readonly AttachedProperty<ICommand?> CommitCommandProperty =
        AvaloniaProperty.RegisterAttached<TextBox, ICommand?>("CommitCommand", typeof(InlineRenameBehavior));
    public static readonly AttachedProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.RegisterAttached<TextBox, ICommand?>("CancelCommand", typeof(InlineRenameBehavior));

    public static ICommand? GetCommitCommand(TextBox element) => element.GetValue(CommitCommandProperty);
    public static void SetCommitCommand(TextBox element, ICommand? value) => element.SetValue(CommitCommandProperty, value);
    public static ICommand? GetCancelCommand(TextBox element) => element.GetValue(CancelCommandProperty);
    public static void SetCancelCommand(TextBox element, ICommand? value) => element.SetValue(CancelCommandProperty, value);

    private static void Run(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true) command.Execute(parameter);
    }

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
        if (sender is not TextBox tb) return;
        var (commit, cancel, parameter) = Commands(tb);
        if (commit is null) return;
        switch (e.Key)
        {
            case Key.Enter:
                Run(commit, parameter);
                e.Handled = true;
                break;
            case Key.Escape:
                Run(cancel, parameter);
                e.Handled = true;
                break;
        }
    }

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        // Esc spegne il flag e poi il TextBox perde il fuoco: solo chi sta ancora rinominando salva.
        var renaming = tb.DataContext is HomeRowViewModel { IsRenaming: true } or SeriesChipViewModel { IsEditing: true };
        if (!renaming) return;
        var (commit, _, parameter) = Commands(tb);
        Run(commit, parameter);
    }

    private static (ICommand? Commit, ICommand? Cancel, object? Parameter) Commands(TextBox tb) => tb.DataContext switch
    {
        HomeRowViewModel vm => (vm.CommitRenameCommand, vm.CancelRenameCommand, null),
        _ => (GetCommitCommand(tb), GetCancelCommand(tb), tb.DataContext),
    };
}
