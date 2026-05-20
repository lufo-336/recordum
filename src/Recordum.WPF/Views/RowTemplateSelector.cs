using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels;

namespace Recordum.WPF.Views;

public sealed class RowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HabitTemplate { get; set; }
    public DataTemplate? GroupTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is HomeRowViewModel { IsGroup: true } ? GroupTemplate : HabitTemplate;
}
