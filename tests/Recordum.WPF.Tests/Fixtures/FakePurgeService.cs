using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakePurgeService : IPurgeService
{
    private readonly List<Guid> _purgeCalls = new();
    public IReadOnlyList<Guid> PurgeCalls => _purgeCalls;

    public ValueTask PurgeSubtreeAsync(Guid id, CancellationToken ct = default)
    {
        _purgeCalls.Add(id);
        return ValueTask.CompletedTask;
    }
}
