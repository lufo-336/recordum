using System.Collections.Immutable;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.WPF.ViewModels.HabitScreen.Today;

namespace Recordum.WPF.ViewModels.HabitBuilder.Preview;

/// <summary>One measured field of the draft, as the preview needs it: enough to render a sample
/// recording row and to format the sample value with the right unit or scale step.</summary>
public sealed record PreviewField(
    string Name,
    FieldNature Nature,
    string? UnitCode,
    IReadOnlyList<string> ScaleLabels);

/// <summary>The draft, projected into what the preview needs — no view-models, no services, so the
/// factory is a pure function of the composition (determinism, spec §3).</summary>
public sealed record BuilderPreviewInput(
    string Name,
    string Color,
    string? Icon,
    HabitType Type,
    bool CountsOccurrences,
    bool Repeatable,
    JournalPrimaryGesture JournalGesture,
    IReadOnlyList<PreviewField> Fields,
    IReadOnlyList<string> SeriesLabels,
    IReadOnlyList<string> SegmentNames,
    decimal? GoalTarget);

/// <summary>The minimal visual model of a Home row (spec §3): the production
/// <c>HomeRowViewModel</c> is too coupled to be reused here — four services and the tree machinery —
/// so the pane renders the same visual pieces (4px accent bar, key-figure chip, the real
/// <c>HeatmapStrip</c>) off this record instead.</summary>
public sealed record HomeRowPreviewModel(
    string Name,
    string Color,
    string? Icon,
    string? KeyFigure,
    StripStyle StripStyle,
    ImmutableArray<int> Counts,
    ImmutableArray<double> Fractions);

/// <summary>What the third pane shows. Not the production quick-add: those DataTemplates live inside
/// <c>HabitScreen.xaml</c>'s resources, wired to DB-backed view-models, and one of them hosts the
/// Markdown editor. The preview renders a per-kind sample of the same shape instead (spec §3, the
/// escape hatch) — which also makes "no WebView2 in the preview" true by construction rather than by
/// a binding that happens to be null.</summary>
public sealed record RecordingPreviewModel(
    RecordingPreviewKind Kind,
    IReadOnlyList<RecordingFieldPreview> Fields,
    IReadOnlyList<string> SeriesLabels,
    IReadOnlyList<string> SegmentNames,
    string? Clock);

public enum RecordingPreviewKind
{
    /// <summary>Action: a single done-today control.</summary>
    Done,
    /// <summary>Counter with the bare tap block: one "+1".</summary>
    Tap,
    /// <summary>Counter / State with fields: a row per field.</summary>
    Fields,
    /// <summary>Resistance: start / relapse plus the (static) clean-streak clock.</summary>
    Abstinence,
    /// <summary>Journal with the Diary gesture: a static stand-in for the Markdown editor.</summary>
    Diary,
    /// <summary>Journal with the QuickNote gesture: the quick-note composer's shape.</summary>
    QuickNote,
}

/// <summary>A sample recording row: the field's name, the sample value already formatted, and the
/// unit to print beside it (null when the value speaks for itself, e.g. a scale step).</summary>
public sealed record RecordingFieldPreview(string Name, string Value, string? Unit);

/// <summary>The whole preview: the three tiers, rebuilt on every change of the draft.</summary>
public sealed record BuilderPreviewModel(
    HomeRowPreviewModel HomeRow,
    ITodayContent? Today,
    RecordingPreviewModel Recording);
