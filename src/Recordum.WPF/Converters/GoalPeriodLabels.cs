using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.WPF.Converters;

/// <summary>Single source of truth for localizing <see cref="GoalPeriodType"/> (Onda 2.5 T4, F0-11
/// pattern: never show a raw enum name). Consumed by <see cref="GoalPeriodLabelConverter"/> (the
/// builder's Goal brick period ComboBox binds through it) — same shared-label shape as
/// <see cref="StreakLabels"/>.</summary>
internal static class GoalPeriodLabels
{
    internal static string For(GoalPeriodType type) => type switch
    {
        GoalPeriodType.Open => Strings.HabitBuilder_Goal_Period_Open,
        GoalPeriodType.Fixed => Strings.HabitBuilder_Goal_Period_Fixed,
        GoalPeriodType.Rolling => Strings.HabitBuilder_Goal_Period_Rolling,
        _ => string.Empty,
    };
}
