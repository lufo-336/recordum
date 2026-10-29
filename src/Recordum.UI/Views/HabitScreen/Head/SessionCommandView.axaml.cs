using Avalonia.Controls;

namespace Recordum.UI.Views.HabitScreen.Head;

public partial class SessionCommandView : UserControl
{
    public SessionCommandView() => InitializeComponent();

    /// <summary>Le celle di "+ a mano" (il quick-add del contatore, costruito dal tipo); senza, niente "+ a mano".</summary>
    public SessionCommandView(Control? manual) : this()
    {
        ManualHost.Child = manual;
        ManualButton.IsVisible = manual is not null;
    }
}
