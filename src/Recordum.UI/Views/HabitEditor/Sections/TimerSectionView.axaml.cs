using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class TimerSectionView : UserControl
{
    public static readonly TimerMode[] Modes = Enum.GetValues<TimerMode>();

    public static readonly IValueConverter ModeLabel = new FuncValueConverter<TimerMode, string>(v => v switch
    {
        TimerMode.Stopwatch => Strings.Editor_TimerMode_Stopwatch,
        TimerMode.Countdown => Strings.Editor_TimerMode_Countdown,
        TimerMode.Primary => Strings.Editor_TimerMode_Primary,
        TimerMode.Session => Strings.Editor_TimerMode_Session,
        _ => string.Empty,
    });

    public TimerSectionView()
    {
        InitializeComponent();
    }
}
