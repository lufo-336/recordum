using Recordum.Core.Enums;

namespace Recordum.Core.Composition;

/// <summary>Grammatica pura mattoni→tipo + guardrail (spec §4). Zero dipendenze UI/EF.</summary>
public static class HabitCompositionRules
{
    public static HabitType ResolveType(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Fields.Contains(FieldNature.Text)) return HabitType.Journal;   // #1
        if (c.SeriesEnabled) return HabitType.Resistance;                    // #2
        bool hasMeasured = c.Fields.Any(f => f is FieldNature.Count or FieldNature.Quantity or FieldNature.Time);
        bool hasScale = c.Fields.Contains(FieldNature.Scale);
        if (hasScale && !hasMeasured) return HabitType.State;                // #3
        if (hasMeasured) return HabitType.Counter;                           // #4
        return HabitType.Action;                                            // #5
    }

    public static bool CanAddField(HabitComposition c, FieldNature nature)
    {
        ArgumentNullException.ThrowIfNull(c);
        bool hasText = c.Fields.Contains(FieldNature.Text);
        switch (nature)
        {
            case FieldNature.Text:
                return !hasText && !c.SeriesEnabled && c.Fields.All(f => f == FieldNature.Time);
            case FieldNature.Count:
            case FieldNature.Quantity:
                return !hasText;
            case FieldNature.Scale:
                return !hasText && !c.SeriesEnabled;
            case FieldNature.Time:
                if (hasText) return !c.Fields.Contains(FieldNature.Time);
                return true;
            default:
                return false;
        }
    }

    public static bool CanEnableSeries(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return !c.Fields.Contains(FieldNature.Scale) && !c.Fields.Contains(FieldNature.Text);
    }

    public static bool CanSetMode(HabitComposition c, RecordingMode mode)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (mode == RecordingMode.Manual) return true;
        bool hasMeasured = c.Fields.Any(f => f is FieldNature.Count or FieldNature.Quantity or FieldNature.Time);
        return hasMeasured;
    }
}
