using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels.HabitScreen.Today;

namespace Recordum.UI.Views.HabitScreen.Today;

/// <summary>Sceglie la view della fascia Oggi dal tipo del contenuto e costruisce SOLO quella. Per il
/// contenuto sconosciuto torna null (la fascia si collassa).</summary>
public sealed class TodayContentTemplate : IDataTemplate
{
    public static TodayContentTemplate Instance { get; } = new();

    public bool Match(object? data) => data is ITodayContent;

    public Control? Build(object? param) => param switch
    {
        ActionTodayContent => new ActionTodayView(),
        CounterTodayContent => new CounterTodayView(),
        ResistanceTodayContent => new ResistanceTodayView(),
        StateTodayContent => new StateTodayView(),
        JournalTodayContent => new JournalTodayView(),
        _ => null,
    };
}
