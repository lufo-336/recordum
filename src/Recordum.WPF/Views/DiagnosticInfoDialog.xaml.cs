using System.Windows;
using Recordum.WPF.ViewModels.DiagnosticInfo;

namespace Recordum.WPF.Views;

public partial class DiagnosticInfoDialog : Window
{
    public DiagnosticInfoDialog(DiagnosticInfoViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        InitializeComponent();
        DataContext = vm;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
