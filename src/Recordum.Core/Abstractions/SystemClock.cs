namespace Recordum.Core.Abstractions;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}
