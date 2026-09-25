using Recordum.Core.Abstractions.Security;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeAutoBackupKeyStore : IAutoBackupKeyStore
{
    public bool Has { get; set; }
    public string? Written { get; private set; }
    public bool HasKey() => Has;
    public void WriteKey(string passphrase) { Written = passphrase; Has = true; }
    public void DeleteKey() { Has = false; Written = null; }
    public string? ReadKey() => Has ? Written : null;
}
