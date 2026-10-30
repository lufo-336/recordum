using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeGroupHistoryQueryService : IGroupHistoryQueryService
{
    public List<GroupChildHistory> Children { get; } = [];
    public List<(DateOnly From, DateOnly To)> Calls { get; } = [];

    public ValueTask<IReadOnlyList<GroupChildHistory>> GetAsync(
        Guid groupId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        Calls.Add((from, to));
        return new ValueTask<IReadOnlyList<GroupChildHistory>>(Children.ToList());
    }
}
