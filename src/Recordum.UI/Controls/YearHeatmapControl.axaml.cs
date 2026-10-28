using Avalonia;
using Avalonia.Controls;

namespace Recordum.UI.Controls;

public partial class YearHeatmapControl : UserControl
{
    /// <summary>Il colore dell'abitudine (hex): i gradini della heatmap sono questo colore sul pannello (§4.3).</summary>
    public static readonly StyledProperty<string?> HabitColorProperty =
        AvaloniaProperty.Register<YearHeatmapControl, string?>(nameof(HabitColor));

    public string? HabitColor
    {
        get => GetValue(HabitColorProperty);
        set => SetValue(HabitColorProperty, value);
    }

    // InitializeComponent(), MAI AvaloniaXamlLoader.Load(this): il secondo carica lo XAML ma non
    // cabla i campi x:Name, e il guasto è un NullReferenceException a runtime senza nessun errore
    // di compilazione.
    public YearHeatmapControl() => InitializeComponent();
}
