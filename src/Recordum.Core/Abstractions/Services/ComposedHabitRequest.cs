using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

/// <summary>Composizione pronta per il Lancia: il nodo, la sua config, le metriche
/// (con Id già assegnati lato client) e, opzionalmente, i mattoni restanti del builder —
/// segmenti, goal, unità/scale staged (non ancora persistite) e ui_prefs. Persistita
/// atomicamente da IComposedHabitService in un'unica transazione (2.5 T2).</summary>
public sealed record ComposedHabitRequest(
    HabitNode Node,
    HabitConfig? Config,
    IReadOnlyList<MetricDefinition> Metrics,
    IReadOnlyList<SegmentDefinition>? Segments = null,
    Goal? Goal = null,
    IReadOnlyList<UnitOfMeasure>? NewUnits = null,
    IReadOnlyList<ScaleTemplate>? NewScales = null,
    HabitUiPrefs? UiPrefs = null);
