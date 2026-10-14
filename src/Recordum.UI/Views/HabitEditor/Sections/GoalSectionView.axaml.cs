using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Behaviors;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class GoalSectionView : UserControl
{
    public static readonly GoalPeriodType[] PeriodTypes = Enum.GetValues<GoalPeriodType>();

    public static readonly IValueConverter PeriodLabel = new FuncValueConverter<GoalPeriodType, string>(p => p switch
    {
        GoalPeriodType.Fixed => Strings.Editor_GoalPeriod_Fixed,
        GoalPeriodType.Rolling => Strings.Editor_GoalPeriod_Rolling,
        GoalPeriodType.Open => Strings.Editor_GoalPeriod_Open,
        _ => string.Empty,
    });

    public GoalSectionView()
    {
        InitializeComponent();
        // TargetValue e' un decimal non nullo: un testo rifiutato lo azzera (cio' che si salva e' cio' che si vede).
        RejectedTextGuard.Watch<GoalSectionViewModel>(TargetBox, this, vm => vm.TargetValue = 0m);
        RejectedTextGuard.Watch<GoalSectionViewModel>(RollingDaysBox, this, vm => vm.RollingDays = null);
    }
}
