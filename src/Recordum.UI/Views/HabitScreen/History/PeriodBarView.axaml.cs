using Avalonia.Controls;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.HabitScreen.History;

namespace Recordum.UI.Views.HabitScreen.History;

public partial class PeriodBarView : UserControl
{
    public PeriodBarView()
    {
        InitializeComponent();
        ((Flyout)CustomButton.Flyout!).Opening += (_, _) =>
        {
            if (DataContext is not HistoryPeriodViewModel vm) return;
            CustomFrom.SelectedDate = vm.From.ToDateTime(TimeOnly.MinValue);
            CustomTo.SelectedDate = vm.To.ToDateTime(TimeOnly.MinValue);
        };
    }

    // "Dal… al…": l'ordine lo rimette SetCustom (un inizio dopo la fine si scambia).
    private void OnApplyCustom(object? sender, RoutedEventArgs e)
    {
        CustomButton.Flyout?.Hide();
        if (DataContext is HistoryPeriodViewModel vm && CustomFrom.SelectedDate is { } a && CustomTo.SelectedDate is { } b)
            vm.SetCustom(DateOnly.FromDateTime(a), DateOnly.FromDateTime(b));
    }
}
