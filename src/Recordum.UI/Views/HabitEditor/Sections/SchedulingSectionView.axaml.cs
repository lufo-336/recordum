using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Behaviors;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class SchedulingSectionView : UserControl
{
    public static readonly ScheduleType[] ScheduleTypes = Enum.GetValues<ScheduleType>();

    public static readonly IValueConverter ScheduleLabel = new FuncValueConverter<ScheduleType, string>(t => t switch
    {
        ScheduleType.Daily => Strings.HabitBuilder_Schedule_Daily,
        ScheduleType.SpecificDays => Strings.HabitBuilder_Schedule_SpecificDays,
        ScheduleType.WeeklyX => Strings.HabitBuilder_Schedule_WeeklyX,
        ScheduleType.EveryXDays => Strings.HabitBuilder_Schedule_EveryXDays,
        ScheduleType.Monthly => Strings.Editor_Schedule_Monthly,
        ScheduleType.Custom => Strings.Editor_Schedule_Custom,
        _ => string.Empty,
    });

    public SchedulingSectionView()
    {
        InitializeComponent();
        // Ciò che si salva è ciò che si vede: un testo rifiutato (altro separatore) svuota il valore.
        RejectedTextGuard.Watch<SchedulingSectionViewModel>(WeeklyTargetBox, this, vm => vm.ScheduleWeeklyTarget = null);
        RejectedTextGuard.Watch<SchedulingSectionViewModel>(EveryXDaysBox, this, vm => vm.ScheduleEveryXDays = null);
    }
}
