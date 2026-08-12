using System.Collections.Immutable;
using System.Globalization;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Today;

namespace Recordum.WPF.ViewModels.HabitBuilder.Preview;

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

    private static HomeRowPreviewModel BuildHomeRow(BuilderPreviewInput input)
    {
        var length = HomeRowPresentation.ResolveStrip(input.Type, null) == HomeStrip.Days14 ? 14 : 7;
        var counts = Enumerable.Range(0, length).Select(i => DayPattern[i % DayPattern.Length]).ToImmutableArray();
        // Bars and Ramp read Fractions; the others read Counts. Filling both keeps the strip honest
        // whichever style the type resolves to.
        var fractions = counts.Select(c => c == 0 ? 0d : 0.4 + (0.2 * (c % 3))).ToImmutableArray();
        return new HomeRowPreviewModel(
            Name: string.IsNullOrWhiteSpace(input.Name) ? Resources.Strings.HabitBuilder_Preview_UnnamedHabit : input.Name,
            Color: input.Color,
            Icon: input.Icon,
            KeyFigure: KeyFigureText(input),
            StripStyle: HomeRowPresentation.ResolveStripStyle(input.Type),
            Counts: counts,
            Fractions: fractions);
    }

    private static string? KeyFigureText(BuilderPreviewInput input) =>
        HomeRowPresentation.ResolveKeyFigure(input.Type, null) switch
        {
            KeyFigureKind.Streak => SampleStreak.ToString(CultureInfo.CurrentCulture),
            KeyFigureKind.DoneToday => "✓",
            KeyFigureKind.CounterTotal => FormatSample(FirstMeasured(input)),
            KeyFigureKind.LiveClock => SampleCleanStreak,
            KeyFigureKind.StateValue => SampleStep(FirstMeasured(input)),
            KeyFigureKind.JournalToday => "1",
            _ => null,
        };

    private static PreviewField? FirstMeasured(BuilderPreviewInput input) =>
        input.Fields.FirstOrDefault(f => f.Nature != FieldNature.Text);

    /// <summary>The sample value with the field's real unit — including a unit staged in this very
    /// session, which is the point: the preview shows what the user will actually read.</summary>
    private static string FormatSample(PreviewField? field)
    {
        var value = SampleValue.ToString("0.##", CultureInfo.CurrentCulture);
        return field?.UnitCode is { Length: > 0 } code ? $"{value} {code}" : value;
    }

    /// <summary>A scale field samples its second step — the first would look like an unset control.</summary>
    private static string SampleStep(PreviewField? field) =>
        field?.ScaleLabels is { Count: > 1 } labels
            ? labels[1]
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
                var counter = new CounterTodayContent(new SampleServices.NoCompletions(), new SampleServices.NoMetrics(),
                    new SampleServices.NoReferenceData(), new SampleServices.NoGoals(), _clock)
                {
                    TodayTotal = SampleValue,
                    TodayTotalDisplay = SampleValue.ToString("0.##", CultureInfo.CurrentCulture),
                    UnitCode = FirstMeasured(input)?.UnitCode,
                    HasGoal = input.GoalTarget is > 0,
                    GoalFraction = input.GoalTarget is { } t and > 0 ? Math.Min(1d, (double)(SampleValue / t)) : 0d,
                };
                return counter;
            case HabitType.State:
                var labels = FirstMeasured(input)?.ScaleLabels ?? [];
                return new StateTodayContent(new SampleServices.NoCompletions(), new SampleServices.NoMetrics(),
                    new SampleServices.NoReferenceData(), _clock)
                {
                    Min = 1,
                    Max = Math.Max(2, labels.Count),
                    SliderValue = 2,
                    CurrentLabel = SampleStep(FirstMeasured(input)),
                };
            case HabitType.Resistance:
                var resistance = new ResistanceTodayContent(new SampleServices.NoAbstinence(), _clock)
                {
                    Status = AbstinenceStatus.Abstaining,
                    AttemptsLast90Days = 1,
                };
                // Set, never Start(): a ticking DispatcherTimer in a preview would both leak and
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
                f.Nature == FieldNature.Scale ? null : f.UnitCode))
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
