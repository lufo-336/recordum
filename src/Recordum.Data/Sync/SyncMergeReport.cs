namespace Recordum.Data.Sync;

/// <summary>Cosa ha cambiato un'unione: righe inserite, aggiornate e cancellate qui, campi di testo cambiati da tutte e
/// due le parti, voci rese coerenti dal motore (Task 8), e testi salvati in una nota rapida perché la loro riga è stata
/// cancellata da chi non li aveva visti (non contati in <see cref="Inserted"/>).</summary>
public sealed record SyncMergeReport(int Inserted, int Updated, int Deleted, int TextConflicts, int Repaired, int Rescued = 0);
