using System.Windows;

namespace Recordum.WPF.Behaviors;

public static class TreeDragDropBehavior
{
    // Full drag-and-drop wiring lands in Milestone 3.3 Task 29. This attached
    // property exists so the HomeView.xaml host can declare its intent now.
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(TreeDragDropBehavior),
            new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);
}
