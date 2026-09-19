using System.Windows;
using System.Windows.Controls;

namespace Recordum.WPF.Views;

public partial class WorkspaceRibbon : UserControl
{
    public WorkspaceRibbon()
    {
        InitializeComponent();
    }

    // Clicking empty rail space deselects (spec §B.3): with a full tree there is no empty
    // Home space left to click. Button clicks never reach here (ButtonBase handles the event),
    // but walk the tree defensively anyway.
    private void OnRibbonEmptyClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (DataContext is Recordum.Presentation.ViewModels.MainWindowViewModel { Home.SelectedRow: not null } vm)
            vm.Home.SelectedRow = null;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    // Left-click opens the backup menu too (a bare ContextMenu only opens on right-click).
    private void OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (BackupButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = BackupButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
        menu.DataContext = DataContext; // the MenuItems bind MainWindowViewModel commands
        menu.IsOpen = true;
    }
}
