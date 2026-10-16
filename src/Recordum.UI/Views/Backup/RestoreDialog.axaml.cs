using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.UI.Views.Backup;

/// <summary>Gemella di <c>Recordum.WPF.Views.Backup.RestoreDialog</c>: si chiude da sola a restore riuscito.</summary>
public partial class RestoreDialog : RecordumWindow
{
    public RestoreDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is RestoreDialogViewModel vm) vm.PropertyChanged += OnVmChanged;
        };
    }

    // Chiudere mentre l'operazione gira lascerebbe il lanciatore senza risultato e l'app su un database sostituito.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is RestoreDialogViewModel { IsRunning: true }) e.Cancel = true;
        base.OnClosing(e);
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsCompleted sale dentro il try, IsRunning scende nel finally: si chiude quando entrambe sono a posto.
        if (e.PropertyName is nameof(RestoreDialogViewModel.IsCompleted) or nameof(RestoreDialogViewModel.IsRunning)
            && sender is RestoreDialogViewModel { IsCompleted: true, IsRunning: false })
            Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
