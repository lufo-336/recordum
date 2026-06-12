using Recordum.Core.Enums;

namespace Recordum.Core.Composition;

/// <summary>Grammatica pura mattoni→tipo + guardrail (spec §3). Zero dipendenze UI/EF.</summary>
public static class HabitCompositionRules
{
    public static HabitType ResolveType(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Fields.Contains(FieldNature.Text)) return HabitType.Journal;            // #1
        if (c.Abstinence) return HabitType.Resistance;                                 // #2
        bool hasMeasured = c.Fields.Any(f => f is FieldNature.Quantity or FieldNature.Time);
        bool hasScale = c.Fields.Contains(FieldNature.Scale);
        if (hasScale && !hasMeasured && !c.CountsOccurrences) return HabitType.State;  // #3
        if (c.CountsOccurrences || hasMeasured) return HabitType.Counter;              // #4
        return HabitType.Action;                                                       // #5
    }

    public static bool CanAddField(HabitComposition c, FieldNature nature)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.CountsOccurrences) return false; // il contatore nudo non ha campi (spec §3.3 nota 2)
        bool hasText = c.Fields.Contains(FieldNature.Text);
        switch (nature)
        {
            case FieldNature.Text:
                return !hasText && !c.Abstinence && c.Fields.All(f => f == FieldNature.Time);
            case FieldNature.Quantity:
            case FieldNature.Scale:
                return !hasText;
            case FieldNature.Time:
                if (hasText) return !c.Fields.Contains(FieldNature.Time);
                return true;
            case FieldNature.Count: // non è più un campo: mattone dedicato CountsOccurrences
            default:
                return false;
        }
    }

    /// <summary>Il mattone contatore vale solo a tela vuota: con campi o astinenza il conteggio è implicito.</summary>
    public static bool CanEnableCounter(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Fields.Count == 0 && !c.Abstinence;
    }

    public static bool CanEnableAbstinence(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return !c.Fields.Contains(FieldNature.Text) && !c.CountsOccurrences;
    }

    public static bool CanSetMode(HabitComposition c, RecordingMode mode)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (mode == RecordingMode.Manual) return true;
        return c.Fields.Any(f => f is FieldNature.Quantity or FieldNature.Time);
    }
}
