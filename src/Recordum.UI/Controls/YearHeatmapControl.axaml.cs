using Avalonia.Controls;

namespace Recordum.UI.Controls;

public partial class YearHeatmapControl : UserControl
{
    // InitializeComponent(), MAI AvaloniaXamlLoader.Load(this): il secondo carica lo XAML ma non
    // cabla i campi x:Name, e il guasto è un NullReferenceException a runtime senza nessun errore
    // di compilazione.
    public YearHeatmapControl() => InitializeComponent();
}
