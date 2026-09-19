using System.Windows.Controls;
using System.Windows.Input;
using Recordum.Presentation.ViewModels.Workspace;

namespace Recordum.WPF.Views.Workspace;

public partial class WorkspaceView : UserControl
{
    public WorkspaceView()
    {
        InitializeComponent();
    }

    // Single-click activation is native (ListBox selection ↔ ActiveSubjectTab). Double-click pins
    // the active (just-selected) tab so it stops being a transient preview.
    private void OnTabStripDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm)
            vm.PinActiveTab();
    }
}
