# Sonda: file su Google Drive dal telefono con il selettore di sistema (2026-11-22)

**Domanda**: per l'"unisci", il telefono può leggere e scrivere i due file di scambio direttamente su Google Drive
tramite il selettore di sistema di Android (Storage Access Framework), senza DriveSync e senza integrare l'API di Drive?

**Come**: mini-app .NET Android usa e getta (fuori dal repo, `~/dev/_spike-saf-drive`), Galaxy S21 dell'autore,
Android 15, app Google Drive installata. File del computer caricato e aggiornato da Fedora con `rclone`.
`ACTION_CREATE_DOCUMENT` / `ACTION_OPEN_DOCUMENT` + `TakePersistableUriPermission(read|write)`.

| Prova | Esito |
|---|---|
| Creare il file del telefono in una cartella di Drive | ✅ |
| Aprire un file esistente su Drive in lettura **e scrittura** | ✅ permesso persistente rw concesso |
| Scrivere 195 KB | ✅ 40–60 ms (scrive su una copia locale) |
| Il file scritto arriva su Drive | ✅ ≈ 3 s dopo, con la rete accesa |
| Una modifica fatta dal PC (stesso file, stesso id) si vede sul telefono | ✅ entro ≈ 40 s; prima legge la copia in cache |
| Permessi dopo reinstallazione dell'app (update) e dopo riavvio del telefono | ✅ restano tutti e due |
| Scrivere e leggere in modalità aereo | ✅ nessun errore (copia locale) |
| Il file scritto senza rete parte quando torna la rete | ❌ fermo 9 minuti con la rete accesa; caricato solo all'apertura dell'app Drive |

**Conclusioni**
- Sul telefono il selettore di sistema basta per i due file: niente login a Google dentro Recordum e niente DriveSync.
  Lo stesso selettore mostra anche le cartelle locali (quindi anche una cartella DriveSync) e altri provider.
- Limite: Recordum non sa se il suo file è già stato caricato. Una scrittura fatta offline può restare in coda
  nell'app Drive a lungo. Siccome ogni file contiene lo stato intero del dispositivo, un caricamento in ritardo
  ritarda l'unione e non perde dati.
- La lettura del file dell'altro può essere vecchia di qualche decina di secondi (cache di Drive): l'unione deve
  essere idempotente e tollerare un file non aggiornato.
