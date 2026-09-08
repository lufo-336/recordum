using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeStreakAggregator : IStreakAggregator
{
    public List<StreakRun> Runs { get; } = new();

    /// <summary>L intervallo dell ultima chiamata. Le Runs tornano sempre uguali, quindi e questo il
    /// solo modo di provare che il chiamante ha chiesto la finestra del giorno GIUSTO (R9).</summary>
    public (DateOnly From, DateOnly To)? LastRange { get; private set; }

    public ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        LastRange = (fromDate, toDate);
        return new(Runs);
    }
}
