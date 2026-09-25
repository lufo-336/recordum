using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Converters;
using Recordum.Presentation.Resources;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

/// <summary>Exercises the actual XAML-bound converters the Schedule brick's two StreakMode/
/// StreakRecovery ComboBoxes render through (Onda 2.5 T3 review finding: the prior regression test only
/// covered <c>ScheduleBrickViewModel.BuildSummary</c>'s local switch, leaving the converter classes —
/// the real UI code path — with no coverage of their own; a revert to raw <c>value.ToString()</c>, or a
/// copy-paste swap between the two switch statements, would have passed the suite undetected).</summary>
/// <para>Gira in italiano: in inglese tre etichette coincidono col nome dell'enum ("Daily", "Weekly", "Both"),
/// e il guardiano <c>NotBe(value.ToString())</c> non saprebbe distinguere una traduzione da un <c>ToString()</c>
/// grezzo. Passava sulla VM (Windows in italiano) e falliva sul runner della CI (in inglese).</para>
public sealed class StreakModeAndRecoveryLabelConverterTests : IDisposable
{
    private readonly StreakModeLabelConverter _mode = new();
    private readonly StreakRecoveryLabelConverter _recovery = new();
    private readonly CultureInfo? _previousCulture = Strings.Culture;

    public StreakModeAndRecoveryLabelConverterTests() => Strings.Culture = new CultureInfo("it-IT");

    public void Dispose() => Strings.Culture = _previousCulture;

    [Theory]
    [InlineData(StreakMode.Daily)]
    [InlineData(StreakMode.Weekly)]
    [InlineData(StreakMode.Occurrences)]
    [InlineData(StreakMode.Both)]
    public void StreakModeLabelConverter_localizes_every_value_the_brick_offers(StreakMode value)
    {
        var expected = value switch
        {
            StreakMode.Daily => Strings.HabitBuilder_StreakMode_Daily,
            StreakMode.Weekly => Strings.HabitBuilder_StreakMode_Weekly,
            StreakMode.Occurrences => Strings.HabitBuilder_StreakMode_Occurrences,
            StreakMode.Both => Strings.HabitBuilder_StreakMode_Both,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        var actual = _mode.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        actual.Should().Be(expected);
        actual.Should().NotBe(value.ToString()); // guards against a revert to raw ToString()
    }

    [Theory]
    [InlineData(StreakRecovery.None)]
    [InlineData(StreakRecovery.GraceDay)]
    [InlineData(StreakRecovery.Manual)]
    [InlineData(StreakRecovery.Retroactive)]
    public void StreakRecoveryLabelConverter_localizes_every_value_the_brick_offers(StreakRecovery value)
    {
        var expected = value switch
        {
            StreakRecovery.None => Strings.HabitBuilder_Recovery_None,
            StreakRecovery.GraceDay => Strings.HabitBuilder_Recovery_GraceDay,
            StreakRecovery.Manual => Strings.HabitBuilder_Recovery_Manual,
            StreakRecovery.Retroactive => Strings.HabitBuilder_Recovery_Retroactive,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        var actual = _recovery.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        actual.Should().Be(expected);
    }

    [Fact]
    public void The_two_converters_are_not_swapped()
    {
        // Copy-paste-swap guard: StreakModeLabelConverter must never answer with a Recovery label
        // (or vice versa) for a value the *other* converter would also accept structurally — the
        // compiler already prevents passing the wrong enum type, so this instead pins each converter's
        // output to its own string family via the distinguishable Occurrences/Retroactive labels.
        _mode.Convert(StreakMode.Occurrences, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(Strings.HabitBuilder_StreakMode_Occurrences);
        _recovery.Convert(StreakRecovery.Retroactive, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(Strings.HabitBuilder_Recovery_Retroactive);
    }

    [Fact]
    public void Unrecognized_input_falls_back_to_empty_string()
    {
        _mode.Convert(null, typeof(string), null, CultureInfo.InvariantCulture).Should().Be(string.Empty);
        _mode.Convert("not-an-enum", typeof(string), null, CultureInfo.InvariantCulture).Should().Be(string.Empty);
        _recovery.Convert(null, typeof(string), null, CultureInfo.InvariantCulture).Should().Be(string.Empty);
    }

    [Fact]
    public void ConvertBack_is_not_supported()
    {
        var act1 = () => _mode.ConvertBack(null, typeof(StreakMode), null, CultureInfo.InvariantCulture);
        var act2 = () => _recovery.ConvertBack(null, typeof(StreakRecovery), null, CultureInfo.InvariantCulture);
        act1.Should().Throw<NotSupportedException>();
        act2.Should().Throw<NotSupportedException>();
    }
}
