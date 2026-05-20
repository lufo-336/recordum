namespace Recordum.WPF.Tests.Fixtures;

public static class TestScheduler
{
    public static readonly Func<TimeSpan, CancellationToken, Task> InstantDelay =
        (_, _) => Task.CompletedTask;
}
