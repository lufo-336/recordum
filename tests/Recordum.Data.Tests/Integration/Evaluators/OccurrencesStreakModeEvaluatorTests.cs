using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data.Services.Evaluators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Evaluators;

public sealed class OccurrencesStreakModeEvaluatorTests
{
    private readonly IStreakModeEvaluator _eval = new OccurrencesStreakModeEvaluator();
    private static DayContext Ctx(int count, bool scheduled = true) =>
        new(new DateOnly(2026, 5, 1), scheduled, false, 0m, count, null);

    [Fact] public void Mode_is_Occurrences() => Assert.Equal(StreakMode.Occurrences, _eval.Mode);
    [Fact] public void Completed_counts() => Assert.Equal(DayVerdict.Counts, _eval.Evaluate(Ctx(1)));
    [Fact] public void Not_completed_is_neutral_never_misses() => Assert.Equal(DayVerdict.Neutral, _eval.Evaluate(Ctx(0)));
    [Fact] public void Not_scheduled_with_completion_still_counts() => Assert.Equal(DayVerdict.Counts, _eval.Evaluate(Ctx(1, scheduled: false)));
}
