using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
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

    public static readonly StyledProperty<bool> SelectableProperty =
        AvaloniaProperty.Register<HistoryMapView, bool>(nameof(Selectable), true);

    /// <summary>False per la mappa di un gruppo: i giorni non sono scelte (niente mano, niente fuoco).</summary>
    public bool Selectable { get => GetValue(SelectableProperty); set => SetValue(SelectableProperty, value); }

    public HistoryMapView() => InitializeComponent();

    /// <summary>I giorni brevi della cultura, dal lunedì.</summary>
    public static IReadOnlyList<string> WeekdayNames =>
        Enumerable.Range(1, 7).Select(i => CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[i % 7]).ToList();

    public static readonly IValueConverter IsDaysShape = Shape(s => s is MapShape.Week or MapShape.Month);
    public static readonly IValueConverter IsWeekShape = Shape(s => s is MapShape.Week);
    public static readonly IValueConverter IsHeatmapShape = Shape(s => s is MapShape.Heatmap);
    public static readonly IValueConverter IsYearsShape = Shape(s => s is MapShape.YearHeatmaps);

    private static FuncValueConverter<MapShape, bool> Shape(Func<MapShape, bool> test) => new(test);
}
