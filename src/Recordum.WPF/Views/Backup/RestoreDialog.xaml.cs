using System.Windows;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.WPF.Views.Backup;

public partial class RestoreDialog : Window
{
    private readonly RestoreDialogViewModel _vm;
    public RestoreDialog(RestoreDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RestoreDialogViewModel.IsCompleted) && _vm.IsCompleted)
                DialogResult = true;
        };
    }
    private void OnPassChanged(object sender, RoutedEventArgs e) => _vm.Passphrase = PassBox.Password;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
