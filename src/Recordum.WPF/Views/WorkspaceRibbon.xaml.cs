using System.Windows;
using System.Windows.Controls;

namespace Recordum.WPF.Views;

public partial class WorkspaceRibbon : UserControl
{
    public WorkspaceRibbon()
    {
        InitializeComponent();
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
