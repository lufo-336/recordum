using System.Windows;
using Recordum.WPF.ViewModels.Export;

namespace Recordum.WPF.Views;

public partial class ExportDialog : Window
{
    public ExportDialog(ExportDialogViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        InitializeComponent();
        DataContext = vm;
        vm.Closed += (_, _) => Close();
    }
}
