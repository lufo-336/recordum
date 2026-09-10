using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Recordum.Avalonia.Views;

/// <summary>
/// Sonda di styling (branch avalonia-linux-slice): esercita i controlli e le composizioni che
/// Recordum mette a schermo, per misurare quanto di BaseStyles.xaml (WPF) transfer su
/// Avalonia/FluentTheme. NON è una vista di produzione: nessun'altra parte dell'app la referenzia.
/// </summary>
public partial class ThemeProbeView : UserControl
{
    public ThemeProbeView()
    {
        // InitializeComponent(), NON AvaloniaXamlLoader.Load(this): il generatore Avalonia produce
        // entrambe le cose, ma solo InitializeComponent() carica lo XAML *e* cabla i campi x:Name.
        // Con Load(this) i campi restano null e il crash arriva a runtime, muto (trovato nella sonda
        // dei controlli, 2026-09-09). Questa è la forma da copiare nel porting delle 60 view.
        InitializeComponent();
    }
}
