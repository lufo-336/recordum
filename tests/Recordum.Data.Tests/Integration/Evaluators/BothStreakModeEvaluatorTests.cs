using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data.Services.Evaluators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Evaluators;

public sealed class BothStreakModeEvaluatorTests
{
    private readonly IStreakModeEvaluator _eval = new BothStreakModeEvaluator(
        new DailyStreakModeEvaluator(), new WeeklyStreakModeEvaluator());

    private static DayContext Ctx(bool sched, int count, WeeklyPeriodState? ws) =>
        new(new DateOnly(2026, 5, 3), sched, false, 0m, count, ws);

    [Fact] public void Mode_is_Both() => Assert.Equal(StreakMode.Both, _eval.Mode);

    [Fact]
    public void Both_counting_returns_counts() =>
        Assert.Equal(DayVerdict.Counts, _eval.Evaluate(Ctx(true, 1, new WeeklyPeriodState(3, 3, true))));

    [Fact]
    public void Either_misses_returns_misses() =>
        Assert.Equal(DayVerdict.Misses, _eval.Evaluate(Ctx(true, 0, new WeeklyPeriodState(3, 3, true))));

    [Fact]
    public void Both_neutral_returns_neutral() =>
        Assert.Equal(DayVerdict.Neutral, _eval.Evaluate(Ctx(false, 0, new WeeklyPeriodState(3, 1, false))));

    [Fact]
    public void Daily_counts_weekly_neutral_yields_counts() =>
        Assert.Equal(DayVerdict.Counts, _eval.Evaluate(Ctx(true, 1, new WeeklyPeriodState(3, 1, false))));
}
