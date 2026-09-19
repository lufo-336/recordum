using System.Windows;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.WPF.Views.Backup;

public partial class BackupDialog : Window
{
    private readonly BackupDialogViewModel _vm;

    public BackupDialog(BackupDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BackupDialogViewModel.SuccessPath) && _vm.SuccessPath != null)
                DialogResult = true;
        };
    }

    private void OnPassChanged(object sender, RoutedEventArgs e) => _vm.Passphrase = PassBox.Password;
    private void OnConfirmChanged(object sender, RoutedEventArgs e) => _vm.ConfirmPassphrase = ConfirmBox.Password;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
