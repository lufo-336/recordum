using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Converters;
using Recordum.Presentation.Resources;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class GoalPeriodLabelsTests
{
    [Theory]
    [InlineData(GoalPeriodType.Open)]
    [InlineData(GoalPeriodType.Fixed)]
    [InlineData(GoalPeriodType.Rolling)]
    public void For_never_falls_back_to_the_raw_enum_name(GoalPeriodType type)
    {
        // F0-11: a regression back to type.ToString() would still produce a non-empty string
        // ("Open"/"Fixed"/"Rolling" happen to match some EN labels by coincidence), so assert against
        // the actual localized constants instead of just "not empty".
        var expected = type switch
        {
            GoalPeriodType.Open => Strings.HabitBuilder_Goal_Period_Open,
            GoalPeriodType.Fixed => Strings.HabitBuilder_Goal_Period_Fixed,
            GoalPeriodType.Rolling => Strings.HabitBuilder_Goal_Period_Rolling,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        GoalPeriodLabels.For(type).Should().Be(expected);
    }

    [Fact]
    public void GoalPeriodLabelConverter_delegates_to_GoalPeriodLabels()
    {
        var converter = new GoalPeriodLabelConverter();
        converter.Convert(GoalPeriodType.Rolling, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Strings.HabitBuilder_Goal_Period_Rolling);
    }
}
