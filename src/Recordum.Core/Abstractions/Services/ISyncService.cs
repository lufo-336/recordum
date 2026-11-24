namespace Recordum.Core.Abstractions.Services;

/// <summary>L'unione fra dispositivi attraverso i file di scambio <c>.rcsync</c> (spec 2026-11-22 "Unisci").</summary>
public interface ISyncService
{
    /// <summary>Un giro: unisce i file degli altri dispositivi e riscrive il proprio se qualcosa è cambiato.</summary>
    Task<SyncRunResult> RunAsync(CancellationToken ct);

    Task<SyncStatus> GetStatusAsync(CancellationToken ct);

    /// <summary>La frase dell'unione: resta nel database cifrato, non viaggia mai.</summary>
    Task SetPassphraseAsync(string passphrase, CancellationToken ct);

    Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct);

    /// <summary>Collega il dispositivo; fa sempre prima un backup locale.</summary>
    Task JoinAsync(SyncJoinChoice choice, CancellationToken ct);

    /// <summary>Smette di unire: dati e ultimo file restano dove sono.</summary>
    Task DisconnectAsync(CancellationToken ct);

    /// <summary>"Apri in chiaro": decifra un <c>.rcsync</c> nello ZIP che sceglie l'utente.</summary>
    Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct);

    /// <summary>"Ricifra": il contrario di <see cref="ExportPlainAsync"/>.</summary>
    Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct);
}

public enum SyncJoinChoice { StartFromOthers, KeepThisDeviceToo }

public enum SyncJoinState { NotConfigured, NeedsChoice, Joined }

public enum SyncPeerProblem { CannotOpen, NewerFormat, BrokenFile, SameIdentity }

public sealed record SyncStatus(
    DateTimeOffset? LastRun,
    IReadOnlyList<SyncPeerStatus> Peers,
    int TextConflicts,
    long OwnFileBytes);

public sealed record SyncPeerStatus(string DeviceName, DateTimeOffset? FileWrittenAt, SyncPeerProblem? Problem);

/// <summary>Esito di un giro: <c>Ran</c> falso se l'unione non è configurata o è scollegata.</summary>
public sealed record SyncRunResult(bool Ran, bool OwnFileWritten, IReadOnlyList<SyncPeerStatus> Peers);

public enum SyncJoinRefusal { MissingConfiguration, NoReadablePeer }

/// <summary><see cref="ISyncService.JoinAsync"/> si rifiuta prima di toccare qualcosa: manca la frase o la cartella, o
/// per "Parti dai dati degli altri" nessun file di un altro dispositivo si apre. Il messaggio è solo per i log: la UI
/// mostra la sua stringa tradotta.</summary>
public sealed class SyncJoinRefusedException(SyncJoinRefusal reason, string message) : Exception(message)
{
    public SyncJoinRefusal Reason { get; } = reason;
}
