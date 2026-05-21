using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Recordum.WPF.ViewModels;

namespace Recordum.WPF.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;
        // Skip clicks on interactive controls (rename TextBox, context-menu triggers, etc.).
        if (e.OriginalSource is TextBox or ButtonBase or System.Windows.Documents.Run) return;

        var row = FindAncestorDataContext<HomeRowViewModel>(e.OriginalSource as DependencyObject);
        if (row is null) return;
        home.SelectRowCommand.Execute(row);
    }

    private static T? FindAncestorDataContext<T>(DependencyObject? source) where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement fe && fe.DataContext is T match) return match;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is FrameworkElement fe
            && fe.DataContext is HomeRowViewModel vm)
        {
            DispatchRowKey(vm, e);
        }
    }

    private static void DispatchRowKey(HomeRowViewModel vm, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                if (vm.BeginRenameCommand.CanExecute(null))
                    vm.BeginRenameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete:
                if (vm.SoftDeleteCommand.CanExecute(null))
                    vm.SoftDeleteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.E when Keyboard.Modifiers == ModifierKeys.Control:
                var toggleArchive = vm.IsArchived ? vm.UnarchiveCommand : vm.ArchiveCommand;
                if (toggleArchive.CanExecute(null))
                    toggleArchive.Execute(null);
                e.Handled = true;
                break;
            case Key.R when Keyboard.Modifiers == ModifierKeys.Control:
                if (vm.RestoreCommand.CanExecute(null))
                    vm.RestoreCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
