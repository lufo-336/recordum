using System.Collections.Immutable;
using System.Globalization;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Presentation.Templates;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Today;

namespace Recordum.Presentation.ViewModels.HabitBuilder.Preview;

/// <summary>Turns the builder's draft into the three preview tiers (E7). Pure and deterministic: the
/// same composition always yields the same preview, so the pane never flickers between rebuilds and
/// the sample data can be asserted in tests. No database, no DI — the "Oggi" view-models are the real
/// ones but hand-fed through their bound properties instead of their DB-backed <c>LoadAsync</c>
/// (spec §3, seam 2).</summary>
public sealed class SamplePreviewFactory(IClock clock)
{
    /// <summary>Sample streak, day pattern and today's value. Fixed on purpose (spec §3): a preview
    /// that invents different numbers on every keystroke reads as a bug, not as a sample.</summary>
    private const int SampleStreak = 5;
    private const decimal SampleValue = 2m;
    private const string SampleCleanStreak = "20g 4:12";
    private static readonly int[] DayPattern = [1, 0, 1, 1, 0, 1, 1];

    private readonly IClock _clock = clock;

    public BuilderPreviewModel Build(BuilderPreviewInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new BuilderPreviewModel(BuildHomeRow(input), BuildToday(input), BuildRecording(input));
    }

    private static HomeRowPreviewModel BuildHomeRow(BuilderPreviewInput input) =>
        HomeRowFor(input.Type, HomeRowPresentation.ResolveKeyFigure(input.Type, null),
            HomeRowPresentation.ResolveStrip(null), input.Name, input.Color, input.Icon, input.Fields);

    /// <summary>La riga Home con valore e striscia <b>scelti</b> (Configura: anteprima viva). Stessa logica del builder,
    /// che passa i suoi default; senza campi il valore del contatore e' senza unita'. Con <paramref name="sample"/> falso
    /// (Configura: l'abitudine e' vera) niente numeri inventati: la forma della striscia scelta, a celle vuote, e nessun
    /// valore accanto al nome.</summary>
    public static HomeRowPreviewModel HomeRowFor(HabitType type, KeyFigureKind keyFigure, HomeStrip strip,
        string name, string color, string? icon, bool sample = true) =>
        HomeRowFor(type, sample ? keyFigure : KeyFigureKind.None, strip, name, color, icon, [], sample);

    private static HomeRowPreviewModel HomeRowFor(HabitType type, KeyFigureKind keyFigure, HomeStrip strip,
        string name, string color, string? icon, IReadOnlyList<PreviewField> fields, bool sample = true)
    {
        var length = strip == HomeStrip.None ? 0 : 14;
        var counts = Enumerable.Range(0, length).Select(i => sample ? DayPattern[i % DayPattern.Length] : 0).ToImmutableArray();
        // Bars and Ramp read Fractions; the others read Counts. Filling both keeps the strip honest
        // whichever style the type resolves to.
        var fractions = counts.Select(c => c == 0 ? 0d : 0.4 + (0.2 * (c % 3))).ToImmutableArray();
        return new HomeRowPreviewModel(
            Name: string.IsNullOrWhiteSpace(name) ? Resources.Strings.HabitBuilder_Preview_UnnamedHabit : name,
            Color: color,
            Icon: icon,
            KeyFigure: KeyFigureText(keyFigure, fields),
            StripStyle: HomeRowPresentation.ResolveStripStyle(type),
            Counts: counts,
            Fractions: fractions);
    }

    private static string? KeyFigureText(KeyFigureKind kind, IReadOnlyList<PreviewField> fields) =>
        kind switch
        {
            KeyFigureKind.Streak => SampleStreak.ToString(CultureInfo.CurrentCulture),
            KeyFigureKind.DoneToday => "✓",
            KeyFigureKind.CounterTotal => FormatSample(FirstMeasured(fields)),
            KeyFigureKind.LiveClock => SampleCleanStreak,
            KeyFigureKind.StateValue => SampleStep(FirstMeasured(fields)),
            KeyFigureKind.JournalToday => "1",
            KeyFigureKind.JournalEntriesWeek => "3",
            _ => null,
        };

    private static PreviewField? FirstMeasured(IReadOnlyList<PreviewField> fields) =>
        fields.FirstOrDefault(f => f.Nature != FieldNature.Text);

