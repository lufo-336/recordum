namespace Recordum.Core.Backup;

public interface IBackupScheduler
{
    /// <summary>Fa il backup automatico se e' dovuto, e dice com'e' andata: chi lo prova a mano (Impostazioni) deve
    /// poterlo dire all'utente, non solo "controllo finito" (F2c-8).</summary>
    Task<BackupRunOutcome> RunDueAsync(CancellationToken ct = default);
    DateTime? NextRunUtc { get; }
    bool IsEnabled { get; }
}

public enum BackupRunOutcome
{
    /// <summary>Il backup automatico e' spento.</summary>
    Disabled,
    /// <summary>Acceso, ma non e' ancora il momento.</summary>
    NotDue,
    /// <summary>Il backup e' stato creato.</summary>
    Created,
    /// <summary>Il portachiavi non ha risposto (o e' bloccato): si riprova al prossimo giro, niente si spegne (R11).</summary>
    KeyringUnavailable,
    /// <summary>La passphrase non c'e': il backup automatico si e' spento.</summary>
    SwitchedOff,
}
