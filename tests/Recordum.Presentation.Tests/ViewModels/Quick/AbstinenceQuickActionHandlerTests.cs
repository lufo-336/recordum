using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Quick;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Quick;

public class AbstinenceQuickActionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private static AbstinenceQuickActionHandler Create(FakeAbstinenceService abstinence)
        => new(abstinence, new FakeClock(Now), new FakeResetTimeResolver());

    [Fact]
    public async Task Home_quick_action_starts_only_today()
    {
        var abstinence = new FakeAbstinenceService(); // NotStarted
        var sut = Create(abstinence);
        var habitId = Guid.NewGuid();

        // A past day: nothing is started, the screen opens instead.
        (await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 30))).Should().BeFalse();
        (await abstinence.GetStateAsync(habitId)).Status.Should().Be(AbstinenceStatus.NotStarted);

        // null = today, and the explicit day of today.
        (await sut.ExecuteAsync(habitId, null)).Should().BeTrue();
        (await abstinence.GetStateAsync(habitId)).Status.Should().Be(AbstinenceStatus.Abstaining);

        var other = Guid.NewGuid();
        (await sut.ExecuteAsync(other, new DateOnly(2026, 6, 1))).Should().BeTrue();
        (await abstinence.GetStateAsync(other)).Status.Should().Be(AbstinenceStatus.Abstaining);
    }

    [Fact]
    public async Task Home_quick_action_never_lapses()
    {
        var abstinence = new FakeAbstinenceService();
        var habitId = Guid.NewGuid();
        await abstinence.StartAsync(habitId); // Abstaining
        var sut = Create(abstinence);

        (await sut.ExecuteAsync(habitId, null)).Should().BeFalse();
        (await sut.ExecuteAsync(habitId, new DateOnly(2026, 6, 1))).Should().BeFalse();
        (await sut.ExecuteAsync(habitId, new DateOnly(2026, 5, 30))).Should().BeFalse();

        abstinence.LapseDays.Should().BeEmpty();
        (await abstinence.GetStateAsync(habitId)).Status.Should().Be(AbstinenceStatus.Abstaining);
    }
}
