using Avalonia.Controls;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Services;

/// <summary>Apre la scheda di un'abitudine nella finestra principale partendo da una voce delle impostazioni.</summary>
public sealed class AvaloniaHabitNavigator(Func<Window?> mainWindowResolver) : IHabitNavigator
{
    public Task OpenHabitAtDayAsync(Guid habitNodeId, string title, DateOnly day)
    {
        if (mainWindowResolver()?.DataContext is MainWindowViewModel vm)
            return vm.OpenHabitAtDayAsync(habitNodeId, title, day);
        return Task.CompletedTask;
    }
}
