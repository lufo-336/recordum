using System.Windows;
using Recordum.WPF.ViewModels.Backup;

namespace Recordum.WPF.Views.Backup;

public partial class BackupSettingsDialog : Window
{
    private readonly BackupSettingsViewModel _vm;
    public BackupSettingsDialog(BackupSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
    }
    private void OnPassChanged(object sender, RoutedEventArgs e) => _vm.NewPassphrase = PassBox.Password;
    private void OnConfirmChanged(object sender, RoutedEventArgs e) => _vm.ConfirmPassphrase = ConfirmBox.Password;
}
