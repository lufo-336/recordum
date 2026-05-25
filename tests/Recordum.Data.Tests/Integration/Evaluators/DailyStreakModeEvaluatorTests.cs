using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data.Services.Evaluators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Evaluators;

public sealed class DailyStreakModeEvaluatorTests
{
    private readonly IStreakModeEvaluator _eval = new DailyStreakModeEvaluator();

    private static DayContext Ctx(bool scheduled, int count) =>
        new(new DateOnly(2026, 5, 1), scheduled, false, 0m, count, null);

    [Fact] public void Mode_is_Daily() => Assert.Equal(StreakMode.Daily, _eval.Mode);
    [Fact] public void Scheduled_and_completed_counts() => Assert.Equal(DayVerdict.Counts, _eval.Evaluate(Ctx(true, 1)));
    [Fact] public void Scheduled_and_missing_misses() => Assert.Equal(DayVerdict.Misses, _eval.Evaluate(Ctx(true, 0)));
    [Fact] public void Not_scheduled_is_neutral() => Assert.Equal(DayVerdict.Neutral, _eval.Evaluate(Ctx(false, 0)));
    [Fact] public void Not_scheduled_with_completion_still_neutral() => Assert.Equal(DayVerdict.Neutral, _eval.Evaluate(Ctx(false, 1)));
}
