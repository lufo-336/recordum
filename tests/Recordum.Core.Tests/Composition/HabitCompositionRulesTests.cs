using FluentAssertions;
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using Xunit;

namespace Recordum.Core.Tests.Composition;

public class HabitCompositionRulesTests
{
    private static HabitComposition C(bool counts = false, bool abst = false, params FieldNature[] fields) =>
        new(fields, counts, abst);

    [Fact] public void Text_wins() => Assert.Equal(HabitType.Journal, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Text, FieldNature.Time])));
    [Fact] public void Abstinence_resolves_resistance() => Assert.Equal(HabitType.Resistance, HabitCompositionRules.ResolveType(C(abst: true, fields: [FieldNature.Quantity, FieldNature.Scale])));
    [Fact] public void Scale_only_is_state() => Assert.Equal(HabitType.State, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Scale])));
    [Fact] public void Counts_occurrences_is_counter() => Assert.Equal(HabitType.Counter, HabitCompositionRules.ResolveType(C(counts: true)));
    [Fact] public void Measured_field_is_counter() => Assert.Equal(HabitType.Counter, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Time])));
    [Fact] public void Scale_plus_measured_is_counter() => Assert.Equal(HabitType.Counter, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Scale, FieldNature.Quantity])));
    [Fact] public void Empty_is_action() => Assert.Equal(HabitType.Action, HabitCompositionRules.ResolveType(C()));

    [Fact] public void Count_nature_no_longer_addable() => Assert.False(HabitCompositionRules.CanAddField(C(), FieldNature.Count));
    [Fact] public void Fields_blocked_when_counting() => Assert.All(
        new[] { FieldNature.Quantity, FieldNature.Time, FieldNature.Scale, FieldNature.Text },
        n => Assert.False(HabitCompositionRules.CanAddField(C(counts: true), n)));
    [Fact] public void Abstinence_allows_measured_and_scale() => Assert.All(
        new[] { FieldNature.Quantity, FieldNature.Time, FieldNature.Scale },
        n => Assert.True(HabitCompositionRules.CanAddField(C(abst: true), n)));
    [Fact] public void Abstinence_blocks_text() => Assert.False(HabitCompositionRules.CanAddField(C(abst: true), FieldNature.Text));
    [Fact] public void Journal_time_relaxation_survives() => Assert.True(HabitCompositionRules.CanAddField(C(fields: [FieldNature.Text]), FieldNature.Time));

    [Fact] public void Counter_block_requires_empty_canvas() { Assert.True(HabitCompositionRules.CanEnableCounter(C())); Assert.False(HabitCompositionRules.CanEnableCounter(C(fields: [FieldNature.Quantity]))); Assert.False(HabitCompositionRules.CanEnableCounter(C(abst: true))); }
    [Fact] public void Abstinence_blocked_by_text_and_counter() { Assert.True(HabitCompositionRules.CanEnableAbstinence(C(fields: [FieldNature.Scale]))); Assert.False(HabitCompositionRules.CanEnableAbstinence(C(fields: [FieldNature.Text]))); Assert.False(HabitCompositionRules.CanEnableAbstinence(C(counts: true))); }

    [Fact]
    public void Composition_has_no_recording_mode()
    {
        typeof(HabitComposition).GetProperty("Mode").Should().BeNull(
            "il modo di registrazione è uscito dalla grammatica (Onda 2.5, decisione 4: il mattone Sessione lo sostituisce)");
    }
}
