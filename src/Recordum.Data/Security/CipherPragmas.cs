using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>
/// The only place that spells the statements which set or change a database key (wave D9, probe §1–§2).
/// <para>The provider is SQLite3 Multiple Ciphers, whose default cipher is ChaCha20. Our files are SQLCipher 4, so the
/// format is declared before every key: without it a database made by the old SQLCipher bundle answers error 26, the same
/// signal as a wrong passphrase, and a new one is born in a format the next provider would not read. The format is set
/// with PRAGMAs, never with <c>sqlite3mc_config(...)</c> or any SELECT before the key (probe, trap 4).</para>
/// </summary>
internal static class CipherPragmas
{
    /// <summary>SQLCipher 4, as written by SQLCipher 4.x with its defaults. Changing it is a data migration, not an edit.</summary>
    internal const string Format = "PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4;";

    /// <summary>The format, then the raw 32-byte key. Must be the first statement on the connection.</summary>
    internal static string Key(KeyMaterial key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return $"{Format} PRAGMA key = \"x'{Convert.ToHexString(key.RawKey)}'\";";
    }

    /// <summary>Re-encrypts the open database with <paramref name="raw"/>; an empty key removes the encryption.</summary>
    internal static string Rekey(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return raw.Length == 0 ? "PRAGMA rekey = '';" : $"PRAGMA rekey = \"x'{Convert.ToHexString(raw)}'\";";
    }
}
