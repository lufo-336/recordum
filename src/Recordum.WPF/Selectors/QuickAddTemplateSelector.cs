using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.WPF.Selectors;

public sealed class QuickAddTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Action { get; set; }
    public DataTemplate? Measured { get; set; }
    public DataTemplate? CounterSeries { get; set; }
    public DataTemplate? Journal { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
    {
        ActionQuickAddViewModel => Action,
        MeasuredQuickAddViewModel => Measured,
        CounterSeriesQuickAddViewModel => CounterSeries,
        JournalQuickAddViewModel => Journal,
        _ => null,
    };
}
