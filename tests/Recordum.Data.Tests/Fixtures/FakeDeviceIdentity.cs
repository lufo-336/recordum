using Recordum.Core.Abstractions;

namespace Recordum.Data.Tests.Fixtures;

public sealed class FakeDeviceIdentity : IDeviceIdentity
{
    public string? DeviceName { get; set; }
}
