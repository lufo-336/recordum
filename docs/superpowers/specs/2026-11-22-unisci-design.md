# "Unisci" — sincronizzazione fra dispositivi attraverso un file di scambio

> Spec del passo 6 della ROADMAP. Conversazione con l'autore del 2026-11-19/22 (sessione `unisci-spec`).
> Sonda collegata: `docs/design/2026-11-22-saf-drive-probe.md`. Decisioni del 2026-11-10:
> `2026-11-10-v1-ux-review-and-path-design.md` §6.

## 1. Perché

Il telefono diventa il posto dove si registra ogni giorno; i computer restano lo studio (grafici, diario lungo). Tutti
scrivono. L'autore usa **più computer** (Fedora, Windows, Mac) e un telefono Android. Ogni dispositivo tiene il proprio
database locale; nessun server nostro (principio 1). Il cloud è quello dell'utente (Google Drive per l'autore).

**Criterio della v1.0: niente si perde e niente di cancellato ritorna.**

Desiderio esplicito dell'autore sul formato: **compatibilità a lungo termine**, un file che possa essere **letto e
interpretato da altri programmi** e **modificato dagli utenti**. Non la strada più corta: quella migliore.

## 2. Obiettivi, non obiettivi, anti-obiettivi

**Obiettivi**
- N dispositivi (telefono + più computer) convergono allo stesso stato, senza perdere dati.
- Unione automatica, con controlli e spiegazioni in Impostazioni.
- Formato di scambio pubblico, documentato, versionato, indipendente dallo schema interno.
- Foto comprese nello stesso file.

**Non obiettivi (v1.0)**
- Collegamento diretto all'API di Google Drive (dopo la v1.0, se l'invio in ritardo osservato nella sonda dà fastidio).
- Unione automatica del testo riga per riga.
- Collaborazione fra persone diverse: un utente, i suoi dispositivi.
- "Esporta tutto / Importa" come funzioni separate: il formato le permette, si fanno dopo (§9).

**Anti-obiettivi** (fallimento anche se "funziona")
- Una registrazione o una voce che sparisce senza che l'utente l'abbia cancellata.
- Una cosa cancellata che ricompare.
- Testo del diario scartato in silenzio per risolvere un conflitto.
- Dati in chiaro fuori dal dispositivo (il file di scambio è sempre cifrato).
- L'utente che deve capire la sincronizzazione per usare l'app.

## 3. Vincoli

- Principi di `CLAUDE.md` §2: offline-first, nessun account, privacy, English-first nelle stringhe.
- Il database vivo **non** sta nella cartella sincronizzata (decisione 2026-11-10).
- Sul telefono non esiste una cartella di Drive: con il selettore di sistema (SAF) si scelgono **file singoli**; una
  cartella di Drive non è affidabile. Recordum non può creare file da sola su Drive dal telefono (serve il selettore).
- Una scrittura fatta offline sul telefono può restare in coda nell'app Drive per molti minuti (sonda: 9 min); la
  lettura del file dell'altro può essere vecchia di decine di secondi. L'unione deve tollerare file vecchi e non
  dipendere dai tempi.
- Su Windows la chiave del database viene da DPAPI e gli altri dispositivi non la conoscono: serve un segreto comune.
- Le foto sono BLOB dentro il database (`attachment.content`/`thumbnail`, schema 2.19), ridimensionate a 2048 px (D10).
- **58 chiamate** `ExecuteDelete`/`ExecuteUpdate`/SQL diretto nei servizi e le cascade di SQLite **scavalcano**
  `TimestampInterceptor`: ore di modifica e cancellazioni non si possono catturare solo lì.
- La sincronizzazione arriva dopo la testa Android (ROADMAP passi 4–5): la parte telefono si innesta lì.

## 4. Strade considerate

| | Strada | Pro | Contro | Esito |
|---|---|---|---|---|
| A | Il file di scambio è una copia cifrata del database (`VACUUM INTO`), unita tabella per tabella | Riusa il backup; ogni colonna viaggia da sola | È lo schema interno: chi legge da fuori insegue le nostre migrazioni; dispositivi con schema diverso non uniscono | Scartata |
| **B** | **Formato pubblico: ZIP con JSON Lines per entità + foto, cifrato come i `.recbak`** | **Contratto stabile e documentato, separato dallo schema; leggibile e modificabile; tollera versioni diverse; base per export/import** | Più lavoro (mappatura per ogni tabella); rischio della colonna dimenticata, chiuso da un test di guardia | **Scelta dall'autore** |
| C | Registro delle singole modifiche, file nuovi aggiunti uno dopo l'altro | Niente riscritture | Tocca tutti i servizi; dal telefono senza API di Drive non si creano file | Scartata |

