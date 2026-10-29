using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Recordum.Presentation.ViewModels.HabitScreen.History;

namespace Recordum.UI.Views.HabitScreen.History;

public partial class HistoryMapView : UserControl
{
    public static readonly StyledProperty<string?> HabitColorProperty =
        AvaloniaProperty.Register<HistoryMapView, string?>(nameof(HabitColor));

    public static readonly StyledProperty<string?> NoteColorProperty =
        AvaloniaProperty.Register<HistoryMapView, string?>(nameof(NoteColor));

    /// <summary>Il colore dell'abitudine (hex) per i gradini.</summary>
    public string? HabitColor { get => GetValue(HabitColorProperty); set => SetValue(HabitColorProperty, value); }

    /// <summary>Il colore del segno delle note (<c>NoteMarkerColor</c>).</summary>
    public string? NoteColor { get => GetValue(NoteColorProperty); set => SetValue(NoteColorProperty, value); }

    public HistoryMapView()
    {
        InitializeComponent();
        var cell = (IDataTemplate)Resources["HeatCell"]!;
        WeekColumn = new FuncDataTemplate<IReadOnlyList<HistoryDayCell>>((week, _) => new ItemsControl
        {
            ItemsSource = week,
            ItemTemplate = cell,
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Vertical }),
        }, supportsRecycling: false);
        // Qui, non con un binding: WeekColumn nasce dopo InitializeComponent e senza notifica un binding lo leggeva null
        // (l'anno mostrava "HistoryDayCell[]" al posto delle celle; visto nei PNG). Quello di "Tutto" sta in un template e
        // si valuta dopo.
        Weeks.ItemTemplate = WeekColumn;
    }

    /// <summary>I giorni brevi della cultura, dal lunedì.</summary>
    public static IReadOnlyList<string> WeekdayNames =>
        Enumerable.Range(1, 7).Select(i => CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[i % 7]).ToList();

    public static readonly IValueConverter IsDaysShape = Shape(s => s is MapShape.Week or MapShape.Month);
    public static readonly IValueConverter IsWeekShape = Shape(s => s is MapShape.Week);
    public static readonly IValueConverter IsHeatmapShape = Shape(s => s is MapShape.Heatmap);
    public static readonly IValueConverter IsYearsShape = Shape(s => s is MapShape.YearHeatmaps);

    /// <summary>Nella heatmap un giorno fuori dall'intervallo completa la colonna ma non si vede.</summary>
    public static readonly IValueConverter InRangeOpacity = new FuncValueConverter<bool, double>(inRange => inRange ? 1 : 0);

    private static FuncValueConverter<MapShape, bool> Shape(Func<MapShape, bool> test) => new(test);

    /// <summary>Una colonna di settimana della heatmap: sette celle in verticale. Si scrive in codice perché l'elemento è un
    /// <c>IReadOnlyList&lt;HistoryDayCell&gt;</c>, che un <c>x:DataType</c> non sa nominare; la cella è "HeatCell" delle
    /// risorse.</summary>
    public IDataTemplate WeekColumn { get; }
}
