using Avalonia.Controls;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Behaviors;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class TimerSectionView : UserControl
{
    public static readonly TimerMode[] Modes = Enum.GetValues<TimerMode>();

    public TimerSectionView()
    {
        InitializeComponent();
        RejectedTextGuard.Watch<TimerSectionViewModel>(TargetBox, this, vm => vm.TimerTargetSeconds = null);
        RejectedTextGuard.Watch<TimerSectionViewModel>(RestBox, this, vm => vm.RestTimerSeconds = null);
    }
}