Scartati anche: **un unico file condiviso da tutti** (con i ritardi di Drive due dispositivi si sovrascrivono);
**la cartella di Drive scelta dal telefono** (non affidabile, §3).

## 5. Decisioni (dell'autore, come le ha prese)

1. **Trasporto**: sul telefono due o più file scelti dal selettore di sistema; sui computer una cartella. L'app non
   parla con Google. Chi usa DriveSync o simili sul telefono può scegliere una cartella locale.
2. **Quando**: unione **automatica**; in Impostazioni i controlli per l'utente, **ognuno con una riga di spiegazione**.
3. **Più computer**: ogni dispositivo scrive lo **stato intero già unito**; le modifiche passano anche attraverso un
   intermediario. Il telefono sceglie il proprio file e almeno il file di un computer, e può aggiungerne altri.
4. **Conflitti**:
   - numeri, spunte, impostazioni dell'abitudine: **vince la modifica più recente, campo per campo**;
   - **diario e note: si tengono tutte e due le versioni** (una resta, l'altra diventa una copia segnata
     "modificata anche su <dispositivo>"); l'app avvisa, l'utente unisce a mano.
5. **Foto nello stesso file** del resto; in Impostazioni l'utente può escluderle ("Unisci anche le foto").
6. **Formato B** (§6).

## 6. Il formato di scambio

### 6.1 Contenitore
- Un file per dispositivo, estensione `.rcsync`. Nome suggerito: `recordum-<nome-dispositivo>-<device_id 8 car.>.rcsync`.
- **Fuori**: la busta dei `.recbak` (Argon2id + AES-256-GCM, `AesGcmBackupCipher`) con **magic distinto** (`RCSY`),
  così un file di scambio non viene mai preso per un backup e viceversa. Un file troncato o a metà caricamento non
  supera il tag GCM: si salta e si riprova più tardi, senza errori a schermo.
- **Dentro**: un archivio ZIP.

```
manifest.json
data/habit_node.jsonl
data/completion.jsonl
data/…                      un file per entità che viaggia
tombstones.jsonl
photos/<attachment_id>.jpg       (o .png, come salvato)
photos/<attachment_id>.thumb.jpg
```

### 6.2 manifest.json
`format` (`"recordum-sync"`), `format_version` (`"1.0"`, maggiore.minore), `device_id`, `device_name`, `app_version`,
`written_at_ms`, `includes_photos` (bool), `entities` (elenco dei file presenti con conteggio righe).

