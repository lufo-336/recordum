using Avalonia.Controls;

namespace Recordum.UI.Views.Startup;

public partial class UnlockView : UserControl
{
    public UnlockView() => InitializeComponent();

    public void FocusPassphrase() => PassphraseBox.Focus();
}
