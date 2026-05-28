namespace Recordum.Core.Abstractions.Security;

public interface IBackupCipher
{
    Task EncryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default);
    Task DecryptAsync(Stream input, Stream output, string passphrase, CancellationToken ct = default);
}
