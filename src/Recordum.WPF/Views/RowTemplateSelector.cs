using System.Windows;
using System.Windows.Controls;
using Recordum.Presentation.ViewModels;

namespace Recordum.WPF.Views;

/// <summary>Resolves the row template by resource key from the container's scope, so the
/// group template can recursively select itself for nested groups (a self StaticResource
/// inside GroupRowTemplate.xaml would be unresolvable).</summary>
public sealed class RowTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        container is FrameworkElement fe
            ? (DataTemplate)fe.FindResource(item is HomeRowViewModel { IsGroup: true } ? "GroupRowTemplate" : "HabitRowTemplate")
            : null;
}
