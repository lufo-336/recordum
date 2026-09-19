using Recordum.Core.Abstractions;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeApplicationLifetime : IApplicationLifetime
{
    public int ShutdownCount { get; private set; }
    public void Shutdown() => ShutdownCount++;
}
