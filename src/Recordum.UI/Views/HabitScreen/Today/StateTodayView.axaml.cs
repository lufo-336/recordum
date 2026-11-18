using Avalonia.Controls;
using Recordum.UI.Behaviors;

namespace Recordum.UI.Views.HabitScreen.Today;

public partial class StateTodayView : UserControl
{
    public StateTodayView()
    {
        InitializeComponent();
        CommitOnEnter.Watch(this);
    }
}
