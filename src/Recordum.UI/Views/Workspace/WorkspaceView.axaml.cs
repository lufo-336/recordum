using Avalonia.Controls;
using Avalonia.Input;
using Recordum.Presentation.ViewModels.Workspace;

namespace Recordum.UI.Views.Workspace;

public partial class WorkspaceView : UserControl
{
    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs.
    public WorkspaceView()
    {
        InitializeComponent();
        TabStrip.DoubleTapped += OnTabStripDoubleTapped;
    }

    // L'attivazione al clic singolo e' nativa (selezione della ListBox <-> ActiveSubjectTab).
    // Il doppio tap fissa il tab attivo (appena selezionato) perche' smetta di essere un'anteprima.
    private void OnTabStripDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm)
            vm.PinActiveTab();
    }
}
