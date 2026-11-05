using Avalonia;
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

    /// <summary>Il creatore offre l'obiettivo aperto o su giorni mobili; il periodo fisso (inizio e fine) resta in Configura.</summary>
    public static readonly GoalPeriodType[] GoalPeriods = [GoalPeriodType.Open, GoalPeriodType.Rolling];

    // "Si comporta come **{0}** · ...": le due meta' della stessa stringa attorno alla parola in grassetto. Proprieta', non
    // campi: si leggono al caricamento della vista, nella lingua di quel momento.
    private const string WordMarker = "**{0}**";
    public static string DerivedLineBefore => Strings.HabitBuilder_DerivedType_Line.Split(WordMarker)[0];
    public static string DerivedLineAfter => Strings.HabitBuilder_DerivedType_Line.Split(WordMarker)[^1];

    /// <summary>L'etichetta sopra il selettore di un campo (UX-12): l'unita' per Quantita' e Tempo, la scala per Scala.</summary>
    public static readonly IValueConverter PickerCaption = new FuncValueConverter<FieldNature, string>(n => n switch
    {
        FieldNature.Quantity or FieldNature.Time => Strings.HabitBuilder_Field_Unit,
        FieldNature.Scale => Strings.Editor_Field_Scale,
        _ => string.Empty,
    });

    /// <summary>Sotto questa larghezza l'anteprima ("Come appare in Home") scende sotto i blocchi.</summary>
    public const double NarrowWidth = 720;
    private bool _narrow;

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
        RejectedTextGuard.Watch<HabitBuilderViewModel>(ToleranceBox, this, vm => vm.Schedule.StreakToleranceMinutes = null);
        RejectedTextGuard.Watch<HabitBuilderViewModel>(RollingDaysBox, this, vm => vm.GoalBrick.RollingDays = null);
        RejectedTextGuard.Watch<HabitBuilderViewModel>(RestBox, this, vm => vm.SessionBrick.RestTimerSeconds = null);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Arrange(e.NewSize.Width < NarrowWidth);
    }

    // Stretto: l'anteprima va in fondo alla colonna che scorre (un solo scorrimento), la colonna di destra sparisce. Largo: torna
    // colonna fissa a destra. Si sposta la stessa vista, mai due anteprime insieme.
    private void Arrange(bool narrow)
    {
        if (narrow == _narrow) return;
        _narrow = narrow;
        ((Panel)PreviewHost.Parent!).Children.Remove(PreviewHost);
        if (narrow)
        {
            LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
            PreviewHost.BorderThickness = new Thickness(0, 1, 0, 0);
            BlocksStack.Children.Add(PreviewHost);
        }
        else
        {
            LayoutGrid.ColumnDefinitions[1].Width = new GridLength(360);
            PreviewHost.BorderThickness = new Thickness(1, 0, 0, 0);
            LayoutGrid.Children.Add(PreviewHost);
        }
    }
}
