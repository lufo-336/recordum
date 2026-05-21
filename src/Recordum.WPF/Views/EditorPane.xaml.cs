using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels;

namespace Recordum.WPF.Views;

public partial class EditorPane : UserControl
{
    public EditorPane()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // The pane's DataContext is the HabitEditorViewModel; the SelectRow command lives on the
        // HomeViewModel sibling. Walk up to the parent Window's DataContext (MainWindowViewModel).
        var window = Window.GetWindow(this);
        if (window?.DataContext is not MainWindowViewModel mainVm) return;
        mainVm.Home.SelectRowCommand.Execute(null);
    }
}
