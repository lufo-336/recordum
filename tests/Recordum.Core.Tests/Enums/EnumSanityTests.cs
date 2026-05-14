using Recordum.Core.Enums;
using Xunit;

namespace Recordum.Core.Tests.Enums;

public class EnumSanityTests
{
    [Theory]
    [InlineData(typeof(NodeType), 2)]
    [InlineData(typeof(HabitType), 5)]
    [InlineData(typeof(ScheduleType), 6)]
    [InlineData(typeof(StreakMode), 4)]
    [InlineData(typeof(StreakRecovery), 4)]
    [InlineData(typeof(TimerMode), 4)]
    [InlineData(typeof(MetricType), 4)]
    [InlineData(typeof(GoalPeriodType), 3)]
    [InlineData(typeof(NotifyType), 3)]
    [InlineData(typeof(AiDataSharing), 3)]
    [InlineData(typeof(AttachmentParentKind), 2)]
    public void Enum_has_expected_value_count(Type enumType, int expected)
    {
        Assert.Equal(expected, Enum.GetValues(enumType).Length);
    }
}
