using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

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
    }
}
