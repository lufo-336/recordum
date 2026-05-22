namespace Recordum.Core.Abstractions.Services;

public interface ITrendQueryService
{
    ValueTask<IReadOnlyList<TrendPoint>> GetTrendAsync(
        Guid habitNodeId,
        DateOnly fromDate,
        DateOnly toDate,
        TrendGranularity granularity,
        CancellationToken ct = default);
}

public sealed record TrendPoint(DateOnly Bucket, decimal Value, int Count);

public enum TrendGranularity
{
    Daily,
    Weekly,
}
