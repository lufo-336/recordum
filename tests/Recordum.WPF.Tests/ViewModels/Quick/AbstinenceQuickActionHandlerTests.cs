using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Quick;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Quick;

public class AbstinenceQuickActionHandlerTests
{
    [Fact]
    public async Task Execute_when_not_abstaining_starts()
    {
        var abstinence = new FakeAbstinenceService(); // no state set → NotStarted
        var sut = new AbstinenceQuickActionHandler(abstinence);
        var habitId = Guid.NewGuid();

        var result = await sut.ExecuteAsync(habitId);

        result.Should().BeTrue();
        var state = await abstinence.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Abstaining);
    }

    [Fact]
    public async Task Execute_when_abstaining_lapses()
    {
        var abstinence = new FakeAbstinenceService();
        var habitId = Guid.NewGuid();
        await abstinence.StartAsync(habitId); // put habit into Abstaining state
        var sut = new AbstinenceQuickActionHandler(abstinence);

        var result = await sut.ExecuteAsync(habitId);

        result.Should().BeTrue();
        var state = await abstinence.GetStateAsync(habitId);
        state.Status.Should().Be(AbstinenceStatus.Relapsed);
    }
}