    /// <summary>The sample value with the field's real unit — including a unit staged in this very
    /// session, which is the point: the preview shows what the user will actually read.</summary>
    private static string FormatSample(PreviewField? field)
    {
        var value = SampleValue.ToString("0.##", CultureInfo.CurrentCulture);
        return UnitLabel.WithValue(value, field?.UnitLabel ?? field?.UnitCode);
    }

    /// <summary>A scale field samples its second step — the first would look like an unset control.</summary>
    private static string SampleStep(PreviewField? field) =>
        field?.ScaleLabels is { Count: > 1 } labels
            ? ScaleLabelText.At(labels, 1)
            : SampleValue.ToString(CultureInfo.CurrentCulture);

    private ITodayContent? BuildToday(BuilderPreviewInput input)
    {
        switch (input.Type)
        {
            case HabitType.Action:
                return new ActionTodayContent(new SampleServices.NoCompletions(), new SampleServices.NoStreaks(),
                    new SampleServices.NoHourly(), _clock)
                {
                    IsDoneToday = true,
                    CurrentStreak = SampleStreak,
                    Pct30 = 80,
                    UsualHour = "08:00",
                };
            case HabitType.Counter:
                var measured = FirstMeasured(input.Fields);
                var counter = new CounterTodayContent(new SampleServices.NoCompletions(), new SampleServices.NoMetrics(),
                    new SampleServices.NoReferenceData(), new SampleServices.NoGoals(), _clock)
                {
                    TodayTotal = SampleValue,
                    TodayTotalDisplay = SampleValue.ToString("0.##", CultureInfo.CurrentCulture),
                    UnitCode = measured?.UnitCode,
                    UnitLabel = measured?.UnitLabel ?? measured?.UnitCode,
                    HasGoal = input.GoalTarget is > 0,
                    GoalFraction = input.GoalTarget is { } t and > 0 ? Math.Min(1d, (double)(SampleValue / t)) : 0d,
                };
                return counter;
            case HabitType.State:
                var labels = FirstMeasured(input.Fields)?.ScaleLabels ?? [];
                return new StateTodayContent(new SampleServices.NoCompletions(), new SampleServices.NoMetrics(),
                    new SampleServices.NoReferenceData(), _clock)
                {
                    Min = 1,
                    Max = Math.Max(2, labels.Count),
                    SliderValue = 2,
                    CurrentLabel = SampleStep(FirstMeasured(input.Fields)),
                };
            case HabitType.Resistance:
                var resistance = new ResistanceTodayContent(new SampleServices.NoAbstinence(), _clock, NullTickSourceFactory.Instance)
                {
                    Status = AbstinenceStatus.Abstaining,
                    AttemptsLast90Days = 1,
                };
                // Set, never Start(): a ticking source in a preview would both leak and
                // contradict the determinism the sample data is built on (spec §3, constraint 3).
                resistance.Clock.Display = SampleCleanStreak;
                return resistance;
            case HabitType.Journal:
                return new JournalTodayContent(new SampleServices.NoUiPrefs()) { Gesture = input.JournalGesture };
            default:
                return null;
        }
    }

    private static RecordingPreviewModel BuildRecording(BuilderPreviewInput input)
    {
        var fields = input.Fields
            .Where(f => f.Nature != FieldNature.Text)
            .Select(f => new RecordingFieldPreview(
                f.Name,
                f.Nature == FieldNature.Scale ? SampleStep(f) : SampleValue.ToString("0.##", CultureInfo.CurrentCulture),
                f.Nature == FieldNature.Scale ? null : f.UnitLabel ?? f.UnitCode))
            .ToList();

        var kind = input.Type switch
        {
            HabitType.Resistance => RecordingPreviewKind.Abstinence,
            HabitType.Journal => input.JournalGesture == JournalPrimaryGesture.QuickNote
                ? RecordingPreviewKind.QuickNote
                : RecordingPreviewKind.Diary,
            HabitType.Counter when input.CountsOccurrences => RecordingPreviewKind.Tap,
            HabitType.Counter or HabitType.State => RecordingPreviewKind.Fields,
            _ => RecordingPreviewKind.Done,
        };

        return new RecordingPreviewModel(
            kind,
            fields,
            // The series chips only exist on a repeatable counter, exactly as in the real quick-add.
            input.Repeatable ? input.SeriesLabels : [],
            input.SegmentNames,
            kind == RecordingPreviewKind.Abstinence ? SampleCleanStreak : null);
    }
}
