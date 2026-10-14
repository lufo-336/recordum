using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Behaviors;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class StreakSectionView : UserControl
{
    public StreakSectionView()
    {
        InitializeComponent();
        RejectedTextGuard.Watch<StreakSectionViewModel>(ToleranceBox, this, vm => vm.StreakToleranceMinutes = null);
    }
}
