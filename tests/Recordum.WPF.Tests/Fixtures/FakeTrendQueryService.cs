using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeTrendQueryService : ITrendQueryService
{
    public List<TrendPoint> Result { get; } = new();
    public TrendGranularity LastGranularity { get; private set; }

    /// <summary>La finestra dell'ultima interrogazione. I punti tornati sono sempre gli stessi: ciò
    /// che si può provare — e che R9 deve garantire — è che il chiamante abbia chiesto i giorni giusti.</summary>
    public DateOnly? LastFrom { get; private set; }
    public DateOnly? LastTo { get; private set; }

    public ValueTask<IReadOnlyList<TrendPoint>> GetTrendAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, TrendGranularity granularity, CancellationToken ct = default)
    {
        LastGranularity = granularity;
        LastFrom = fromDate;
        LastTo = toDate;
        return new(Result);
    }
}
