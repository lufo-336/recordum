using Recordum.Core.Composition;
using Recordum.Core.Enums;
using FluentAssertions;
using Xunit;

namespace Recordum.Core.Tests.Composition;

public class HabitCompositionRulesTests
{
    private static HabitComposition Comp(bool series = false, RecordingMode mode = RecordingMode.Manual, params FieldNature[] fields)
        => new(fields, series, mode);

    [Fact] public void Text_present_resolves_journal()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Text })).Should().Be(HabitType.Journal);

    [Fact] public void Text_with_time_still_journal()
        => HabitCompositionRules.ResolveType(Comp(mode: RecordingMode.Stopwatch, fields: new[]{ FieldNature.Text, FieldNature.Time })).Should().Be(HabitType.Journal);

    [Fact] public void Series_resolves_resistance()
        => HabitCompositionRules.ResolveType(Comp(series: true, fields: new[]{ FieldNature.Count, FieldNature.Time })).Should().Be(HabitType.Resistance);

    [Fact] public void Scale_only_resolves_state()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Scale })).Should().Be(HabitType.State);

    [Fact] public void Count_resolves_counter()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Count })).Should().Be(HabitType.Counter);

    [Fact] public void Time_resolves_counter()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Time })).Should().Be(HabitType.Counter);

    [Fact] public void Scale_plus_count_resolves_counter()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Scale, FieldNature.Count })).Should().Be(HabitType.Counter);

    [Fact] public void Empty_resolves_action()
        => HabitCompositionRules.ResolveType(HabitComposition.Empty).Should().Be(HabitType.Action);

    [Fact] public void Text_wins_over_series_priority()
        => HabitCompositionRules.ResolveType(Comp(series: true, fields: new[]{ FieldNature.Text })).Should().Be(HabitType.Journal);

    [Fact] public void Cannot_add_count_when_text_present()
        => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Text }), FieldNature.Count).Should().BeFalse();

    [Fact] public void Can_add_time_when_text_present()
        => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Text }), FieldNature.Time).Should().BeTrue();

    [Fact] public void Cannot_add_second_time_to_journal()
        => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Text, FieldNature.Time }), FieldNature.Time).Should().BeFalse();

    [Fact] public void Cannot_add_text_when_other_fields_present()
        => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Count }), FieldNature.Text).Should().BeFalse();

    [Fact] public void Cannot_add_scale_when_series_enabled()
        => HabitCompositionRules.CanAddField(Comp(series: true, fields: new[]{ FieldNature.Count }), FieldNature.Scale).Should().BeFalse();

    [Fact] public void Cannot_enable_series_with_scale()
        => HabitCompositionRules.CanEnableSeries(Comp(fields: new[]{ FieldNature.Scale })).Should().BeFalse();

    [Fact] public void Cannot_enable_series_with_text()
        => HabitCompositionRules.CanEnableSeries(Comp(fields: new[]{ FieldNature.Text })).Should().BeFalse();

    [Fact] public void Can_enable_series_with_count()
        => HabitCompositionRules.CanEnableSeries(Comp(fields: new[]{ FieldNature.Count })).Should().BeTrue();

    [Fact] public void Timer_mode_rules()
    {
        HabitCompositionRules.CanSetMode(HabitComposition.Empty, RecordingMode.Stopwatch).Should().BeFalse();
        HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Count }), RecordingMode.Stopwatch).Should().BeTrue();
        HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Scale }), RecordingMode.Countdown).Should().BeFalse();
        HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Text, FieldNature.Time }), RecordingMode.Stopwatch).Should().BeTrue();
        HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Count }), RecordingMode.Manual).Should().BeTrue();
    }
}
