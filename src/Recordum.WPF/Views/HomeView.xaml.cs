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

    // ＋Nuova▾: the button opens its ContextMenu as a dropdown (same pattern as the ribbon
    // Backup menu). The menu doesn't inherit DataContext, so pass it explicitly.
    private void OnNewMenuClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu!.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;
        // Skip clicks on interactive controls (rename TextBox, context-menu triggers, etc.).
        if (e.OriginalSource is TextBox or ButtonBase or System.Windows.Documents.Run) return;

        var source = e.OriginalSource as DependencyObject;
        var row = FindAncestorDataContext<HomeRowViewModel>(source);
        if (row is not null)
        {
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (ctrl || shift)
                home.SelectInteractive(row, ctrl, shift);
            else
                home.SelectRowCommand.Execute(row);
            return;
        }

        // Clicking empty space inside the tree region deselects, collapsing the workspace/editor
        // columns. Scoped to the tree so clicks on the top bar / filter row don't deselect.
        if (home.SelectedRow is not null && IsDescendantOf(source, TreeRegion))
            home.SelectRowCommand.Execute(null);
    }

    // Right-click on a row OUTSIDE the selection collapses the selection to that row, like
    // file managers, so the context menu's bulk routing matches what the user sees.
    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;
        var row = FindAncestorDataContext<HomeRowViewModel>(e.OriginalSource as DependencyObject);
        if (row is { IsSelected: false })
            home.SelectRowCommand.Execute(row);
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject ancestor)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, ancestor)) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
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
