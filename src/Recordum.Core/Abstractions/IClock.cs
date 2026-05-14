namespace Recordum.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    long UtcNowMs { get; }
}
