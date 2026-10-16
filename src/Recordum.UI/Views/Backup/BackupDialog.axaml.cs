using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.UI.Views.Backup;

/// <summary>Gemella di <c>Recordum.WPF.Views.Backup.BackupDialog</c>: si chiude da sola a backup riuscito.</summary>
public partial class BackupDialog : RecordumWindow
{
    public BackupDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is BackupDialogViewModel vm) vm.PropertyChanged += OnVmChanged;
        };
    }

    // Chiudere mentre l'operazione gira lascerebbe il lanciatore senza risultato (e, per il restore, l'app su un
    // database sostituito): finche' IsRunning la finestra non si chiude.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is BackupDialogViewModel { IsRunning: true }) e.Cancel = true;
        base.OnClosing(e);
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BackupDialogViewModel.SuccessPath) or nameof(BackupDialogViewModel.IsRunning)
            && sender is BackupDialogViewModel { SuccessPath: not null, IsRunning: false })
            Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
