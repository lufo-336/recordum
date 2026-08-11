using Recordum.Core.Enums;

namespace Recordum.Core.Composition;

/// <summary>Snapshot immutabile dei mattoni correnti, input puro della grammatica.</summary>
public sealed record HabitComposition(
    IReadOnlyList<FieldNature> Fields,
    bool CountsOccurrences,
    bool Abstinence)
{
    public static HabitComposition Empty { get; } =
        new(Array.Empty<FieldNature>(), CountsOccurrences: false, Abstinence: false);
}
