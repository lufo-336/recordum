using Avalonia.Controls;

namespace Recordum.UI.Views.Startup;

/// <summary>Avalonia non ha un MessageBox: il dialogo fatale e' una finestra piccola. Non prende un
/// ViewModel — arriva quando il contenitore potrebbe non essersi costruito.</summary>
public partial class FatalErrorWindow : Window
{
    public FatalErrorWindow() : this(string.Empty, null) { }

    public FatalErrorWindow(string message, string? logPath)
    {
        // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs.
        InitializeComponent();
        MessageText.Text = message;
        LogPathText.Text = logPath ?? string.Empty;
        LogPathText.IsVisible = !string.IsNullOrEmpty(logPath);
        CloseButton.Click += (_, _) => Close();
    }
}
