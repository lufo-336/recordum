using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

/// <summary>
/// Parameterized check that <see cref="SectionViewModelBase.IsApplicable"/> across the
/// 11 sections matches the matrix in milestone-3.4 spec §6.
/// </summary>
public class SectionVisibilityMatrixTests
{
    public static IEnumerable<object?[]> Cases() => new[]
    {
        new object?[] { NodeType.Group, (HabitType?)null,            "L1,Goal,Notifications,Tags,AiSharing,ResetTime" },
        new object?[] { NodeType.Habit, (HabitType?)HabitType.Action,    "L1,Scheduling,Streak,Goal,Notifications,Tags,HomeRow,AiSharing,DataSource,ResetTime" },
        new object?[] { NodeType.Habit, (HabitType?)HabitType.Counter,   "L1,Scheduling,Streak,Timer,Metrics,Goal,Notifications,Tags,HomeRow,AiSharing,DataSource,ResetTime" },
        new object?[] { NodeType.Habit, (HabitType?)HabitType.Resistance,"L1,Timer,Metrics,Goal,Notifications,Tags,HomeRow,AiSharing,DataSource,ResetTime" },
        new object?[] { NodeType.Habit, (HabitType?)HabitType.Journal,   "L1,Scheduling,Streak,Goal,Notifications,Tags,HomeRow,AiSharing,ResetTime" },
        new object?[] { NodeType.Habit, (HabitType?)HabitType.State,     "L1,Scheduling,Streak,Scales,Goal,Notifications,Tags,HomeRow,AiSharing,DataSource,ResetTime" },
    };

    [Theory, MemberData(nameof(Cases))]
    public void Matrix_matches_spec(NodeType node, HabitType? habit, string expectedSections)
    {
        var fakeSettings = new FakeAppSettingService();
        var sut = new HabitEditorViewModel(
            new FakeHabitNodeService(),
            new FakeHabitConfigService(),
            new FakeGoalService(),
            new FakeMetricDefinitionService(),
            new FakeNotificationService(),
            new FakeTagService(),
            fakeSettings,
            new FakeReferenceData(),
            new RecentColorStore(fakeSettings),
            new FavoriteColorStore(fakeSettings),
            new FakeScreenColorPicker(),
            new FakeHabitUiPrefsService());

        var applicable = new List<string> { "L1" };
        if (sut.Scheduling.IsApplicable(node, habit)) applicable.Add("Scheduling");
        if (sut.Streak.IsApplicable(node, habit)) applicable.Add("Streak");
        if (sut.Timer.IsApplicable(node, habit)) applicable.Add("Timer");
        if (sut.Metrics.IsApplicable(node, habit)) applicable.Add("Metrics");
        if (sut.Scales.IsApplicable(node, habit)) applicable.Add("Scales");
        if (sut.Goal.IsApplicable(node, habit)) applicable.Add("Goal");
        if (sut.Notifications.IsApplicable(node, habit)) applicable.Add("Notifications");
        if (sut.Tags.IsApplicable(node, habit)) applicable.Add("Tags");
        if (sut.HomeRow.IsApplicable(node, habit)) applicable.Add("HomeRow");
        if (sut.AiSharing.IsApplicable(node, habit)) applicable.Add("AiSharing");
        if (sut.DataSource.IsApplicable(node, habit)) applicable.Add("DataSource");
        if (sut.ResetTime.IsApplicable(node, habit)) applicable.Add("ResetTime");

        string.Join(",", applicable).Should().Be(expectedSections);
    }
}
