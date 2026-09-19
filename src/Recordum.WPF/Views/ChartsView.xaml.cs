using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Recordum.Presentation.ViewModels.Charts;

namespace Recordum.WPF.Views;

public partial class ChartsView : UserControl
{
    public ChartsView() => InitializeComponent();

    private async void OnChartSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ChartsWorkspaceViewModel vm && sender is ListBox lb)
            await vm.SelectAsync(lb.SelectedItem as ChartListItemViewModel);
    }

    // A series row's Label auto-fills from the habit/metric selection (ChartSeriesRowViewModel.AutoLabel)
    // until the user edits it by hand (MarkLabelTouched). TextChanged alone isn't enough to detect that:
    // it also fires for programmatic Text pushes (AutoLabel/LoadAsync restoring a persisted label), which
    // would mark the row touched the instant auto-label first runs and permanently defeat it. GotKeyboardFocus
    // is unambiguous — WPF never moves focus as a side effect of a binding update — so it always marks the
    // row touched the moment the user tabs/clicks into the box; TextChanged is gated on IsKeyboardFocused as
    // defense in depth for the same reason.
    private void OnSeriesLabelGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChartSeriesRowViewModel row)
            row.MarkLabelTouched();
    }

    private void OnSeriesLabelTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox { IsKeyboardFocused: true } tb && tb.DataContext is ChartSeriesRowViewModel row)
            row.MarkLabelTouched();
    }
}
