namespace Recordum.Data.Sync;

/// <summary>Cosa ha cambiato un'unione: righe inserite, aggiornate e cancellate qui, campi di testo cambiati da tutte e
/// due le parti, e voci rese coerenti dal motore (Task 8).</summary>
public sealed record SyncMergeReport(int Inserted, int Updated, int Deleted, int TextConflicts, int Repaired);
