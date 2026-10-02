using System.Collections.Immutable;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Tests.Fixtures;

public sealed class FakeHabitNodeService : IHabitNodeService
{
    public List<(Guid Id, Guid? Parent, Guid? Anchor, bool After)> ReorderCalls { get; } = new();
    public ValueTask<Guid> CreateAsync(HabitNode draft, CancellationToken ct = default) => ValueTask.FromResult(Guid.NewGuid());
    public ValueTask UpdateAsync(Guid id, Action<HabitNode> mutate, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask MoveAndReorderAsync(Guid id, Guid? newParentId, Guid? anchorSiblingId, bool placeAfter, CancellationToken ct = default)
    { ReorderCalls.Add((id, newParentId, anchorSiblingId, placeAfter)); return ValueTask.CompletedTask; }
    public ValueTask SoftDeleteAsync(Guid id, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask RestoreAsync(Guid id, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask<HabitNode?> GetAsync(Guid id, CancellationToken ct = default) => ValueTask.FromResult<HabitNode?>(null);
    public ValueTask SetPinScopeAsync(Guid id, PinScope scope, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default) => ValueTask.CompletedTask;
}

public sealed class FakePurgeService : IPurgeService
{
    public ValueTask PurgeSubtreeAsync(Guid id, CancellationToken ct = default) => ValueTask.CompletedTask;
}

public sealed class FakeHabitTreeQueryService : IHabitTreeQueryService
{
    public List<HomeTreeRow> Rows { get; } = new();
    public ValueTask<IReadOnlyList<HomeTreeRow>> GetHomeTreeAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<HomeTreeRow>>(Rows);
    public ValueTask<IReadOnlyList<HomeTreeRow>> GetTrashTreeAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<HomeTreeRow>>(Array.Empty<HomeTreeRow>());
}

public static class RowFixture
{
    public static HomeTreeRow Row(Guid id, string name = "x", string color = "#3B82F6",
        HabitType? type = HabitType.Action, Guid? parent = null, int childCount = 0) =>
        new(id, parent, name, color, type, null, 0, false, PinScope.None, childCount, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    public static HomeRowViewModel Vm(HomeTreeRow row) =>
        new(row, 0, new FakeHabitNodeService(), new FakePurgeService());
}

public static class HomeFixture
{
    public static (HomeViewModel Vm, FakeHabitTreeQueryService Query, FakeHabitNodeService Nodes) Build(
        params HomeTreeRow[] rows)
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.AddRange(rows);
        var nodes = new FakeHabitNodeService();
        var vm = new HomeViewModel(query, nodes, new FakePurgeService());
        return (vm, query, nodes);
    }
}
