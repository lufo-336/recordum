using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.Home;
using Recordum.UI.Controls;

namespace Recordum.UI.Views.Home;

public partial class DayHeaderView : UserControl
{
    public static readonly StyledProperty<ImmutableArray<DayHeaderCell>> CellsProperty =
        AvaloniaProperty.Register<DayHeaderView, ImmutableArray<DayHeaderCell>>(nameof(Cells));

    public static readonly StyledProperty<HomeRowsMetrics> MetricsProperty =
        AvaloniaProperty.Register<DayHeaderView, HomeRowsMetrics>(nameof(Metrics), HomeRowsMetrics.For(HomeRows.Comfortable));

    /// <summary>Il comando di un giorno (parametro: quanti giorni fa). Null dove l'intestazione è solo da guardare
    /// (Impostazioni › Aspetto).</summary>
    public static readonly StyledProperty<System.Windows.Input.ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<DayHeaderView, System.Windows.Input.ICommand?>(nameof(SelectCommand));

    public System.Windows.Input.ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public ImmutableArray<DayHeaderCell> Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public HomeRowsMetrics Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    static DayHeaderView()
    {
        CellsProperty.Changed.AddClassHandler<DayHeaderView>((view, _) => view.Refresh());
        HeatmapStrip.VisibleDaysProperty.Changed.AddClassHandler<DayHeaderView>((view, _) => view.Refresh());
        HomeRowsMetrics.RowsProperty.Changed.AddClassHandler<DayHeaderView>((view, _) =>
        {
            view.Metrics = HomeRowsMetrics.Of(view);
            view.Classes.Set("narrow", view.Metrics.Marks);
            view.Refresh();
        });
    }

    public DayHeaderView()
    {
        InitializeComponent();
    }

    private void Refresh()
    {
        if (Cells.IsDefaultOrEmpty)
        {
            Days.ItemsSource = Array.Empty<DayHeaderCell>();
            return;
        }
        var visible = Cells.TakeLast(Math.Max(1, HeatmapStrip.GetVisibleDays(this)));
        Days.ItemsSource = Metrics.HeaderInitials
            ? visible.Select(cell => cell with { ShortName = cell.Initial }).ToArray()
            : visible.ToArray();
    }
}
