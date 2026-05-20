using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Recordum.WPF.ViewModels;

namespace Recordum.WPF.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
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
