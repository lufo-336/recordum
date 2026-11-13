using Avalonia;
using Avalonia.Controls;
using Recordum.UI.Controls;

namespace Recordum.UI.Views.Rows;

public partial class HabitRow : UserControl
{
    static HabitRow()
    {
        HomeRowsMetrics.RowsProperty.Changed.AddClassHandler<HabitRow>((row, _) => row.ApplyRows());
    }

    // InitializeComponent(), mai AvaloniaXamlLoader.Load(this): il secondo non cabla gli x:Name.
    public HabitRow()
    {
        InitializeComponent();
        ApplyRows();
        RowMenu.AttachOnDemand(this, MenuHost);
    }

    private void ApplyRows()
    {
        var m = HomeRowsMetrics.Of(this);
        MenuHost.Height = m.RowHeight;
        QuickLog.Width = m.Command;
        QuickLog.Height = m.Command - 2;
    }
}
