using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Recordum.UI.Views.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.Head;

/// <summary>Il comando in testa per il caso dell'abitudine (spec direzione visiva §6.2). Costruisce solo la view del caso
/// (niente ContentControl con un template statico: CLAUDE.md §8). Null per un gruppo o prima del caricamento.</summary>
public static class HeadCommandTemplate
{
    public static Control? Build(HabitScreenViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        var quickAdd = vm.QuickAdd;
        (Control? view, object? context) = vm.Case switch
        {
            HabitCase.Action => (new ActionCommandView(), quickAdd),
            HabitCase.CounterTap => (new CounterTapCommandView(), quickAdd),
            HabitCase.State => (new StateCommandView(), quickAdd),
            HabitCase.Abstinence => (new AbstinenceCommandView(), quickAdd),
            HabitCase.Measured or HabitCase.Series => (QuickAddTemplate.Instance.Build(quickAdd), quickAdd),
            HabitCase.Session => (new SessionCommandView(Manual(quickAdd)), vm.TimerControl),
            HabitCase.Journal when vm.TodayStrip.Content is JournalTodayContent journal => (new JournalCommandView(), journal),
            _ => ((Control?)null, (object?)null),
        };
        if (view is not null) view.DataContext = context;
        return view;
    }

    private static Control? Manual(object? quickAdd)
    {
        var view = QuickAddTemplate.Instance.Build(quickAdd);
        if (view is not null) view.DataContext = quickAdd;
        return view;
    }
}
