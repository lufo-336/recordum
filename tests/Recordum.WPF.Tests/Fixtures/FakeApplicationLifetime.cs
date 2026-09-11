using Recordum.Core.Abstractions;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeApplicationLifetime : IApplicationLifetime
{
    public int ShutdownCount { get; private set; }
    public void Shutdown() => ShutdownCount++;
}
