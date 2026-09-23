namespace Recordum.Core.Abstractions.Security;

/// <summary>In che stato è la chiave del database su questo sistema, cioè che cosa la shell deve
/// chiedere all'utente prima di poter aprire il file.</summary>
public enum DbUnlockState
{
    /// <summary>Nessun database (o un salt orfano senza database): serve una passphrase NUOVA, con conferma.</summary>
    NeedsCreation,

    /// <summary>Il database esiste ed è cifrato con una passphrase (esiste <c>db.salt</c>): serve la passphrase.</summary>
    NeedsPassphrase,

    /// <summary>La chiave la custodisce il sistema (DPAPI su Windows): nessuna interazione.</summary>
    UsePlatformKey,

    /// <summary>Il database è stato creato con una chiave di piattaforma che questo sistema non ha
    /// (un <c>db.key</c> DPAPI su Linux): l'unico ponte è ripristinare un backup <c>.recbak</c>.</summary>
    Unsupported,
}

public enum UnlockOutcome
{
    Unlocked,
    WrongPassphrase,
    PassphraseTooShort,
    Failed,
}

/// <summary><paramref name="Provider"/> è valorizzato solo con <see cref="UnlockOutcome.Unlocked"/>.</summary>
public sealed record UnlockResult(UnlockOutcome Outcome, IDbKeyProvider? Provider);

/// <summary>Lo sblocco del database prima che l'app si avvii. Vive in Core (non in Presentation)
/// perché l'implementazione sta in Data, che non può referenziare Presentation, e il risultato porta
/// un <see cref="IDbKeyProvider"/>, che è già Core.</summary>
public interface IDbUnlockService
{
    /// <summary>Guarda i file su disco (nessuna apertura del database, nessuna derivazione della chiave).</summary>
    DbUnlockState DetectState();

    /// <summary>Tenta lo sblocco. <paramref name="passphrase"/> è ignorata per
    /// <see cref="DbUnlockState.UsePlatformKey"/> e <see cref="DbUnlockState.Unsupported"/>.</summary>
    Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, CancellationToken ct = default);
}
