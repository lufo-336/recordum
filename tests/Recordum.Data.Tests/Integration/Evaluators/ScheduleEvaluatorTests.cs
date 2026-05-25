using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data.Services.Evaluators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Evaluators;

public sealed class ScheduleEvaluatorTests
{
    private readonly IScheduleEvaluator _eval = new ScheduleEvaluator();
    private static readonly DateOnly Created = new(2026, 1, 1);

    [Fact]
    public void Daily_always_scheduled() =>
        Assert.True(_eval.IsScheduled(new DateOnly(2026, 5, 1),
            new ScheduledRules(ScheduleType.Daily, null, null, null, null, Created)));

    [Theory]
    [InlineData("[\"Mon\",\"Wed\",\"Fri\"]", "2026-05-04", true)]   // Mon
    [InlineData("[\"Mon\",\"Wed\",\"Fri\"]", "2026-05-05", false)]  // Tue
    [InlineData("[\"Sat\",\"Sun\"]", "2026-05-09", true)]           // Sat
    public void SpecificDays_respects_mask(string json, string dateStr, bool expected)
    {
        var date = DateOnly.Parse(dateStr);
        Assert.Equal(expected, _eval.IsScheduled(date,
            new ScheduledRules(ScheduleType.SpecificDays, json, null, null, null, Created)));
    }

    [Fact]
    public void WeeklyX_is_always_scheduled_each_day() =>
        Assert.True(_eval.IsScheduled(new DateOnly(2026, 5, 5),
            new ScheduledRules(ScheduleType.WeeklyX, null, 3, null, null, Created)));

    [Theory]
    [InlineData(2, "2026-01-01", true)]  // created date
    [InlineData(2, "2026-01-02", false)]
    [InlineData(2, "2026-01-03", true)]
    [InlineData(3, "2026-01-04", true)]
    public void EveryXDays_modulo(int x, string dateStr, bool expected)
    {
        Assert.Equal(expected, _eval.IsScheduled(DateOnly.Parse(dateStr),
            new ScheduledRules(ScheduleType.EveryXDays, null, null, x, null, Created)));
    }

    [Theory]
    [InlineData("[1,15]", "2026-05-01", true)]
    [InlineData("[1,15]", "2026-05-15", true)]
    [InlineData("[1,15]", "2026-05-02", false)]
    public void Monthly_respects_day_list(string json, string dateStr, bool expected) =>
        Assert.Equal(expected, _eval.IsScheduled(DateOnly.Parse(dateStr),
            new ScheduledRules(ScheduleType.Monthly, null, null, null, json, Created)));

    [Fact]
    public void Custom_treated_as_always_scheduled() =>
        Assert.True(_eval.IsScheduled(new DateOnly(2026, 5, 1),
            new ScheduledRules(ScheduleType.Custom, null, null, null, null, Created)));

    [Fact]
    public void Date_before_created_is_not_scheduled() =>
        Assert.False(_eval.IsScheduled(new DateOnly(2025, 12, 31),
            new ScheduledRules(ScheduleType.Daily, null, null, null, null, Created)));

    [Fact]
    public void SpecificDays_with_null_json_returns_false() =>
        Assert.False(_eval.IsScheduled(new DateOnly(2026, 5, 1),
            new ScheduledRules(ScheduleType.SpecificDays, null, null, null, null, Created)));

    [Fact]
    public void SpecificDays_with_malformed_json_returns_false() =>
        Assert.False(_eval.IsScheduled(new DateOnly(2026, 5, 1),
            new ScheduledRules(ScheduleType.SpecificDays, "{not json}", null, null, null, Created)));

    [Fact]
    public void Monthly_handles_31_in_short_month() =>
        Assert.False(_eval.IsScheduled(new DateOnly(2026, 2, 28),
            new ScheduledRules(ScheduleType.Monthly, null, null, null, "[31]", Created)));
}
