using System.ComponentModel;
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

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RestoreDialogViewModel.IsCompleted) && ((RestoreDialogViewModel)sender!).IsCompleted)
            Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
