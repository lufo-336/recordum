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
    public ThemeProbeView() => AvaloniaXamlLoader.Load(this);
}
