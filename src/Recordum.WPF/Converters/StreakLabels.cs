using Recordum.Core.Enums;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Converters;

/// <summary>Single source of truth for localizing <see cref="StreakMode"/>/<see cref="StreakRecovery"/>
/// (Onda 2.5 T3, F0-11: never show a raw enum name). Consumed by
/// <see cref="StreakModeLabelConverter"/>/<see cref="StreakRecoveryLabelConverter"/> (the builder's
/// Schedule brick ComboBoxes bind through these) and by
/// <c>ScheduleBrickViewModel.BuildSummary</c> (the collapsed Summary is plain text, not a binding a
/// converter can sit on). Was previously duplicated as parallel switch expressions in the converters
/// and the VM — collapsed here after a review finding flagged the copy-paste-swap risk.</summary>
internal static class StreakLabels
{
    internal static string For(StreakMode mode) => mode switch
    {
        StreakMode.Daily => Strings.HabitBuilder_StreakMode_Daily,
        StreakMode.Weekly => Strings.HabitBuilder_StreakMode_Weekly,
        StreakMode.Occurrences => Strings.HabitBuilder_StreakMode_Occurrences,
        StreakMode.Both => Strings.HabitBuilder_StreakMode_Both,
        _ => string.Empty,
    };

    internal static string For(StreakRecovery recovery) => recovery switch
    {
        StreakRecovery.None => Strings.HabitBuilder_Recovery_None,
        StreakRecovery.GraceDay => Strings.HabitBuilder_Recovery_GraceDay,
        StreakRecovery.Manual => Strings.HabitBuilder_Recovery_Manual,
        StreakRecovery.Retroactive => Strings.HabitBuilder_Recovery_Retroactive,
        _ => string.Empty,
    };
}
