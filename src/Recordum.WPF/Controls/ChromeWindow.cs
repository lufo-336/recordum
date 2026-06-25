using System.Windows;
using System.Windows.Input;

namespace Recordum.WPF.Controls;

/// <summary>Attached behavior that wires the caption buttons of the shared
/// <c>Recordum.ChromedWindow</c> template to the standard window system commands
/// (minimize / maximize / restore / close). Set <c>ChromeWindow.Active="True"</c>
/// from the style so any child window gets the dark custom chrome without
/// duplicating MainWindow's per-window Click handlers.</summary>
public static class ChromeWindow
{
    public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached(
        "Active", typeof(bool), typeof(ChromeWindow), new PropertyMetadata(false, OnActiveChanged));

    public static bool GetActive(DependencyObject o) => (bool)o.GetValue(ActiveProperty);
    public static void SetActive(DependencyObject o, bool value) => o.SetValue(ActiveProperty, value);

    private static void OnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w || e.NewValue is not true) return;
        w.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(w)));
        w.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(w)));
        w.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(w)));
        w.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(w)));
    }
}
