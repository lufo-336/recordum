using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakePurgeService : IPurgeService
{
    private readonly List<Guid> _purgeCalls = new();

    // IPurgeService is currently an empty marker interface.
    // PurgeCalls is here for forward compatibility: once IPurgeService gains members, record into _purgeCalls.
    public IReadOnlyList<Guid> PurgeCalls => _purgeCalls;
}
