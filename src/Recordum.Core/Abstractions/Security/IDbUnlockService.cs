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

/// <summary>Che cosa ha fatto <see cref="IDbUnlockService.ReconcileOrphans"/>.</summary>
public enum OrphanReconciliation
{
    /// <summary>Niente da sistemare.</summary>
    None,

    /// <summary>Mancava il database ma c'era <c>recordum.db.old</c>, lasciato da un ripristino interrotto fra i
    /// due spostamenti: è tornato al suo posto.</summary>
    RecoveredInterruptedRestore,

    /// <summary>C'era un salt senza database e nessun <c>.old</c>: il salt è stato spostato di lato
    /// (<c>db.salt.orphan-…</c>), mai cancellato, e la creazione riparte da zero.</summary>
    SaltSetAside,
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
    /// <summary>Da chiamare <b>prima</b> di <see cref="DetectState"/>: sistema un salt rimasto senza database
    /// (decisione A del Passo 0, 2026-09-25). Non distrugge mai niente — o rimette al suo posto il database di un
    /// ripristino interrotto, o sposta di lato il salt. Senza questo passo la creazione riscriverebbe il salt, e un
    /// database rimasto altrove (un <c>.old</c>) diventerebbe illeggibile per sempre.</summary>
    OrphanReconciliation ReconcileOrphans();

    /// <summary>Guarda i file su disco (nessuna apertura del database, nessuna derivazione della chiave).</summary>
    DbUnlockState DetectState();

    /// <summary>Tenta lo sblocco. <paramref name="passphrase"/> è ignorata per
    /// <see cref="DbUnlockState.UsePlatformKey"/> e <see cref="DbUnlockState.Unsupported"/>.</summary>
    Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, CancellationToken ct = default);
}
