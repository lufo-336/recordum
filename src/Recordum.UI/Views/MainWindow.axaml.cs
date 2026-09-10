using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Recordum.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        // InitializeComponent(), NON AvaloniaXamlLoader.Load(this): il generatore Avalonia produce
        // entrambe le cose, ma solo InitializeComponent() carica lo XAML *e* cabla i campi x:Name.
        // Con Load(this) i campi restano null e il crash arriva a runtime, muto (trovato nella sonda
        // dei controlli, 2026-09-09). Questa è la forma da copiare nel porting delle 60 view.
        InitializeComponent();
    }
}
