using System.ComponentModel;
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

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BackupDialogViewModel.SuccessPath) && ((BackupDialogViewModel)sender!).SuccessPath is not null)
            Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
