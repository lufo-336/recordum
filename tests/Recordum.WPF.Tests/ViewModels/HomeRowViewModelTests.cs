using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeRowViewModelTests
{
    private static HomeTreeRow Sample(Guid id, string name = "x", PinScope scope = PinScope.None, bool archived = false, HabitType? type = HabitType.Action) =>
        new(id, null, name, "#000000", type, null, 0, false, scope, 0, archived, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    [Fact]
    public void IsGroup_true_when_HabitType_is_null()
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: null), 0, new FakeHabitNodeService(), new FakePurgeService());
        sut.IsGroup.Should().BeTrue();
    }

    [Fact]
    public async Task SetPinScopeCommand_calls_service()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        var sut = new HomeRowViewModel(Sample(id), 0, fakeService, new FakePurgeService());

        await sut.SetPinScopeCommand.ExecuteAsync(PinScope.Global);

        fakeService.PinScopeCalls.Should().ContainSingle().Which.Should().Be((id, PinScope.Global));
        sut.PinScope.Should().Be(PinScope.Global);
    }

    [Fact]
    public async Task SoftDeleteCommand_calls_service()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        var sut = new HomeRowViewModel(Sample(id), 0, fakeService, new FakePurgeService());

        await sut.SoftDeleteCommand.ExecuteAsync(null);

        fakeService.SoftDeleteCalls.Should().Contain(id);
    }

    [Fact]
    public async Task CommitRename_rolls_back_on_service_failure()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        fakeService.Store[id] = new HabitNode { Id = id, Name = "old" };
        var sut = new HomeRowViewModel(Sample(id, "old"), 0, fakeService, new FakePurgeService());

        sut.BeginRenameCommand.Execute(null);
        sut.PendingName = "new";
        fakeService.NextException = new InvalidOperationException("boom");

        await sut.CommitRenameCommand.ExecuteAsync(null);

        sut.Name.Should().Be("old");
        sut.IsRenaming.Should().BeFalse();
    }

    [Fact]
    public async Task CommitRename_persists_via_UpdateAsync_on_success()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        fakeService.Store[id] = new HabitNode { Id = id, Name = "old" };
        var sut = new HomeRowViewModel(Sample(id, "old"), 0, fakeService, new FakePurgeService());

        sut.BeginRenameCommand.Execute(null);
        sut.PendingName = "new";
        await sut.CommitRenameCommand.ExecuteAsync(null);

        sut.Name.Should().Be("new");
        fakeService.Store[id].Name.Should().Be("new");
        sut.IsRenaming.Should().BeFalse();
    }
}
