# "Unisci" — sincronizzazione fra dispositivi attraverso un file di scambio

> Spec del passo 6 della ROADMAP. Conversazione con l'autore del 2026-11-19/22 (sessione `unisci-spec`). Letta da un
> revisore esterno il 2026-11-22; le sue domande sono risolte qui (scelte dell'autore o marcate *mia scelta*).
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
   - **diario e note: si tengono tutte e due le versioni**; l'app avvisa, l'utente unisce a mano. La versione in più
     sta **in coda alla stessa voce**, in un blocco segnato ("— Scritto anche su <dispositivo>, alle HH:MM —"):
     niente registrazioni in più, niente conti falsati (scelta (a) dell'autore, 2026-11-22). Quale versione resta in
     testa è deciso allo stesso modo su tutti i dispositivi (§8.3).
5. **Foto nello stesso file** del resto; in Impostazioni l'utente può escluderle ("Unisci anche le foto").
6. **Formato B** (§6).
7. **Collegare un dispositivo che ha già dati suoi**: l'utente sceglie fra "Parti dai dati degli altri" (consigliata: i
   dati di questo dispositivo vanno in un backup e non si uniscono) e "Tieni anche i dati di questo dispositivo" (si
   uniscono tutti; eventuali doppioni li sistema l'utente). Nessun riconoscimento automatico di abitudini omonime
   nella v1.0.

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
L'unione è **idempotente** e **convergente**:
- rileggere lo stesso file non cambia nulla;
- i dispositivi che continuano a scambiarsi i file arrivano allo stesso stato;
- quando ogni coppia di dispositivi ha la base, i valori dei campi non dipendono dall'ordine dei file
  (`updated_at_ms` sì);
- senza base, il limite di §8.4 può far perdere, a seconda dell'ordine, una modifica concorrente diversa della
  stessa riga: mai una riga, mai un testo (regola B), mai una cancellazione.

(corretto il 2026-11-22 durante l'implementazione: prima diceva "commutativa: in un altro ordine lo stesso stato";
con una sola ora per riga non è vero senza base, e le ore di modifica possono variare per il `+1` di §8.3.)

### 8.3 Regole
- **Riga nuova** (id sconosciuto, nessuna lapide più recente): si inserisce. Le righe figlie arrivano dopo le madri
  (ordine delle entità per dipendenza).
- **Stessa riga su tutti e due**: campo per campo vince l'`updated_at_ms` più recente della riga; a parità vince il
  `device_id` minore (deterministico). *Nota per chi costruisce*: con un solo `updated_at_ms` per riga il "campo per
  campo" si ottiene confrontando i valori con la **base** (§8.4): un campo cambiato da una parte sola vince comunque.
  (corretto il 2026-11-22 durante l'implementazione: a parità vince la riga con la forma canonica minore,
  `docs/formats/sync-file.md` §5, non il `device_id`. Una riga unita che tiene un valore di qui diverso dalla remota
  prende `updated_at_ms` = massimo dei due + 1, salvo che l'ora locale sia già la più recente: deve battere le due
  versioni anche su un dispositivo che non ha ancora la base, altrimenti i due non convergono.)
- **Cancellazione contro modifica**: vince l'azione più recente (`deleted_at_ms` contro `updated_at_ms`). Se vince la
  cancellazione ma dall'altra parte c'è **testo di diario o nota** che il cancellato non aveva, quel testo si salva
  come copia (regola B).
- **Cancellazione di una madre** con figlie modificate altrove dopo: se la madre resta cancellata, le figlie seguono
  (come la cascade locale). Per le abitudini il "cancellato" è il cestino (soft delete): il ripristino è una modifica
  più recente e vince.
- **Diario e note (B)**: se il testo è cambiato da **tutte e due** le parti rispetto alla base, il testo unito è
  `vincente + blocco segnato + perdente`, dove **vincente** = `updated_at_ms` più recente, a parità `device_id` minore.
  Il risultato è una funzione deterministica delle due versioni (stesso testo, stesso `updated_at_ms` = il massimo dei
  due) così che tutti i dispositivi convergano allo stesso testo senza generare copie nuove; un testo che contiene già
  il blocco di quella versione non lo riaggiunge. Il blocco ha un formato fisso e riconoscibile (documentato in
  `docs/formats/sync-file.md`) e lo stato conta le voci che lo contengono ("N voci da sistemare"). Se il testo è
  cambiato da una parte sola, vince quella senza conflitto. Vale per `note.body` e per ogni altro campo di testo
  libero lungo che chi costruisce classifica come "testo" nella mappatura.
  (corretto il 2026-11-23 durante l'implementazione: il testo unito, diverso da tutte e due le versioni, prende
  `updated_at_ms` = massimo + 1 come ogni riga unita che tiene un valore di qui, così batte le due versioni anche dove
  manca la base; con il massimo uno scambio incrociato lascia le due ore diverse di uno per un giro, perché il nome del
  dispositivo tenuto di qui le sposta. Un testo si legge a pezzi (la cima e il corpo di ogni blocco): si aggiungono
  solo i pezzi che il vincente non ha, e i blocchi stanno in ordine d'ora, così una versione non compare mai due
  volte, neanche quando torna indietro un testo già unito. La copia di un testo cancellato è una nota rapida con un id
  deterministico, `docs/formats/sync-file.md` §6.)
  (corretto il 2026-11-23, review del Task 7, Ruling 16/17: un campo uguale alla base in una riga remota più recente
  della base è ambiguo, "non toccato" o "visto il nostro e rimesso com'era" (ABA: la pulizia dei blocchi tornava
  indietro e le ore salivano a ogni giro). Il manifest dice, in `merged_from`, quale nostro file il pari ha unito per
  ultimo. Se il pari ha visto la nostra versione, la sua riga decide ogni campo; se la sua riga è uno stato che
  avevamo già noi, decide la nostra; se no vale la regola dei campi, che tiene il cambio fatto da una parte sola anche
  quando l'altra riga è più recente. La regola letterale "vince la riga più recente sul campo ambiguo" perdeva
  proprio quel cambio. Il salvataggio di un testo cancellato senza base guarda `merged_from`: il cancellante aveva
  visto quella versione, niente nota. Dettagli: `docs/formats/sync-file.md` §2.1 e §6, regole 2 e 4.)
  (corretto il 2026-11-23, seconda review del Task 7: l'ora d'unione nella base la mette solo l'unione che cambia la
  riga; un'unione che la lascia com'è non la sposta, altrimenti il file di un terzo dispositivo unito nel mezzo
  faceva sembrare "non visto" ciò che il pari aveva visto. Una modifica fatta qui conta come vista solo se il nostro
  file unito dal pari è stato scritto almeno 5 s dopo: l'ora della modifica si prende prima del salvataggio.)
- **Invarianti di dominio che la riga non vede** (*mia scelta*): il motore le rende coerenti in modo deterministico
  senza cancellare dati, e le conta fra le "voci da sistemare":
  - due periodi di astinenza aperti sulla stessa abitudine: resta aperto quello iniziato prima; l'altro, che gli sta
    tutto dentro, si chiude al proprio inizio (durata zero), resta visibile con le sue note e si conta fra le voci da
    sistemare (chi costruisce verifica che un periodo di durata zero non sporchi "di fila" e statistiche);
  - un ciclo nell'albero (`parent_id` spostati su due dispositivi): perde lo spostamento più recente, il nodo torna
    sotto la madre di prima (o in radice se non esiste più).
- **Chiavi naturali uniche** (*mia scelta*): due righe con GUID diversi e la stessa chiave naturale sono **la stessa
  cosa**:
  - `tag` (nome, senza distinzione di maiuscole), `unit_of_measure.code`, `scale_template.code`: si tiene il GUID
    minore; i riferimenti (`note_tag`, `habit_node_tag`, `scale_anchor.tag_id`, `metric_definition`, …) si riscrivono;
    il GUID scartato diventa una lapide con `alias_of` (campo in `sync_tombstone`), così chi lo riceve più tardi lo
    riscrive allo stesso modo;
  - `scale_anchor`: si confronta per (`scale_template_id`, `value`), non per id (gli scaglioni si ricostruiscono con
    GUID nuovi a ogni modifica della scala).
  Ogni indice unico del modello EF va classificato (chiave naturale gestita / impossibile da duplicare): test di guardia.
- `HabitConfigHistory` e `NoteHistory` sono storici: si uniscono come insiemi (righe nuove si aggiungono).

### 8.4 La base
Per riconoscere "cambiato da tutte e due le parti" serve sapere com'era la riga all'ultima unione con quel
dispositivo. Tabella locale `sync_base` (non viaggia): per (`peer_device_id`, `entity`, `entity_id`) l'hash della riga
o dei campi rilevanti all'ultima unione. Senza base (prima unione con quel dispositivo) le differenze di testo si
trattano come conflitto (nessun testo scartato).

Senza base, per i campi **non di testo** si ricade sull'intera riga "vince la più recente" (*mia scelta*): due
modifiche a campi diversi della stessa riga fatte su due dispositivi prima di un'unione ne perdono una. È raro
(richiede modifiche concorrenti alla stessa riga) e non tocca testo; se l'uso lo mostra, il rimedio è un'ora per campo
tenuta in una tabella locale e scritta nel formato (versione minore). `ponytail:` nel codice.

La base è la riga **remota** com'era all'ultima unione (forma pubblica; le foto come SHA-256). Una riga remota con
`updated_at_ms` minore di quello della base viene da un file vecchio dello stesso dispositivo e si ignora. (corretto il
2026-11-22 durante l'implementazione: poiché la base è la riga remota e non quella unita, una riga unita prende
massimo + 1 come in §8.3.)

### 8.4-bis Identità del dispositivo e ripristino (*mia scelta*)
- `device_id` sta in `sync_meta`, dentro il database. Dopo un **ripristino** da `.recbak` (o una copia del DB su un'altra
  macchina) il dispositivo **rigenera** `device_id`, azzera `sync_base`, e rientra come un dispositivo nuovo con dati
  vecchi: le modifiche e le cancellazioni più recenti degli altri vincono. Un ripristino quindi **non annulla** nulla
  sugli altri dispositivi; lo dice la spiegazione del ripristino quando l'unione è attiva. ("Ripristina per tutti" è
  fuori dalla v1.0.)
- Il ripristino tiene le lapidi presenti nel backup; quelle successive arrivano dagli altri file.
- Il backup prima della prima unione (§9) protegge i dati di **questo** dispositivo; non è un modo di annullare
  l'unione per tutti.

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
  → stesso stato in ogni ordine; nessuna riga persa; nessuna cancellata che ritorna. Con `IClock` finto. Con la
  base fra ogni coppia, stessi valori dei campi fra ordini diversi (tolto `updated_at_ms`). Senza base, in ogni
  ordine nessuna riga persa.
  (corretto il 2026-11-22 durante l'implementazione: il confronto fra ordini vale per i valori dei campi con la base,
  non per l'intero stato; vedi §8.2.)
- Ritardo: un dispositivo che unisce un file vecchio non torna indietro.
- Compatibilità: un file con campi ed entità sconosciuti si legge; un `format_version` maggiore diverso si rifiuta.

## 9. Impostazioni › Unisci

Ogni voce ha **una riga di spiegazione** sotto (`.resx` inglese + italiano).

| Voce | Cosa fa |
|---|---|
| Unisci automaticamente | acceso di partenza; all'apertura, al ritorno nell'app, ogni qualche minuto se è aperta; spento = solo a mano |
| Unisci ora | un giro subito |
| Unisci anche le foto | acceso di partenza; spento = questo dispositivo **non scrive** originali nel suo file e **non tiene** gli originali degli altri, solo le miniature (risparmia spazio e dati sul telefono); sugli altri la foto appare piccola con "originale su <dispositivo>", mai come cancellata. La riga di spiegazione avverte che gli originali fatti qui restano solo qui. Una foto senza originale non si considera modificata: chi l'ha ancora la tiene |
| Stato | ultima unione; per ogni altro dispositivo l'ora del suo ultimo file; "N voci da sistemare" (le copie B), che apre l'elenco; errori in parole ("Il file del telefono non si apre: la frase è cambiata?") |
| Dove sono i file | computer: la cartella; telefono: il proprio file e i file degli altri, con "Aggiungi", "Cambia", "Scollega questo dispositivo" |
| Frase dell'unione | imposta / cambia |
| Apri in chiaro / Ricifra | solo computer (§6.5) |

Il proprio file si scrive quando l'app va in secondo piano e, se qualcosa è cambiato, al più ogni qualche minuto.

**Collegare un dispositivo che ha già dati suoi**: la scelta del §5.7. In tutti e due i casi prima si fa un backup
locale automatico (*mia scelta*: è il momento più rischioso).

**Peso del file con le foto** (*mia scelta*, coerente con la decisione 5): ogni scrittura ricarica il file intero, foto
comprese. Si scrive solo se qualcosa è cambiato, quando l'app va in secondo piano o al più ogni qualche minuto. Lo stato
mostra la dimensione del file. Se cresce troppo (centinaia di MB), il rimedio è un file foto separato scritto solo
quando cambiano le foto: versione minore del formato, fuori dalla v1.0.

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
