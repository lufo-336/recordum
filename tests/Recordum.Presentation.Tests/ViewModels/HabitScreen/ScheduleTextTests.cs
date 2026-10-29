using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public sealed class ScheduleTextTests : IDisposable
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;

    public ScheduleTextTests() => CultureInfo.CurrentUICulture = It;
    public void Dispose() => CultureInfo.CurrentUICulture = _ui;

    [Theory]
    [InlineData(ScheduleType.Daily, null, null, null, "Ogni giorno")]
    [InlineData(ScheduleType.SpecificDays, "[\"Mon\",\"Wed\",\"Fri\"]", null, null, "lun, mer, ven")]
    [InlineData(ScheduleType.WeeklyX, null, 3, null, "3 volte a settimana")]
    [InlineData(ScheduleType.EveryXDays, null, null, 2, "Ogni 2 giorni")]
    [InlineData(ScheduleType.Monthly, null, null, null, "Ogni mese")]
    [InlineData(ScheduleType.Custom, null, null, null, "")]
    public void Describe(ScheduleType type, string? days, int? weekly, int? everyX, string expected)
        => ScheduleText.Describe(new HabitConfig
        {
            ScheduleType = type, ScheduleDaysJson = days, ScheduleWeeklyTarget = weekly, ScheduleEveryXDays = everyX,
        }, It).Should().Be(expected);

    [Fact]
    public void No_config_is_every_day() => ScheduleText.Describe(null, It).Should().Be("Ogni giorno");

    [Fact]
    public void Days_follow_the_week_order_from_monday()
        => ScheduleText.Describe(new HabitConfig { ScheduleType = ScheduleType.SpecificDays, ScheduleDaysJson = "[\"Sun\",\"Mon\"]" }, It)
            .Should().Be("lun, dom");
}
