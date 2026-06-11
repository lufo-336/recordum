using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Navigation;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.Views.HabitScreen;

public partial class HabitScreen : UserControl
{
    public HabitScreen() => InitializeComponent();

    // Opens the WebView2 Runtime download page in the user's default browser.
    private void OnWebView2DownloadNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // ---- column resize (drag-thumb on each column's right edge) ----

    private HabitScreenViewModel? Vm => DataContext as HabitScreenViewModel;

    private static HabitColumn ColumnOf(object sender) => (HabitColumn)((Thumb)sender).Tag;

    private void OnColumnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (Vm is not { } vm) return;
        var column = ColumnOf(sender);
        vm.SetColumnWidth(column, vm.GetColumnWidth(column) + e.HorizontalChange);
    }

    private void OnColumnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (Vm is { } vm) _ = vm.PersistColumnWidthsAsync();
    }

    private void OnColumnResizeReset(object sender, MouseButtonEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.ResetColumnWidth(ColumnOf(sender));
        _ = vm.PersistColumnWidthsAsync();
        e.Handled = true;
    }
}
