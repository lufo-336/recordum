using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Recordum.UI.Views.HabitScreen.Today;

namespace Recordum.UI.Views.HabitScreen.Head;

/// <summary>Il comando in testa per il caso dell'abitudine (spec direzione visiva §6.2). Costruisce solo la view del caso
/// (niente ContentControl con un template statico: CLAUDE.md §8). Null per un gruppo o prima del caricamento.</summary>
public static class HeadCommandTemplate
{
    public static Control? Build(HabitScreenViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        Control? view;
        object? context;
        switch (vm.Case)
        {
            case HabitCase.Session:
                (view, context) = (new TimerControlView(), vm.TimerControl);
                break;
            case HabitCase.Journal:
                context = vm.TodayStrip.Content as JournalTodayContent;
                view = context is null ? null : new JournalTodayView();
                break;
            case null:
                return null;
            default:
                context = vm.QuickAdd;
                view = QuickAddTemplate.Instance.Build(vm.QuickAdd);
                break;
        }
        if (view is not null) view.DataContext = context;
        return view;
    }
}
