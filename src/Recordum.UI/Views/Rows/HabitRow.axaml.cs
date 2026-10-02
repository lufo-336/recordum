using Avalonia.Controls;

namespace Recordum.UI.Views.Rows;

public partial class HabitRow : UserControl
{
    // InitializeComponent(), mai AvaloniaXamlLoader.Load(this): il secondo non cabla gli x:Name.
    public HabitRow() => InitializeComponent();
}
