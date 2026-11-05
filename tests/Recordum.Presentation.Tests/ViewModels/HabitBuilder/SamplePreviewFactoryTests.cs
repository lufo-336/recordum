using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

/// <summary>Onda 2.5 T9 — the sample data behind the three-tier preview (E7).</summary>
public class SamplePreviewFactoryTests
{
    private static SamplePreviewFactory Factory() => new(new FakeClock());

    private static BuilderPreviewInput Input(
        HabitType type,
        IReadOnlyList<PreviewField>? fields = null,
        bool countsOccurrences = false,
        bool repeatable = false,
        JournalPrimaryGesture gesture = JournalPrimaryGesture.Diary,
        IReadOnlyList<string>? seriesLabels = null,
        IReadOnlyList<string>? segments = null,
        decimal? goal = null) =>
        new("Tè", "#123456", null, type, countsOccurrences, repeatable, gesture,
            fields ?? [], seriesLabels ?? [], segments ?? [], goal);

    [Fact]
    public void A_counter_key_figure_carries_the_unit_of_its_field()
    {
        var input = Input(HabitType.Counter, [new PreviewField("Tazze", FieldNature.Quantity, "tazze", [])]);

        var preview = Factory().Build(input);

        preview.HomeRow.KeyFigure.Should().Contain("tazze");
        preview.HomeRow.StripStyle.Should().Be(StripStyle.Bars);
    }

    [Fact]
    public void A_resistance_row_shows_the_static_clean_streak_over_a_fortnight()
    {
        var preview = Factory().Build(Input(HabitType.Resistance));

        preview.HomeRow.KeyFigure.Should().Be("20g 4:12");
        preview.HomeRow.StripStyle.Should().Be(StripStyle.Band);
        // Resistance is the one type whose strip defaults to 14 days (HomeRowPresentation).
        preview.HomeRow.Counts.Should().HaveCount(14);
    }

    [Fact]
    public void A_state_row_samples_the_second_step_of_its_scale()
    {
        var input = Input(HabitType.State,
            [new PreviewField("Umore", FieldNature.Scale, null, ["Male", "Così così", "Bene"])]);

        var preview = Factory().Build(input);

        preview.HomeRow.KeyFigure.Should().Be("Così così");
        preview.HomeRow.StripStyle.Should().Be(StripStyle.Ramp);
    }

    [Fact]
    public void A_blank_second_step_samples_its_step_number()
    {
        var input = Input(HabitType.State,
            [new PreviewField("Umore", FieldNature.Scale, null, ["Male", "", "Bene"])]);

        Factory().Build(input).HomeRow.KeyFigure.Should().Be("2");
    }

    [Fact]
    public void An_unnamed_draft_still_renders_a_row()
    {
        var input = Input(HabitType.Action) with { Name = "   " };

        Factory().Build(input).HomeRow.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(HabitType.Action, typeof(ActionTodayContent))]
    [InlineData(HabitType.Counter, typeof(CounterTodayContent))]
    [InlineData(HabitType.State, typeof(StateTodayContent))]
    [InlineData(HabitType.Resistance, typeof(ResistanceTodayContent))]
    [InlineData(HabitType.Journal, typeof(JournalTodayContent))]
    public void The_today_tier_is_the_real_per_type_component(HabitType type, Type expected)
    {
        Factory().Build(Input(type)).Today.Should().BeOfType(expected);
    }

    [Fact]
    public void A_journal_with_the_diary_gesture_previews_a_static_stand_in()
    {
        var preview = Factory().Build(Input(HabitType.Journal, gesture: JournalPrimaryGesture.Diary));

        preview.Recording.Kind.Should().Be(RecordingPreviewKind.Diary);
    }

    [Fact]
    public void A_journal_with_the_quick_note_gesture_previews_the_composer()
    {
        var preview = Factory().Build(Input(HabitType.Journal, gesture: JournalPrimaryGesture.QuickNote));

        preview.Recording.Kind.Should().Be(RecordingPreviewKind.QuickNote);
    }

    [Fact]
    public void A_bare_tap_counter_previews_the_plus_one_and_no_fields()
    {
        var preview = Factory().Build(Input(HabitType.Counter, countsOccurrences: true));

        preview.Recording.Kind.Should().Be(RecordingPreviewKind.Tap);
        preview.Recording.Fields.Should().BeEmpty();
    }

    [Fact]
    public void A_measured_counter_previews_a_row_per_field_with_its_unit()
    {
        var input = Input(HabitType.Counter, [
            new PreviewField("Durata", FieldNature.Time, "minutes", []),
            new PreviewField("Serie", FieldNature.Quantity, null, []),
        ]);

        var preview = Factory().Build(input);

        preview.Recording.Kind.Should().Be(RecordingPreviewKind.Fields);
        preview.Recording.Fields.Select(f => (f.Name, f.Unit)).Should()
            .Equal(("Durata", "minutes"), ("Serie", (string?)null));
    }

    [Fact]
    public void Series_labels_reach_the_recording_preview_only_when_repeatable()
    {
        var fields = new[] { new PreviewField("Ore", FieldNature.Quantity, null, []) };
        var with = Input(HabitType.Counter, fields, repeatable: true, seriesLabels: ["Notte", "Riposino"]);
        var without = with with { Repeatable = false };

        Factory().Build(with).Recording.SeriesLabels.Should().Equal("Notte", "Riposino");
        Factory().Build(without).Recording.SeriesLabels.Should().BeEmpty();
    }

    [Fact]
    public void The_preview_is_deterministic_for_the_same_composition()
    {
        // The pane is rebuilt on every keystroke; sample data that wandered would read as a bug.
        var input = Input(HabitType.Counter, [new PreviewField("Tazze", FieldNature.Quantity, "tazze", [])]);
        var factory = Factory();

        var first = factory.Build(input);
        var second = factory.Build(input);

        // Structural, not record equality: ImmutableArray compares by underlying-array reference, so
        // two runs that produce the very same numbers would still not be Equal.
        first.HomeRow.Should().BeEquivalentTo(second.HomeRow);
        first.Recording.Should().BeEquivalentTo(second.Recording);
    }
}
