using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeTrendQueryService : ITrendQueryService
{
    public List<TrendPoint> Result { get; } = new();
    public TrendGranularity LastGranularity { get; private set; }

    public ValueTask<IReadOnlyList<TrendPoint>> GetTrendAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, TrendGranularity granularity, CancellationToken ct = default)
    {
        LastGranularity = granularity;
        return new(Result);
    }
}
