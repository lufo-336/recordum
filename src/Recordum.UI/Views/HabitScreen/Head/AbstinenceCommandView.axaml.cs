using Avalonia.Controls;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.Head;

public partial class AbstinenceCommandView : UserControl
{
    public AbstinenceCommandView() => InitializeComponent();

    // Il contenuto di un Flyout non eredita il DataContext in modo affidabile: la conferma passa di qui.
    private async void OnConfirmLapse(object? sender, RoutedEventArgs e)
    {
        try
        {
            LapseButton.Flyout?.Hide();
            if (DataContext is AbstinenceQuickAddViewModel vm) await vm.LapseCommand.ExecuteAsync(null);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OnConfirmLapse failed: {ex}"); }
    }
}
