using Recordum.Core.Enums;

namespace Recordum.Core.Models;

/// <summary>
/// Per-node presentation preferences, serialized to <see cref="HabitNode.UiPrefsJson"/>.
/// Every field nullable: null = fall back to the habit type's default. Consumed by Onda 2.1–2.5.
/// Governance: each field is documented in docs/ARCHITECTURE.md — keep them in sync.
/// </summary>
public sealed record HabitUiPrefs
{
    /// <summary>Home-row key-figure id (per-type set defined by the consuming surface). Null = type default.</summary>
    public string? KeyFigure { get; init; }

    /// <summary>Home-row strip length. Null = type default.</summary>
    public HomeStrip? Strip { get; init; }

    /// <summary>Per-habit open workspace columns (column identifiers). Null = type default.</summary>
    public IReadOnlyList<string>? OpenColumns { get; init; }

    /// <summary>Journal primary gesture. Null = template/type default.</summary>
    public JournalPrimaryGesture? JournalGesture { get; init; }
}
