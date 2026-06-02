using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

/// <summary>Composizione pronta per il Lancia: il nodo, la sua config, e le metriche
/// (con Id già assegnati lato client). Persistita atomicamente da IComposedHabitService.</summary>
public sealed record ComposedHabitRequest(
    HabitNode Node,
    HabitConfig? Config,
    IReadOnlyList<MetricDefinition> Metrics);
