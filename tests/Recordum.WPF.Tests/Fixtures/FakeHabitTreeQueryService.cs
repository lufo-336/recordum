using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeHabitTreeQueryService : IHabitTreeQueryService
{
    public List<HomeTreeRow> HomeRows { get; } = new();
    public List<HomeTreeRow> TrashRows { get; } = new();
    public int HomeCallCount { get; private set; }
    public int TrashCallCount { get; private set; }
    public Exception? NextException { get; set; }

    /// <summary>When set, GetHomeTreeAsync awaits this before completing, so tests can control the
    /// exact interleaving of the load against other state changes (e.g. AddSeries called first).</summary>
    public TaskCompletionSource<bool>? HomeGate { get; set; }

    public async ValueTask<IReadOnlyList<HomeTreeRow>> GetHomeTreeAsync(CancellationToken ct = default)
    {
        Throw();
        if (HomeGate is not null) await HomeGate.Task.ConfigureAwait(true);
        HomeCallCount++;
        return HomeRows.ToList();
    }

    public ValueTask<IReadOnlyList<HomeTreeRow>> GetTrashTreeAsync(CancellationToken ct = default)
    {
        Throw();
        TrashCallCount++;
        return new ValueTask<IReadOnlyList<HomeTreeRow>>(TrashRows.ToList());
    }

    private void Throw()
    {
        if (NextException is { } e)
        {
            NextException = null;
            throw e;
        }
    }
}
