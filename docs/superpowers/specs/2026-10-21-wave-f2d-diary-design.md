# F2d — Diario (design)

> Data: 2026-10-21 · Branch: da `f2c-data-safety` · Autorità: review `2026-09-25-avalonia-v1-plan-review.md` §6 Passo 2-3,
> RV-4, RV-7; decisione D10 (ROADMAP); manifesto `docs/DOGFOODING.md` (righe "Diario del cibo" e "Diario della giornata").
> Perimetro deciso con l'autore il 2026-10-20/21 in brainstorming. **Tre onde, in quest'ordine**: F2d-1 (tag sulle voci + foto
> cifrate), F2d-2 (editor della sera), F2d-3 (`.recbak` v2 a blocchi). Ogni onda ha il suo piano e la sua review di branch.

## 1. Perché

Il manifesto dell'autore ha due diari che devono funzionare dal primo giorno di dogfooding, e su Avalonia oggi non ce n'è
nessuno: il tipo diario non ha fascia "Oggi", non ha quick-add, la colonna Diario non è portata (F2a, decisione 3).

- **Diario del cibo.** A pranzo, a casa. Più tardi, al computer, l'autore scrive una nota breve (cosa e quanto) e, quando c'è,
  aggiunge la foto passata dal telefono al PC. L'ora che resta è quella della registrazione, messa dal programma. Vuole poter
  marcare la voce come "pranzo", "cena", "merenda".
- **Diario della giornata.** La sera, un testo più lungo con titoli, grassetto, elenchi, immagini; rileggere i giorni prima.
  "Un minimo di editor"; le funzioni si allargheranno dopo.

**Fatto quando**: l'autore usa il diario del cibo ogni giorno con tag e foto, e il diario della sera nell'editor, su Linux;
nessuna foto esiste in chiaro sul disco; un backup con un anno di foto si crea e si ripristina con la memoria costante.

## 2. Principi in gioco

