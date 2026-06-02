using Recordum.Core.Enums;

namespace Recordum.Core.Composition;

/// <summary>Snapshot immutabile dei mattoni correnti, input puro della grammatica.</summary>
public sealed record HabitComposition(
    IReadOnlyList<FieldNature> Fields,
    bool SeriesEnabled,
    RecordingMode Mode)
{
    public static HabitComposition Empty { get; } =
        new(Array.Empty<FieldNature>(), SeriesEnabled: false, RecordingMode.Manual);
}
