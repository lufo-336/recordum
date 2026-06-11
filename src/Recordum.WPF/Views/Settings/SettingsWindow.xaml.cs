using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.Settings;

namespace Recordum.WPF.Views.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // PasswordBox doesn't support binding; the boxes live inside the Backup category's
    // DataTemplate, so we reach the row VM through the sender's DataContext.
    private void OnBackupPassphraseChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box && box.DataContext is BackupSettingsViewModel vm)
            vm.NewPassphrase = box.Password;
    }

    private void OnBackupConfirmChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box && box.DataContext is BackupSettingsViewModel vm)
            vm.ConfirmPassphrase = box.Password;
    }
}
