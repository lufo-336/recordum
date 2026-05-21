using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.WPF.Selectors;

public sealed class QuickAddTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Action { get; set; }
    public DataTemplate? Counter { get; set; }
    public DataTemplate? Resistance { get; set; }
    public DataTemplate? Journal { get; set; }
    public DataTemplate? State { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
    {
        ActionQuickAddViewModel => Action,
        CounterQuickAddViewModel => Counter,
        ResistanceQuickAddViewModel => Resistance,
        JournalQuickAddViewModel => Journal,
        StateQuickAddViewModel => State,
        _ => null,
    };
}
