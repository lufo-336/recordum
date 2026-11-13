using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.Home;

namespace Recordum.UI.Controls;

/// <summary>Le misure delle tre versioni delle righe della Home, ereditate lungo l'albero visuale
/// (la Home le imposta su di sé, righe, strisce e intestazione le leggono).</summary>
public sealed record HomeRowsMetrics(double RowHeight, double Command, double Column, double Gap, bool HeaderInitials, bool Marks)
{
    public static readonly AttachedProperty<HomeRows> RowsProperty =
        AvaloniaProperty.RegisterAttached<HomeRowsMetrics, Control, HomeRows>("Rows", HomeRows.Comfortable, inherits: true);

    private static readonly HomeRowsMetrics ComfortableMetrics = new(36, 28, 26, 0, HeaderInitials: false, Marks: false);
    private static readonly HomeRowsMetrics CompactMetrics = new(30, 24, 26, 0, HeaderInitials: true, Marks: false);
    private static readonly HomeRowsMetrics MarksMetrics = new(30, 24, 14, 3, HeaderInitials: true, Marks: true);

    public static HomeRows GetRows(Control element) => element.GetValue(RowsProperty);

    public static void SetRows(Control element, HomeRows value) => element.SetValue(RowsProperty, value);

    public static HomeRowsMetrics For(HomeRows rows) => rows switch
    {
        HomeRows.Compact => CompactMetrics,
        HomeRows.Marks => MarksMetrics,
        _ => ComfortableMetrics,
    };

    public static HomeRowsMetrics Of(Control element) => For(GetRows(element));

    public static int VisibleDays(HomeRows rows, double width)
    {
        var days = width >= 640 ? 14 : width >= 400 ? 7 : width >= 340 ? 5 : 4;
        return rows == HomeRows.Marks ? Math.Max(7, days) : days;
    }
}
