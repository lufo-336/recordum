using Recordum.Core.Abstractions;

namespace Recordum.Data.Tests.Fixtures;

public sealed class FakeClock : IClock
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset? start = null)
        => _now = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => _now;

    public long UtcNowMs => _now.ToUnixTimeMilliseconds();

    public void Set(DateTimeOffset t) => _now = t;

    public void Advance(TimeSpan d) => _now = _now.Add(d);
}
