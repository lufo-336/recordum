using Avalonia.Controls;

namespace Recordum.UI.Views.Startup;

public partial class WaitingView : UserControl
{
    public WaitingView() => InitializeComponent();

    public string Status
    {
        get => StatusText.Text ?? string.Empty;
        set => StatusText.Text = value;
    }
}
