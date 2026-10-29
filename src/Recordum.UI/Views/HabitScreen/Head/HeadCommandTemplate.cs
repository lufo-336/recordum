using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
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
        // Il caso si assegna prima del quick-add nuovo: finché i due non combaciano non si costruisce niente (la testa
        // tornerebbe a costruirsi sopra il quick-add dell'abitudine di prima, coi binding compilati in errore).
        (Control? view, object? context) = vm.Case switch
        {
            HabitCase.Action when quickAdd is ActionQuickAddViewModel => (new ActionCommandView(), quickAdd),
            HabitCase.CounterTap when quickAdd is CounterTapQuickAddViewModel => (new CounterTapCommandView(), quickAdd),
            HabitCase.State when quickAdd is MeasuredQuickAddViewModel => (new StateCommandView(), quickAdd),
            HabitCase.Abstinence when quickAdd is AbstinenceQuickAddViewModel => (new AbstinenceCommandView(), quickAdd),
            HabitCase.Measured or HabitCase.Series when quickAdd is MeasuredQuickAddViewModel or CounterSeriesQuickAddViewModel
                => (QuickAddTemplate.Instance.Build(quickAdd), quickAdd),
            HabitCase.Session => (new SessionCommandView(Manual(quickAdd)), vm.TimerControl),
            HabitCase.Journal when vm.TodayStrip.Content is JournalTodayContent journal => (new JournalCommandView(), journal),
            _ => ((Control?)null, (object?)null),
        };
        if (view is not null) view.DataContext = context;
        return view;
    }

    // Senza campi il quick-add è a tocco (+1 e annulla): QuickAddTemplate non lo costruisce, il comando in testa sì.
    private static Control? Manual(object? quickAdd)
    {
        var view = quickAdd is CounterTapQuickAddViewModel ? new CounterTapCommandView() : QuickAddTemplate.Instance.Build(quickAdd);
        if (view is not null) view.DataContext = quickAdd;
        return view;
    }
}
