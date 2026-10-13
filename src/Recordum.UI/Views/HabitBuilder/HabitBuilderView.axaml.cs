using Avalonia.Controls;
using Recordum.UI.Behaviors;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitBuilder;

namespace Recordum.UI.Views.HabitBuilder;

public partial class HabitBuilderView : UserControl
{
    // Le quattro programmazioni nello scope del mattone (Monthly/Custom restano in Configura) e le etichette
    // localizzate: mai il nome grezzo dell'enum (F0-11).
    public static readonly ScheduleType[] ScheduleTypes =
        [ScheduleType.Daily, ScheduleType.SpecificDays, ScheduleType.WeeklyX, ScheduleType.EveryXDays];
    public static readonly StreakMode[] StreakModes = Enum.GetValues<StreakMode>();
    public static readonly StreakRecovery[] StreakRecoveries = Enum.GetValues<StreakRecovery>();

    public static readonly IValueConverter ScheduleLabel = new FuncValueConverter<ScheduleType, string>(t => t switch
    {
        ScheduleType.Daily => Strings.HabitBuilder_Schedule_Daily,
        ScheduleType.SpecificDays => Strings.HabitBuilder_Schedule_SpecificDays,
        ScheduleType.WeeklyX => Strings.HabitBuilder_Schedule_WeeklyX,
        ScheduleType.EveryXDays => Strings.HabitBuilder_Schedule_EveryXDays,
        _ => string.Empty,
    });
    public static readonly IValueConverter StreakModeLabel = new FuncValueConverter<StreakMode, string>(m => StreakLabels.For(m));
    public static readonly IValueConverter StreakRecoveryLabel = new FuncValueConverter<StreakRecovery, string>(r => StreakLabels.For(r));
    public static readonly IValueConverter NatureLabel = new FuncValueConverter<FieldNature, string>(n => n switch
    {
        FieldNature.Quantity => Strings.HabitBuilder_DefaultField_Quantity,
        FieldNature.Time => Strings.HabitBuilder_DefaultField_Time,
        FieldNature.Scale => Strings.HabitBuilder_DefaultField_Scale,
        FieldNature.Text => Strings.HabitBuilder_DefaultField_Text,
        _ => string.Empty,
    });

    public HabitBuilderView()
    {
        InitializeComponent();
        // Ciò che si salva è ciò che si vede: un testo rifiutato dal binding (altro separatore, raggruppamento)
        // svuota il valore, cosi' Lancia non salva quello vecchio mentre la casella mostra altro (come MetricCellView).
        RejectedTextGuard.Watch<HabitBuilderViewModel>(WeeklyTargetBox, this, vm => vm.Schedule.WeeklyTarget = null);
        RejectedTextGuard.Watch<HabitBuilderViewModel>(EveryXDaysBox, this, vm => vm.Schedule.EveryXDays = null);
        RejectedTextGuard.Watch<HabitBuilderViewModel>(GoalTargetBox, this, vm => vm.GoalBrick.Target = null);
    }
}