### 6.3 Righe
- Una riga JSON per elemento. Nomi dei campi in inglese `snake_case` (come l'export JSON di oggi), date ISO 8601,
  booleani veri, enum come stringhe, GUID come stringhe.
- Ogni riga porta `id` e `updated_at_ms`. Le relazioni per id (`parent_id`, `habit_id`, …).
- **Il formato non è lo schema**: le colonne interne si mappano su nomi pubblici stabili; rinominare una colonna non
  cambia il formato.

### 6.4 Compatibilità
- Versione **minore** in più = campi o entità aggiunti. Un lettore più vecchio ignora campi ed entità sconosciuti.
- **Un campo assente non vuol dire null né cancellato**: si confronta campo per campo solo ciò che c'è.
- Versione **maggiore** diversa: l'unione con quel file si ferma; lo stato dice "Aggiorna Recordum su <dispositivo>".
- Il documento `docs/formats/sync-file.md` descrive tutto (busta, ZIP, manifest, ogni entità e campo, regole
  d'unione) abbastanza perché un altro programma lo legga e lo scriva.

### 6.5 Leggibilità per l'utente
Comando in Impostazioni (solo computer, v1.0): **"Apri il file di scambio in chiaro"** → chiede la frase, scrive lo ZIP
dove sceglie l'utente; e il contrario, **"Ricifra"**. Lo ZIP in chiaro va solo dove l'utente lo mette, per sua scelta
esplicita; i file temporanei dell'unione restano in `TempDirectory` (`SystemTempGuardTests`).

## 7. Cosa viaggia

**Viaggiano** (dati dell'utente): `habit_node`, `habit_config`, `habit_config_history`, `unit_of_measure`,
`scale_template`, `scale_anchor`, `metric_definition`, `segment_definition`, `completion`, `completion_value`,
`completion_segment`, `streak_pause`, `abstinence_period`, `note`, `note_history`, `attachment` (+ foto), `goal`,
`tag`, `habit_node_tag`, `note_tag`, `habit_template`, `export_template`, `chart`, `chart_series`, `notification`.
I tag ricavati e le righe di `scale_anchors` viaggiano coi dati (ROADMAP).

**Non viaggiano** (stato del dispositivo): `active_timer` (il timer vive sul dispositivo dove è partito; la
registrazione che produce viaggia — *mia scelta*), `backup_log`, `export_job`, `ai_insight` (*mia scelta*: prodotti
locali, rigenerabili), `sync_meta`.

**`app_setting`**: viaggia solo un elenco esplicito di chiavi di dati — `daily_reset_time` (cambia il giorno delle
registrazioni, deve essere uguale ovunque), `monthly_summary_day`, `monthly_summary_time`. Restano per dispositivo:
`language`, `theme`, `home.rows`, `onboarding_complete`, `ai_*`, `notifications_enabled`, `app_lock_*`,
`inactivity_timeout_minutes`, `backup.auto.*`, `schema_version` e tutte le chiavi `sync.*`. Una chiave nuova senza
classificazione rompe il test di guardia (§8.6).

`created_by_plugin_id` viaggia con le entità top-level. I modelli predefiniti hanno id fissi: si uniscono come gli altri.

## 8. Come si unisce

### 8.1 Base nel database (migrazione)
- `updated_at_ms` su **tutte** le tabelle che viaggiano (oggi solo 11: mancano registrazioni, valori, segmenti,
  obiettivi, tag, collegamenti, storici…). Backfill: dai `updated_at`/`created_at` esistenti.
- Tabella `sync_tombstone` (`entity`, `entity_id`, `deleted_at_ms`, `device_id`), PK (`entity`, `entity_id`).
- **Cattura a livello di database** (*mia scelta*, per le 58 scritture che scavalcano l'interceptor e per le cascade):
  - trigger `AFTER DELETE` su ogni tabella che viaggia → riga in `sync_tombstone`;
  - trigger `AFTER UPDATE` che aggiorna `updated_at_ms` quando chi scrive non l'ha cambiato
    (`WHEN NEW.updated_at_ms = OLD.updated_at_ms`), così l'unione che scrive l'ora remota la conserva;
  - l'ora viene da una funzione SQLite registrata su ogni connessione (accanto alla chiave, nello stesso
    interceptor) che legge `IClock` — mai l'orologio di SQLite (regola "mai `DateTime.UtcNow`", test deterministici).
- `TimestampInterceptor` continua a stampare le scritture EF; i trigger coprono il resto.
- Le lapidi si tengono per sempre nella v1.0 (*mia scelta*: righe minuscole; potatura quando servirà, con un
  `ponytail:`).

### 8.2 Un giro d'unione
1. Lettura dei file degli altri (computer: tutti i `.rcsync` nella cartella tranne il proprio; telefono: i file scelti).
   Un file che non si decifra o con `format_version` maggiore diverso si salta, e lo stato lo dice.
2. Per ogni file, in **una transazione**: per ogni entità e riga, confronto con la riga locale e con le lapidi.
3. Scrittura del proprio file con lo stato intero unito (atomica sui computer: file temporaneo nella stessa cartella e
   rinomina; sul telefono sovrascrittura `"wt"` del documento SAF).
L'unione è **idempotente** e **commutativa**: rileggere lo stesso file o leggere i file in un altro ordine porta allo
stesso stato.

### 8.3 Regole
- **Riga nuova** (id sconosciuto, nessuna lapide più recente): si inserisce. Le righe figlie arrivano dopo le madri
  (ordine delle entità per dipendenza).
- **Stessa riga su tutti e due**: campo per campo vince l'`updated_at_ms` più recente della riga; a parità vince il
  `device_id` minore (deterministico). *Nota per chi costruisce*: con un solo `updated_at_ms` per riga il "campo per
  campo" si ottiene confrontando i valori con la **base** (§8.4): un campo cambiato da una parte sola vince comunque.
- **Cancellazione contro modifica**: vince l'azione più recente (`deleted_at_ms` contro `updated_at_ms`). Se vince la
  cancellazione ma dall'altra parte c'è **testo di diario o nota** che il cancellato non aveva, quel testo si salva
  come copia (regola B).
- **Cancellazione di una madre** con figlie modificate altrove dopo: se la madre resta cancellata, le figlie seguono
  (come la cascade locale). Per le abitudini il "cancellato" è il cestino (soft delete): il ripristino è una modifica
  più recente e vince.
- **Diario e note (B)**: se il testo è cambiato da **tutte e due** le parti rispetto alla base, la versione locale resta
  e quella remota diventa una **nota copia** collegata alla stessa abitudine e allo stesso giorno, segnata "modificata
  anche su <dispositivo>" (campo o tag di sistema: scelta di chi costruisce). Lo stato conta le copie da sistemare.
  Se è cambiato da una parte sola, vince quella senza conflitto.
- `HabitConfigHistory` e `NoteHistory` sono storici: si uniscono come insiemi (righe nuove si aggiungono).

### 8.4 La base
Per riconoscere "cambiato da tutte e due le parti" serve sapere com'era la riga all'ultima unione con quel
dispositivo. Tabella locale `sync_base` (non viaggia): per (`peer_device_id`, `entity`, `entity_id`) l'hash della riga
o dei campi rilevanti all'ultima unione. Senza base (prima unione con quel dispositivo) le differenze di testo si
trattano come conflitto (nessun testo scartato).

### 8.5 Il segreto: la frase dell'unione
- Scelta sul primo dispositivo, scritta una volta su ogni altro. Conservata **dentro il database cifrato** (chiave
  `sync.passphrase`, mai nel file di scambio, mai fuori).
- Chiave derivata con Argon2id e salt nella busta; la chiave derivata si tiene in memoria per salt (lo sblocco costa
  ≈ 1 s sul telefono: non a ogni giro).
- Persa la frase: se ne sceglie una nuova e si riscrive su tutti; i dati restano, ogni dispositivo ha i suoi.

### 8.6 Test di guardia
- **Ogni colonna di ogni tabella che viaggia** è mappata nel formato o esclusa esplicitamente con una ragione; ogni
  tabella del modello EF è classificata (viaggia / non viaggia); ogni chiave di `app_setting` è classificata. Una
  colonna, tabella o chiave nuova non classificata = build rossa (riflessione sul modello EF).
- Ogni tabella che viaggia ha i due trigger (test sul DB migrato).
- Andata e ritorno: stato → file → stato vuoto = stato uguale (ogni entità, foto comprese).
- Convergenza: tre dispositivi simulati con modifiche, cancellazioni e conflitti incrociati, unioni in ordini diversi
  → stesso stato; nessuna riga persa; nessuna cancellata che ritorna. Con `IClock` finto.
- Ritardo: un dispositivo che unisce un file vecchio non torna indietro.
- Compatibilità: un file con campi ed entità sconosciuti si legge; un `format_version` maggiore diverso si rifiuta.

## 9. Impostazioni › Unisci

Ogni voce ha **una riga di spiegazione** sotto (`.resx` inglese + italiano).

| Voce | Cosa fa |
|---|---|
| Unisci automaticamente | acceso di partenza; all'apertura, al ritorno nell'app, ogni qualche minuto se è aperta; spento = solo a mano |
| Unisci ora | un giro subito |
| Unisci anche le foto | acceso di partenza; spento = viaggiano solo le miniature; sugli altri la foto appare piccola con "originale su <dispositivo>", mai come cancellata |
| Stato | ultima unione; per ogni altro dispositivo l'ora del suo ultimo file; "N voci da sistemare" (le copie B), che apre l'elenco; errori in parole ("Il file del telefono non si apre: la frase è cambiata?") |
| Dove sono i file | computer: la cartella; telefono: il proprio file e i file degli altri, con "Aggiungi", "Cambia", "Scollega questo dispositivo" |
| Frase dell'unione | imposta / cambia |
| Apri in chiaro / Ricifra | solo computer (§6.5) |

Il proprio file si scrive quando l'app va in secondo piano e, se qualcosa è cambiato, al più ogni qualche minuto.

**Prima unione con un dispositivo che ha già dati suoi**: si uniscono, niente si scarta. Prima di applicarla si fa un
backup locale automatico (*mia scelta*: è il momento più rischioso).

**Dopo** (fuori da questa spec): "Esporta tutto" e "Importa" nello stesso formato; collegamento diretto a Drive.

## 10. Architettura (indicazioni, non codice)

- **Data**: mappatura entità ↔ formato, lettore/scrittore del contenitore, motore d'unione, migrazione e trigger,
  `sync_tombstone`, `sync_base`. Servizio `ISyncService` (un giro; stato).
- **Presentation**: ViewModel delle Impostazioni › Unisci; un'astrazione per i file di scambio (cartella sui computer,
  documenti SAF sul telefono). Per `CLAUDE.md` §4 ogni astrazione nuova ha due implementazioni e un test di DI: su
  WPF (congelata) un'implementazione che dichiara "non disponibile".
- **UI**: implementazione cartella (desktop); quella SAF nella testa Android (ROADMAP passo 4), con permessi
  persistenti come nella sonda.
- Il pianificatore dei giri segue il modello di `BackupSchedulerHost` (non parte prima che il DB sia pronto).
- Tutto il tempo da `IClock`; il giorno da `DayBoundary` se serve (l'unione lavora su istanti, non su giorni).

## 11. Lasciato a chi costruisce

Formato JSON esatto di ogni entità (nel rispetto di §6.3); hash della base; come si marca la nota copia; ordine delle
entità per dipendenza; intervallo dei giri; nome della funzione SQLite dell'ora; schema version del bump (2.21);
divisione in onde (proposta: 1. base nel DB + trigger + guardie; 2. formato + andata e ritorno + documento;
3. motore d'unione + convergenza; 4. Impostazioni e cartella sui computer; 5. telefono, dopo la testa Android).
