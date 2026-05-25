using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data.Services.Evaluators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Evaluators;

public sealed class WeeklyStreakModeEvaluatorTests
{
    private readonly IStreakModeEvaluator _eval = new WeeklyStreakModeEvaluator();
    private static DayContext Ctx(WeeklyPeriodState ws) =>
        new(new DateOnly(2026, 5, 3) /* Sunday */, true, false, 0m, 0, ws);

    [Fact] public void Mode_is_Weekly() => Assert.Equal(StreakMode.Weekly, _eval.Mode);

    [Fact]
    public void EndOfWeek_target_met_counts() =>
        Assert.Equal(DayVerdict.Counts, _eval.Evaluate(Ctx(new WeeklyPeriodState(3, 3, true))));

    [Fact]
    public void EndOfWeek_target_unmet_misses() =>
        Assert.Equal(DayVerdict.Misses, _eval.Evaluate(Ctx(new WeeklyPeriodState(3, 2, true))));

    [Fact]
    public void Mid_week_is_neutral() =>
        Assert.Equal(DayVerdict.Neutral, _eval.Evaluate(Ctx(new WeeklyPeriodState(3, 1, false))));

    [Fact]
    public void Null_week_state_is_neutral() =>
        Assert.Equal(DayVerdict.Neutral, _eval.Evaluate(
            new DayContext(new DateOnly(2026, 5, 3), true, false, 0m, 0, null)));
}
