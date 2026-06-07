using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

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
}
