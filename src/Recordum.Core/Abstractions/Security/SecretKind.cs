namespace Recordum.Core.Abstractions.Security;

/// <summary>Quale segreto dell'installazione: la chiave del database o la passphrase del backup automatico.</summary>
public enum SecretKind
{
    DbKey,
    AutoBackup,
}
