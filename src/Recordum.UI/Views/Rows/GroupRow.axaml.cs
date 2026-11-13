using Avalonia;
using Avalonia.Controls;
using Recordum.UI.Controls;

namespace Recordum.UI.Views.Rows;

public partial class GroupRow : UserControl
{
    static GroupRow()
    {
        HomeRowsMetrics.RowsProperty.Changed.AddClassHandler<GroupRow>((row, _) => row.ApplyRows());
    }

    public GroupRow()
    {
        InitializeComponent();
        ApplyRows();
        RowMenu.AttachOnDemand(this, MenuHost);
    }

    private void ApplyRows() => MenuHost.Height = HomeRowsMetrics.Of(this).RowHeight;
}
