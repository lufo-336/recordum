using Avalonia.Controls;
using Recordum.Presentation.ViewModels.Startup;

namespace Recordum.UI.Views.Startup;

public partial class UnlockWindow : Window
{
    public UnlockWindow() : this(null!) { }

    public UnlockWindow(UnlockViewModel viewModel)
    {
        // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs.
        InitializeComponent();
        if (viewModel is null) return;
        DataContext = viewModel;
        // Il titolo del riquadro viene da UnlockViewModel.Heading (binding nello XAML): il VM non
        // espone lo stato, e non serve — Heading e' gia' la decisione (errata 2026-09-25).
        Opened += (_, _) => PassphraseBox.Focus();
    }
}
