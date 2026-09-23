using System.Globalization;
using Avalonia.Data;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Converters;
using Xunit;

namespace Recordum.UI.Tests.Converters;

public class EnumAndLabelConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // ── EnumToBoolConverter ────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnumToBool_is_true_only_when_value_equals_the_parameter()
    {
        var c = EnumToBoolConverter.Instance;

        c.Convert(HomeTab.Archived, typeof(bool), HomeTab.Archived, Culture).Should().Be(true);
        c.Convert(HomeTab.Active, typeof(bool), HomeTab.Archived, Culture).Should().Be(false);
    }

    [Fact]
    public void EnumToBool_is_false_when_value_or_parameter_is_null()
    {
        var c = EnumToBoolConverter.Instance;

        c.Convert(null, typeof(bool), HomeTab.Active, Culture).Should().Be(false);
        c.Convert(HomeTab.Active, typeof(bool), null, Culture).Should().Be(false);
    }

    [Fact]
    public void EnumToBool_does_not_match_a_string_parameter_against_an_enum()
    {
        // Il parametro va passato tipizzato ({x:Static vm:HomeTab.Active}): il confronto è per
        // uguaglianza, e "Active" != HomeTab.Active. Il test fissa il contratto, non lo estende.
        EnumToBoolConverter.Instance.Convert(HomeTab.Active, typeof(bool), "Active", Culture).Should().Be(false);
    }

    [Fact]
    public void EnumToBool_convert_back_returns_the_parameter_when_checked()
    {
        EnumToBoolConverter.Instance.ConvertBack(true, typeof(HomeTab), HomeTab.Trash, Culture)
            .Should().Be(HomeTab.Trash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void EnumToBool_convert_back_writes_nothing_when_unchecked(object? value)
    {
        // Deselezionare un radio non deve azzerare la proprietà legata: DoNothing, non null.
        EnumToBoolConverter.Instance.ConvertBack(value, typeof(HomeTab), HomeTab.Trash, Culture)
            .Should().BeSameAs(BindingOperations.DoNothing);
    }

    // ── HabitTypeLabelConverter ────────────────────────────────────────────────────────────────

    [Fact]
    public void HabitTypeLabel_maps_each_type_to_its_own_distinct_label()
    {
        var c = HabitTypeLabelConverter.Instance;

        c.Convert(HabitType.Action, typeof(string), null, Culture).Should().Be(Strings.Home_HabitType_Action);
        c.Convert(HabitType.Counter, typeof(string), null, Culture).Should().Be(Strings.Home_HabitType_Counter);
        c.Convert(HabitType.Resistance, typeof(string), null, Culture).Should().Be(Strings.Home_HabitType_Resistance);
        c.Convert(HabitType.Journal, typeof(string), null, Culture).Should().Be(Strings.Home_HabitType_Journal);
        c.Convert(HabitType.State, typeof(string), null, Culture).Should().Be(Strings.Home_HabitType_State);

        // Cinque etichette uguali passerebbero i confronti sopra (ognuno è "uguale a se stesso"):
        // questo è il controllo che le cinque chiavi siano davvero cinque parole.
        var labels = Enum.GetValues<HabitType>()
            .Select(t => (string)c.Convert(t, typeof(string), null, Culture))
            .ToList();
        labels.Should().OnlyHaveUniqueItems();
        labels.Should().OnlyContain(l => !string.IsNullOrEmpty(l));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Action")]
    [InlineData(3)]
    public void HabitTypeLabel_is_empty_for_a_value_that_is_not_a_habit_type(object? input)
    {
        HabitTypeLabelConverter.Instance.Convert(input, typeof(string), null, Culture).Should().Be(string.Empty);
    }
}
