using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Recordum.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
