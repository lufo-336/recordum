using System.Windows.Controls;
using Recordum.WPF.ViewModels.Charts;

namespace Recordum.WPF.Views;

public partial class ChartsView : UserControl
{
    public ChartsView() => InitializeComponent();

    private async void OnChartSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ChartsWorkspaceViewModel vm && sender is ListBox lb)
            await vm.SelectAsync(lb.SelectedItem as ChartListItemViewModel);
    }
}
