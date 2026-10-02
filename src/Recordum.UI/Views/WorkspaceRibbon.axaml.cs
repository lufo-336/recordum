using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Views;

public partial class WorkspaceRibbon : UserControl
{
    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs.
    public WorkspaceRibbon()
    {
        InitializeComponent();
        Rail.PointerReleased += OnRailPointerReleased;
    }

    // Clic sul vuoto del rail = deseleziona (spec §B.3): con un albero pieno non resta spazio
    // vuoto da cliccare nella Home. I clic sui pulsanti non arrivano qui (Button li marca
    // Handled), ma si risale comunque l'albero per difesa, come faceva WPF.
    private void OnRailPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (IsInsideButton(e.Source as Visual)) return;
        if (DataContext is MainWindowViewModel { Home.SelectedRow: not null } vm)
            vm.Home.SelectedRow = null;
    }

    // Avalonia 12 NON espone ButtonBase (Button deriva direttamente da ContentControl): il
    // controllo si fa su Button. Verificato per reflection su Avalonia.Controls 12.1.2.
    private static bool IsInsideButton(Visual? source)
    {
        while (source is not null)
        {
            if (source is Button) return true;
            source = source.GetVisualParent();
        }
        return false;
    }
}
