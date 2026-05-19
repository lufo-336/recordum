using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;

namespace Recordum.Data.Security;

public sealed partial class KeyTransitionService(
    IDbStoragePaths paths,
    IDbKeyProvider currentProvider,
    ILogger<KeyTransitionService> logger) : IKeyTransitionService
{
    private const int MinPassphraseLength = 12;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Key transition {From} -> {To} completed")]
    private partial void LogTransition(string from, string to);

    public async ValueTask TransitionToPassphraseAsync(string newPassphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassphrase);
        if (newPassphrase.Length < MinPassphraseLength)
            throw new ArgumentException($"passphrase must be >= {MinPassphraseLength} chars", nameof(newPassphrase));

        var backup = paths.DatabasePath + $".bak.{DateTime.UtcNow.Ticks}";
        File.Copy(paths.DatabasePath, backup, overwrite: false);
        try
        {
            var salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            using var currentKey = await currentProvider.GetKeyAsync(ct);
            using var argon = new Argon2id(Encoding.UTF8.GetBytes(newPassphrase))
            {
                Salt = salt,
                DegreeOfParallelism = 2,
                Iterations = 4,
                MemorySize = 65_536,
            };
            var newRaw = await argon.GetBytesAsync(KeyMaterial.LengthBytes);
            using (var conn = SqlCipherConnectionFactory.Open(paths.DatabasePath, currentKey))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA rekey = \"x'{Convert.ToHexString(newRaw)}'\";";
                await cmd.ExecuteNonQueryAsync(ct);
            }
            SqliteConnection.ClearAllPools();
            await File.WriteAllBytesAsync(paths.SaltFilePath, salt, ct);
            if (File.Exists(paths.KeyFilePath)) File.Delete(paths.KeyFilePath);
            File.Delete(backup);
            LogTransition(KeyProviderId.Dpapi, KeyProviderId.Passphrase);
        }
#pragma warning disable CA1031 // broad catch is intentional — any failure must roll back
        catch (Exception ex)
#pragma warning restore CA1031
        {
            SqliteConnection.ClearAllPools();
            File.Copy(backup, paths.DatabasePath, overwrite: true);
            TryDelete(backup);
            if (File.Exists(paths.SaltFilePath)) TryDelete(paths.SaltFilePath);
            throw new KeyTransitionFailedException(KeyProviderId.Dpapi, KeyProviderId.Passphrase, ex);
        }
    }

    [SupportedOSPlatform("windows")]
    public async ValueTask TransitionToDpapiAsync(string currentPassphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassphrase);

        var backup = paths.DatabasePath + $".bak.{DateTime.UtcNow.Ticks}";
        File.Copy(paths.DatabasePath, backup, overwrite: false);
        try
        {
            using var currentKey = await new PassphraseKeyProvider(currentPassphrase, paths.SaltFilePath).GetKeyAsync(ct);
            var fresh = new byte[KeyMaterial.LengthBytes];
            RandomNumberGenerator.Fill(fresh);
            using (var conn = SqlCipherConnectionFactory.Open(paths.DatabasePath, currentKey))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA rekey = \"x'{Convert.ToHexString(fresh)}'\";";
                await cmd.ExecuteNonQueryAsync(ct);
            }
            SqliteConnection.ClearAllPools();
            var protectedBytes = ProtectedData.Protect(fresh, optionalEntropy: null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(paths.KeyFilePath, protectedBytes, ct);
            if (File.Exists(paths.SaltFilePath)) File.Delete(paths.SaltFilePath);
            File.Delete(backup);
            LogTransition(KeyProviderId.Passphrase, KeyProviderId.Dpapi);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            SqliteConnection.ClearAllPools();
            File.Copy(backup, paths.DatabasePath, overwrite: true);
            TryDelete(backup);
            if (File.Exists(paths.KeyFilePath)) TryDelete(paths.KeyFilePath);
            throw new KeyTransitionFailedException(KeyProviderId.Passphrase, KeyProviderId.Dpapi, ex);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
#pragma warning disable CA1031
        catch { /* best-effort cleanup */ }
#pragma warning restore CA1031
    }
}
