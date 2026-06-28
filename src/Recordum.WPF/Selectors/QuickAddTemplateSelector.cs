using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.WPF.Selectors;

public sealed class QuickAddTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Action { get; set; }
    public DataTemplate? Abstinence { get; set; }
    public DataTemplate? Measured { get; set; }
    public DataTemplate? CounterSeries { get; set; }
    public DataTemplate? CounterTap { get; set; }
    public DataTemplate? Journal { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
    {
        ActionQuickAddViewModel => Action,
        AbstinenceQuickAddViewModel => Abstinence,
        MeasuredQuickAddViewModel => Measured,
        CounterSeriesQuickAddViewModel => CounterSeries,
        CounterTapQuickAddViewModel => CounterTap,
        JournalQuickAddViewModel => Journal,
        _ => null,
    };
}
