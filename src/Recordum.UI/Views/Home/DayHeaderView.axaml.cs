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

    public ImmutableArray<DayHeaderCell> Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    static DayHeaderView()
    {
        CellsProperty.Changed.AddClassHandler<DayHeaderView>((view, _) => view.Refresh());
        HeatmapStrip.VisibleDaysProperty.Changed.AddClassHandler<DayHeaderView>((view, _) => view.Refresh());
    }

    public DayHeaderView()
    {
        InitializeComponent();
    }

    private void Refresh()
    {
        Days.ItemsSource = Cells.IsDefaultOrEmpty
            ? []
            : Cells.TakeLast(Math.Max(1, HeatmapStrip.GetVisibleDays(this))).ToArray();
    }
}
