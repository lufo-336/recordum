using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeTrendQueryService : ITrendQueryService
{
    public List<TrendPoint> Result { get; } = new();

    public ValueTask<IReadOnlyList<TrendPoint>> GetTrendAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, TrendGranularity granularity, CancellationToken ct = default)
        => new(Result);
}