- **Più modi di fare la stessa cosa** (parole dell'autore). Pranzo/cena/merenda si possono fare **per composizione** (gruppo
  "Dieta" con un'abitudine diario per pasto — funziona già, nessun lavoro) **oppure con i tag sulla voce**. Il tag è
  un'etichetta sulla *voce*, non un'opzione sulla configurazione dell'abitudine: non viola il principio 4.
- **Privacy by default**: le foto si cifrano come il database (scelta dell'autore, 2026-10-21). Oggi sono file in chiaro in
  `attachments/` (RV-4, tabella), protetti solo dal modo 0700 della cartella.
- **Come Obsidian** è il riferimento dichiarato per tag ed editor.

## 3. Non-obiettivi e anti-obiettivi

**Non-obiettivi** (fuori da F2d, si rivalutano col dogfooding): sincronizzazione o trascinamento dal telefono; tag annidati
(`#cibo/pranzo`); rinominare un tag riscrivendo il testo delle note; tabelle, link fra note, vista di sola lettura, ricerca nel
testo (FTS5, già prevista dopo); video e file non immagine; HEIC (vedi §5.3).

**Anti-obiettivi** (sarebbe un fallimento anche se "funziona"):
- una foto, anche temporanea, scritta in chiaro fuori da `TempDirectory`, o un file in chiaro che resta dopo l'import;
- la posizione GPS conservata con l'impostazione predefinita;
- un editor che perde testo (chiusura della tab, crash, errore di salvataggio taciuto);
- il tag come campo della configurazione dell'abitudine o di `HabitConfig`;
- un backup che smette di crearsi in silenzio quando le foto crescono.

## 4. Cosa c'è già (da riusare, non da rifare)

- **Modello B del diario** (`IDiaryService`, schema 2.11): ogni voce è una `Note` con `Category = Diario` appaiata 1:1 a una
  `Completion`; più voci al giorno, ognuna con la sua ora. Cibo e sera sono lo stesso modello: cambia il gesto
  (`HabitUiPrefs.JournalGesture`: `QuickNote` per il template "Food diary", `Diary` di default).
- **Tag sulle abitudini**: tabella `tag` (nome unico `NOCASE`, max 64, colore opzionale), `habit_node_tag`, `ITagService`,
  `TagsSectionViewModel` + `TagChipViewModel` (sezione Tag dell'editor, WPF e Avalonia). Nessun tag sulle voci.
- **Allegati**: tabella `attachment` (`note_id`, `filename`, `mime_type`, `file_size_bytes`), `IAttachmentService`
  (`AddAsync` fa `File.Copy` sotto `attachments/{noteId}/{id}.{ext}`), consumatori: backup/restore (copiano la cartella),
  `ExportQueryService` (l'export HTML incorpora le foto), `JournalQuickEntryViewModel`, `NotesViewModel`, `AttachmentViewModel`.
- **ViewModel della nota veloce** (`JournalQuickEntryViewModel`, `JournalEntryRowViewModel`, `JournalTodayContent`) in
  Presentation; la view esiste solo su WPF (`JournalQuickEntryView.xaml`: testo, "Aggiungi foto", Salva, voci di oggi con ora,
  modifica, elimina).
- **Prova Live Preview** su Avalonia (`LivePreviewEditorControl`, `MarkdownLivePreviewColorizer`, `MarkdownLineScanner`,
  `MarkdownLivePreviewMarkerGenerator`; doc `docs/design/2026-09-10-avalonia-live-preview-probe.md`): grassetto, corsivo,
  titoli resi mentre si scrive, sintassi grezza sulla riga del cursore. Etichettata "sonda, NON una feature".
- **SkiaSharp 3.119.4** è già una dipendenza (ExportEngine), con il pattern dei pacchetti nativi per RID.

## 5. F2d-1 — Tag sulle voci e foto cifrate

### 5.1 Tag sulle voci, nei due modi (come Obsidian)

Mockup approvato dall'autore il 2026-10-21 (`.superpowers/brainstorm/…/nota-veloce-tag.html`, non versionato):

- **Nel testo**: scrivendo `#` compare un menu con i tag esistenti filtrati per prefisso; l'ultima voce del menu è
  "Crea «#…»". Il tag resta nel testo, evidenziato.
- **Nella riga "Tag"** sotto il testo: chip scelti a parte, con lo stesso menu dietro "+ tag". Il testo resta pulito.
- **I tag della voce sono l'unione dei due.** L'elenco è uno solo, lo stesso dei tag delle abitudini: "pranzo" creato da una
  nota compare anche nella sezione Tag dell'editor delle abitudini, e viceversa.
- Le voci di oggi e la cronologia mostrano i chip dei tag.

Decisioni (mie, l'autore non le ha discusse):
- **Sintassi del tag inline**: `#` a inizio parola seguito da lettere Unicode, cifre, `_`, `-`; almeno un carattere non
  cifra (`#2` non è un tag, come in Obsidian). `# Titolo` (con lo spazio) resta un titolo. Dentro codice inline o blocchi di
  codice non è un tag.
- **Persistenza**: una tabella `note_tag` (`note_id`, `tag_id`, `source` TEXT `inline`|`property` con CHECK, PK sulla terna,
  cascade esplicita da `note` e da `tag`). Al salvataggio di una voce le righe `inline` si **ricalcolano dal testo** (i tag
  mancanti si creano), quelle `property` vengono dalla riga Tag. Un tag rimosso dal testo sparisce dalla voce; lo stesso nome
  presente anche come `property` resta.
- **Cancellare un tag dall'elenco globale** toglie i link `property`; il `#pranzo` nel testo resta, e al prossimo salvataggio
  di quella voce il tag rinasce. Documentato, non è un bug.
- **Nome**: confronto senza maiuscole (la colonna è già `NOCASE`); si conserva la grafia della prima creazione.
- Il servizio che salva la voce (`IDiaryService.SaveEntryAsync` / `EditEntryAsync`) riceve i tag `property` e fa tutto in una
  transazione con la nota: niente voce salvata con i tag a metà.
- **Tag su `Note` di categoria Veloce** (le note del giorno degli altri tipi): stesso meccanismo, gratis, ma la UI li mostra
  solo dove esiste la view (nota veloce del diario in F2d-1, editor in F2d-2).

### 5.2 Nota veloce su Avalonia

Porta su Avalonia la nota veloce di WPF, con i tag e le foto: fascia "Oggi" del diario (`JournalTodayContent`, oggi senza
view) che apre la nota veloce o la colonna Diario secondo il gesto; composer (testo con il menu `#`, riga Tag, "Aggiungi
foto…", Salva); elenco delle voci di oggi (ora, testo, chip, miniature, modifica, elimina). Il chip "Diario" della schermata
dell'abitudine resta nascosto fino a F2d-2. **Fino a F2d-2 il gesto `Diary` apre la nota veloce** (mia scelta): il diario
della sera si può già scrivere, come testo semplice; il markdown è testo, e l'editor di F2d-2 lo rileggerà così com'è.

### 5.3 Foto: import (D10) e cifratura

**D10** (decisa il 2026-09-30): all'import le foto **si ridimensionano e si puliscono dai metadati** per impostazione
predefinita, con tre scelte per l'utente.
- Le tre scelte stanno nelle **Impostazioni** (mia scelta): "Ridimensiona e pulisci" (predefinito), "Solo pulisci",
  "Tieni l'originale". Accanto a "Tieni l'originale" un avviso: conserva anche la posizione GPS. Un `AppSetting`
  (`photo_import_mode`, enum TEXT + CHECK).
- **Ridimensiona**: lato lungo massimo 2048 px (RV-7a), proporzioni conservate, mai ingrandire; ricodifica JPEG (qualità a
  scelta del costruttore, ~85); PNG resta PNG. L'orientamento EXIF si **applica ai pixel** prima di togliere i metadati, così
  la foto non esce ruotata.
- **Pulisci**: la ricodifica con SkiaSharp non scrive metadati; "Solo pulisci" ricodifica alla dimensione originale.
- **Formati in ingresso**: JPEG, PNG, WebP (quelli che SkiaSharp decodifica su tutti e due i sistemi). Un formato non
  supportato (HEIC degli iPhone) si rifiuta con un messaggio che dice cosa fare ("esporta la foto come JPEG"), mai con
  un'eccezione grezza.

**Cifratura** — le foto vanno **dentro il database SQLCipher** (mia scelta tecnica), come colonna BLOB su `attachment`.
Perché: stessa chiave e stessa passphrase senza un secondo schema di cifratura; il backup le include già col dump del DB;
il restore incrociato Windows → Linux non deve ricifrare niente (con file cifrati a parte la chiave DPAPI non viaggerebbe).
Il costo è un DB più grande: con foto ridimensionate (~300 KB) un uso da food diary fa ~300 MB l'anno.
- Il file sorgente scelto dall'utente **non si tocca**; i byte normalizzati vanno nel BLOB senza passare da un file in chiaro.
  Se un passaggio intermedio su disco servisse, solo in `TempDirectory` e cancellato in `finally`.
- **Migrazione dei file esistenti** (schema 2.19): all'avvio, una volta, i file di `attachments/` entrano nel BLOB **così
  come sono** (non si ridimensionano: sono dell'utente, D10 vale all'import). Un file si cancella **solo dopo** il commit della
  sua riga; un file mancante o illeggibile si registra nel log e la riga resta, segnata come mancante, mai cancellata in
  silenzio. A migrazione completata la cartella `attachments/` vuota si toglie.
- Chi legge le foto (export, miniature, editor) le chiede a `IAttachmentService` come byte o stream, non come percorso.
  Il backup smette di copiare `attachments/`; il restore di un `.recbak` vecchio che contiene `attachments/` le importa nel BLOB
  con la stessa logica della migrazione.
- **Gli allegati sono della voce, non del testo** (mia scelta). Una foto appartiene alla voce finché la voce esiste o
  finché l'utente la toglie esplicitamente dall'elenco delle foto; un riferimento `![](…)` cancellato dal testo **non**
  cancella l'allegato (le versioni in `NoteHistory` possono ancora puntarci, e niente si perde per un taglia-incolla). Le due
  view mostrano la stessa cosa: la nota veloce l'elenco delle foto della voce; l'editor le immagini nel testo e, sotto, le foto
  della voce che il testo non richiama. Una foto aggiunta a una voce **non ancora salvata** resta in memoria (byte già
  normalizzati) e si scrive nella stessa transazione della voce: nessuna `Note` o `Completion` nasce prima di "Salva".
- **WPF** (mia scelta): la shell congelata continua a **mostrare** le foto, leggendole dal BLOB con un convertitore byte →
  bitmap; nessuna funzione nuova su WPF. Il ViewModel condiviso (`AttachmentViewModel`) espone i byte o uno stream al posto
  di `FilePath`; i test WPF restano verdi.
- **Miniature**: a scelta del costruttore se salvarle (seconda colonna) o calcolarle alla lettura; vincolo: l'elenco delle voci
  di oggi con dieci foto non deve bloccare la UI (decodifica fuori dal thread della UI).

## 6. F2d-2 — Editor del diario della sera

La prova Live Preview diventa un editor vero, nella **colonna Diario** della schermata dell'abitudine (portata su Avalonia in
questa onda, chip "Diario" visibile), aperta dal gesto `Diary` della fascia "Oggi".

Nel perimetro "minimo" (deciso con l'autore, 2026-10-21):
- grassetto, corsivo, titoli (ci sono già nella prova); **elenchi** puntati e numerati; **citazioni**; scorciatoie Ctrl+B e
  Ctrl+I;
- **immagini nel testo**: "Inserisci immagine…" sceglie un file, lo passa dall'import del §5.3 e inserisce nel punto del
  cursore un riferimento all'allegato (sintassi markdown `![](…)` con uno schema che punta all'id dell'allegato, a scelta del
  costruttore); l'editor mostra l'immagine nel testo, la riga del cursore mostra la sintassi grezza come per il resto;
- **tag**: `#` evidenziato, menu dei suggerimenti come nella nota veloce, riga Tag sotto l'editor;
- rileggere i giorni prima dalla cronologia che c'è già.

Vincoli: il testo non si perde mai — chiudere la tab o la finestra con modifiche non salvate chiede cosa fare (salvataggio
esplicito come su WPF; un autosave è accettabile se il costruttore lo preferisce, purché un errore di scrittura si veda);
l'export risolve i riferimenti alle immagini; cancellare una voce cancella i suoi allegati.

**Export con le immagini** (deciso dall'autore, 2026-10-21): l'export **Markdown** produce una **cartella** con il `.md` e
una sottocartella di immagini, che si apre così com'è in Obsidian; i riferimenti nel `.md` sono relativi. HTML e PDF
incorporano le immagini come oggi. Cambia il contratto dei plugin di export (`IExportFormatter.Write` restituisce un solo
`byte[]`; `ExportAttachmentRef.RelativePath` presuppone file su disco): il piano dichiara la forma nuova (più file in uscita o
un writer di cartella) e la fa passare da `IExtensionPoint` come il resto.

## 7. F2d-3 — `.recbak` v2 a blocchi (RV-7b)

Oggi `AesGcmBackupCipher` copia l'intero archivio in un `MemoryStream` e cifra in un colpo: 3-4 volte la dimensione in RAM e
un tetto di 2 GiB. Con le foto nel DB il tetto si avvicina col tempo. Il formato va cambiato **prima della v1.0**: dopo, ogni
utente avrà file v1 da leggere per sempre (E17).

- Il writer scrive solo **v2**; il reader legge v1 e v2 (il byte `FormatVersion` c'è già nell'header `RCBK`).
- Cifratura e decifratura **a flusso**: AES-256-GCM per blocco di dimensione fissa, nonce derivato da una base casuale e dal
  contatore del blocco, l'indice e il flag "ultimo blocco" nell'AAD (un file troncato o con blocchi riordinati si rifiuta).
  Argon2id e header restano come in v1. Dimensione del blocco a scelta del costruttore (ordine del MiB).
- RAM costante indipendente dalla dimensione del backup; i file in chiaro intermedi solo in `TempDirectory`.
- Test: round-trip v2; un v1 esistente si legge ancora; troncamento, blocco alterato, blocchi scambiati → errore chiaro; un
  backup oltre 2 GiB (`Category=Performance`, solo in locale) si crea e si ripristina.
- Nessun cambiamento visibile all'utente.

## 8. Obblighi trasversali

- Stringhe in `.resx` (en + it) con accessor in `Strings.Designer.cs` via shell; icone Lucide; chiaro e scuro.
- Schema 2.18 → **2.19** in F2d-1 (`note_tag`, colonna BLOB e flag "mancante" su `attachment`, `photo_import_mode`); il bump
  tocca 8 riferimenti nei test. F2d-2 e F2d-3 non toccano lo schema, salvo necessità che il piano dichiara.
- `BuilderTemplateParityTests`: se F2d aggiunge un template (es. "Dieta"), serve la fixture.
- Ogni onda chiude con review dell'intero branch, CI verde e una checklist a schermo per l'autore nella ROADMAP.

## 9. Lasciato al costruttore

Qualità JPEG e dimensione del blocco v2; miniature salvate o calcolate; schema dell'URL delle immagini nel markdown; come il
menu `#` si aggancia al controllo di testo (TextBox nella nota veloce o lo stesso editor in modalità semplice); salvataggio
esplicito o autosave nell'editor (entro i vincoli del §6); dove vive il codice dell'import delle foto (Data accanto a
`AttachmentService`, o un'astrazione in Core con l'implementazione in Data), purché Presentation resti `net10.0` senza UI.
