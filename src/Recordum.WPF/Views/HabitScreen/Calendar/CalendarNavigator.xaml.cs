using System.Windows.Controls;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;

namespace Recordum.WPF.Views.HabitScreen.Calendar;

public partial class CalendarNavigator : UserControl
{
    public CalendarNavigator() => InitializeComponent();

    private void OnJumpChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CalendarNavigatorViewModel vm)
            _ = vm.JumpAsync();
    }
}
