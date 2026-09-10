# Sonda editor Avalonia — l'editor markdown esiste senza WebView2?

**Branch**: `avalonia-linux-slice`. **Tipo**: probe/spike, non una feature — l'output è una
risposta con prove, non codice di produzione. Niente sotto `src/Recordum.WPF/` è stato toccato.

## Premessa: cosa usa DAVVERO Recordum di Toast UI

La domanda giusta non è "AvaloniaEdit è bravo quanto Toast UI" — è "fa quello che Recordum usa
davvero". La risposta sta in `src/Recordum.WPF/Resources/EditorAssets/editor.js` (70 righe) e nei
tre call site, non nel feature-matrix di Toast UI.

**`editor.js` accende:**

- `initialEditType: 'wysiwyg'` — l'utente edita il documento **renderizzato**, non markdown
  grezzo. Non vede mai `**grassetto**` a meno di passare deliberatamente alla modalità Markdown
  (i tab in fondo, `toastui-editor-mode-switch`).
- `previewStyle: 'tab'` — rilevante **solo** dentro la modalità Markdown (quando l'utente
  l'attiva): un tab-strip Write|Preview. In WYSIWYG (il default, quello che l'utente vede sempre
  a meno di uno switch manuale) non esiste una "preview" separata — il documento renderizzato È
  l'editor.
- Una toolbar di **13 comandi**: `heading, bold, italic, strike, ul, ol, task, hr, quote, table,
  code, codeblock, link`. Niente immagine — né bottone né `addImageBlobHook`.
- `usageStatistics: false`.
- Un bridge JS↔.NET per 4 messaggi: `ready`, `change` (markdown→.NET), `setContent`/`setReadOnly`/
  `setTheme` (.NET→markdown), con guardia di rientranza (`applyingRemote`) per non far rimbalzare
  gli eventi di `setMarkdown()`.
- `setReadOnly` è un'emulazione: **non esiste** un vero read-only in Toast UI, si disabilita
  `pointer-events` sull'intera UI (blocca anche lo scroll col mouse).

**I tre call site** (`grep` su `MarkdownEditorControl`):

| Call site | Uso | `Placeholder` | `IsReadOnly` |
|---|---|---|---|
| `NotesTabView.xaml:104` | editor Note completo, dentro la colonna Diario | non impostato | non impostato |
| `HabitScreen.xaml:702,739` | quick-add/retroattivo per completion di tipo Journal | non impostato | non impostato |
| `BuilderPreviewPane.xaml:197` | **MAI istanziato** — stand-in statico (`Preview.GhostLine`), commento esplicito "MAI il MarkdownEditorControl (WebView2)" | — | — |

Conseguenza verificata col grep, non assunta: **`Placeholder` e `IsReadOnly` sono superficie
pubblica morta**. Nessun call site li imposta mai. `Placeholder` non è nemmeno cablato verso
Toast UI in `editor.js`/`editor.html` — è una proprietà dichiarata e mai letta da nessuno, in
nessuna direzione. `BuilderPreviewPane` non è un vero call site del controllo: è il posto dove
CLAUDE.md registra la lezione C1/W24-Q1 (un `ContentControl` con `ContentTemplate` statico
materializza il template anche a `Content` null) — l'anteprima del builder usa apposta un
placeholder statico per non pagare quel costo.

Le foto allegate a una nota (`JournalQuickEntryViewModel.PendingPhotos`, composer separato di
U7/Onda 2.4) **non passano mai dal markdown editor** — confermato leggendo il ViewModel: sono
gestite da un dialog di file-picking e un `IAttachmentService` indipendenti. L'editor non deve
saper gestire immagini.

**Requisiti onesti, in una riga**: editing WYSIWYG di markdown "GitHub-flavored" comune
(titoli, grassetto/corsivo/barrato, liste puntate/numerate/task, citazioni, tabelle, codice
inline+blocco, link), niente immagini, un fallback quando il motore di rendering manca, e
temi/font che seguono l'app dal vivo.

## Cosa dà il sostituto nativo, feature per feature

| Cosa | Toast UI (oggi) | AvaloniaEdit + Markdown.Avalonia | Importa per Recordum? |
|---|---|---|---|
| Modello di editing | **WYSIWYG**: un solo documento, editi il rendering | **Due controlli distinti**: AvaloniaEdit edita markdown grezzo, Markdown.Avalonia RENDE ma non è editabile — non esiste un "editi-il-rendering" nativo | **Sì, ed è la perdita più grande.** Vedi sezione dedicata sotto. |
| Formattazione via toolbar | 13 comandi con un click (grassetto, liste, tabella, code, link, …) | **Nessuno pronto all'uso.** AvaloniaEdit è un editor di testo — dà syntax-editing (selezione, undo/redo, ricerca) ma zero comandi di formattazione markdown; andrebbero scritti a mano sopra `TextArea.Selection`/`Document` (fattibile, non incluso in questa sonda) | Sì — oggi un click, con la sola porta nativa richiede scrivere markdown a mano (`**`, `-`, ` ``` `) |
| Rendering markdown (titoli, liste, link, code block, tabelle) | ProseMirror (Toast UI) | Markdown.Avalonia (motore proprio, GFM-like) — **verificato a schermo**: titoli, grassetto/corsivo, lista, link, code block con syntax highlight tutti corretti nei due screenshot | No, equivalente per quello che serve qui |
| Task list (`- [ ]`) | Sì (toolbar dedicata) | Non verificato in questa sonda (fuori dal set di contenuti richiesto: titoli/grassetto/lista/link/code block) | **Da verificare prima di un porting reale** — la toolbar di `editor.js` lo accende esplicitamente |
| Read-only | Emulato (`pointer-events:none` su tutta la UI, blocca anche lo scroll) | **`TextEditor.IsReadOnly` nativo** — read-only vero (blocca l'edit, non lo scroll/selezione/copia) | Miglioramento — ma il gap non conta perché nessun call site usa `IsReadOnly` oggi (tabella sopra) |
| Placeholder | Dichiarato, mai cablato (morto) | Overlay `TextBlock` funzionante, nascosto non appena il documento non è vuoto | Neutro: nessun call site lo usa oggi in nessuna delle due versioni; nella porta è tenuto per parità di superficie e perché è quasi gratis, non perché serva ora |
| Tema (colori) | Bridge JS: payload JSON di 6 brush + font, applicato a ~60 righe di override CSS `!important` per ogni classe DOM di Toast UI, ripetuto ad ogni switch tema/font (due `DependencyProperty` "tripwire" dedicate) | `DynamicResource` ordinario sui pochi elementi propri (bordi, sfondi, `TextEditor.Background/Foreground`); **il pannello di anteprima segue light/dark da solo** — verificato negli screenshot: `MarkdownStyleFluentTheme` di Markdown.Avalonia si aggancia al `ThemeVariant` di Avalonia senza che il nostro codice gli dica nulla | Il meccanismo sparisce, non solo il codice: niente bridge, niente tripwire, niente lista di override CSS da mantenere ad ogni nuova classe DOM che Toast UI introduce in un aggiornamento |
| Font follows app | Stessa tripwire di sopra, iniettato come CSS var | `DynamicResource Font.Default` ordinario | Stesso discorso: gratis con l'ordinario data-binding Avalonia |
| Disponibilità motore | `IWebView2Availability` + fallback `TextBox` grezzo quando WebView2 manca | **Non serve**: AvaloniaEdit/Markdown.Avalonia sono librerie managed, sempre presenti se il pacchetto è referenziato — non c'è un runtime esterno la cui assenza vada rilevata | Sparisce per intero, è una vera semplificazione (vedi sotto) |
| Immagini (paste/drop) | Non acceso (nessun bottone, nessun hook) | N/A — non serve, confermato dal grep sul ViewModel del composer | Nessun gap: non era un requisito |

### Il modello WYSIWYG, in dettaglio

Questo è il punto su cui l'inquadramento della sonda insiste, e va detto senza ammorbidirlo:
**oggi l'autore scrive il diario ogni giorno dentro un documento che assomiglia a un documento**
— titoli grandi, grassetto vero, elenchi puntati veri, mentre digita. Il sostituto nativo
composto in questa sonda (AvaloniaEdit + Markdown.Avalonia) non ha un equivalente: si scrive
sempre e solo markdown grezzo (`# Titolo`, `**grassetto**`, `- voce`) in un pannello, e il
rendering vive in un pannello **separato**, sempre di sola lettura. Non è una differenza
cosmetica — è un modello di editing diverso, e per chi scrive spesso e non è abituato alla
sintassi markdown è oggettivamente un downgrade dell'esperienza quotidiana. Le due strade note
per attenuarlo (non costruite qui, fuori budget del probe):

1. **Split fisso o toggle**, con una toolbar che inserisce sintassi al posto dell'utente
   (bottone "grassetto" che avvolge la selezione in `**`) — recupera la comodità dei 13 comandi
   ma non il "vedi il grassetto mentre scrivi".
2. Un vero motore WYSIWYG per Avalonia (rich text editabile) — non risulta esistere un
   equivalente maturo di ProseMirror nell'ecosistema Avalonia oggi; costruirlo da zero è un
   progetto a sé, non una libreria da referenziare.

Nessuna delle due è stata tentata in questa sonda (fuori budget — vedi vincoli). La sonda
dimostra che l'edit-e-preview **funzionano**, non che l'esperienza sia pari.

## Cosa sparisce (quantificato)

- **704 KB di asset vendorizzati** (`toast-ui-editor.min.js` 524K + `.min.css` 164K + `editor.html`
  87 righe + `editor.js` 70 righe + licenza) sotto `src/Recordum.WPF/Resources/EditorAssets/` —
  interamente. Il sostituto non ha assets statici da vendorizzare: sono pacchetti NuGet ordinari.
- **Il bridge JS↔.NET**: `OnWebMessageReceived`, `PostWebMessageAsJson`, i 4 tipi di messaggio,
  la guardia `applyingRemote`/`_bridgeReady`. Circa metà delle 207 righe di
  `MarkdownEditorControl.xaml.cs` esiste solo per parlare con un processo browser attraverso
  JSON. Nel porto, la sincronizzazione fra editor e preview è una chiamata di metodo C# diretta
  (`Preview.Markdown = text`), non un messaggio serializzato attraverso un confine di processo.
- **Il fallback di disponibilità**: `IWebView2Availability`, `AvailabilityProperty`,
  `ActivateFallback`, `FallbackBox` (un `TextBox` grezzo mostrato quando WebView2 non c'è). Non
  c'è più un runtime esterno la cui assenza vada rilevata e degradata — la proprietà pubblica
  `Availability` sparisce dalla superficie del controllo, non solo dall'implementazione.
- **Le due `DependencyProperty` "tripwire"** (`ThemeTripwireProperty`/`FontTripwireProperty`) e
  `PushThemeToEditor`/`Hex()` — l'intero meccanismo per ri-spingere manualmente colori e font nel
  documento HTML ad ogni switch di tema. `DynamicResource` lo fa gratis nel porto.
- **La causa radice dei due difetti in produzione** registrati in CLAUDE.md §8 (C1 dell'Onda 2.4,
  W24-Q1 dell'Onda 2.5): un `ContentControl` con `ContentTemplate` statico materializza il
  template — e con esso **un intero processo browser Chromium + una finestra nativa `HwndHost`**
  — anche a `Content` null o `Visibility="Collapsed"`. Con un controllo managed puro (nessun
  `HwndHost`, nessun processo esterno), la stessa eventuale disattenzione templating costerebbe
  l'istanziazione di qualche oggetto .NET inutile, non l'apertura silenziosa di un browser che
  WPF non può nemmeno sovradisegnare. **La classe di difetto non è necessariamente sparita** (non
  verificato se `ContentPresenter` di Avalonia abbia la stessa scelta-template-anche-a-null-content
  — è un footgun generico di templating, non specifico di WebView2) **ma la sua posta in gioco
  crolla**: da "un browser fantasma che copre l'interfaccia" a "spreco trascurabile".

## Il costo

**Righe** (solo il controllo, non lo scaffolding di sonda):

| | WPF (oggi) | Avalonia (sonda) | Δ |
|---|---|---|---|
| Markup (`.xaml`/`.axaml`) | 12 | 71 | +59 |
| Code-behind (`.cs`) | 207 | 106 | **−101** |
| **Totale controllo** | **219** | **177** | **−42 (−19%)** |

Il codice cala nonostante il markup cresca: la maggior parte delle 207 righe WPF è
gestione-WebView2 (init/fallback/bridge/tripwire) che nel porto semplicemente non esiste — il
nucleo di sincronizzazione editor↔preview↔proprietà pubblica è ~35 righe.

Più lo scaffolding della sonda (non costo di produzione, ma consegnato per i deliverable):
`Views/MarkdownEditorProbeView.axaml(.cs)` (61 righe) + `tests/.../MarkdownEditorScreenshotTests.cs`
(47 righe) = 108 righe.

**Pacchetti**: 2 diretti (`Avalonia.AvaloniaEdit` 12.0.0, `Markdown.Avalonia` 12.0.0-a3), che
tirano **13 pacchetti transitivi** (`Markdown.Avalonia.{Html,Svg,SyntaxHigh,Tight}`,
`ColorDocument.Avalonia`, `ColorTextBlock.Avalonia`, `Svg.Controls.Avalonia`, `Svg.{Custom,Model,
SceneGraph}`, `HtmlAgilityPack`, oltre ad `AvaloniaEdit` stesso) — **~2.5 MB di DLL managed**,
verificato sull'output di build. Contro 704 KB di JS/CSS **più** l'intera dipendenza da un
runtime WebView2 (bundle Edge/Chromium, tipicamente decine/centinaia di MB — condiviso col
sistema su Windows quando presente, ma comunque un intero motore browser). Il porto è più
pesante in numero di pacchetti, più leggero come impegno di runtime esterno.

**Rischio di manutenzione — il punto più importante di questa sezione**: `Markdown.Avalonia`
**stabile** (`11.0.3`, pubblicato 2026-05-27) **compila** pulito contro Avalonia 12.1.2 ma
**esplode a runtime** — vedi sotto. La riga che compila E gira, `12.0.0-a3`, è **prerelease
alpha**, pubblicata 2026-04-15 — **prima** della 11.0.3 stabile, non dopo. Il maintainer ha
continuato a rilasciare la serie 11.0.x dopo aver taggato tre alpha della serie 12.0.0, senza mai
tagliarne una stabile né pubblicarne una quarta: è un segnale concreto che la riga
Avalonia-12 potrebbe essere ferma, non solo giovane. Adottare questa sonda in produzione
significa pinnare un'alpha senza attività recente visibile, non una versione stabile in ritardo
di poche settimane.

### La sorpresa tecnica: compila, poi esplode

Il compito chiedeva di verificare la compatibilità *prima* di fare affidamento sul pacchetto.
`Markdown.Avalonia` 11.0.3 dichiara `Avalonia >= 11.0.0` (minimo inclusivo, non un range chiuso)
— NuGet lo risolve senza obiezioni contro 12.1.2, e **il progetto compila senza errori**. Il
primo `[AvaloniaFact]` che istanzia davvero il controllo però fallisce con:

```
System.MissingMethodException: Method not found: 'Avalonia.Data.IBinding
Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension.ProvideValue(System.IServiceProvider)'.
   at Markdown.Avalonia.StyleCollections.MarkdownStyleFluentTheme.!XamlIlPopulate(...)
   at Markdown.Avalonia.MarkdownStyle.get_FluentTheme()
   at Markdown.Avalonia.MarkdownScrollViewer..ctor()
```

Lo stile `FluentTheme` di `Markdown.Avalonia` è **XAML precompilato dentro la DLL 11.0.3** contro
la firma di `DynamicResourceExtension.ProvideValue` di Avalonia 11 — firma che Avalonia 12 ha
cambiato. Non è un vincolo di versione dichiarato in modo pigro nel `.nuspec`: è una rottura
binaria reale fra Avalonia 11 e 12, invisibile al compilatore (il riferimento che fallisce vive
dentro l'XAML-IL già compilato del pacchetto, non nel codice sorgente di questo progetto) e
visibile solo istanziando davvero il controllo — esattamente il tipo di guasto silenzioso che
questo repo ha già imparato a temere (vedi la nota sul pattern `InitializeComponent()` vs
`AvaloniaXamlLoader.Load(this)` nella sonda controlli). **Un solo tentativo di correzione**
(cambiare pacchetto, non aggirare l'errore): passare alla riga prerelease `12.0.0-a3`, la stessa
libreria ricompilata esplicitamente contro `Avalonia 12.0.0` + `net10.0` — compila **e** gira,
verificato dai due screenshot. `AvaloniaEdit` non ha avuto bisogno di questo giro: il pacchetto
corretto (`Avalonia.AvaloniaEdit`, **non** `AvaloniaEdit` — quel nome fermo alla 0.10.12 su
Avalonia pre-1.0 è un pacchetto diverso e vecchio) dichiara `Avalonia >= 12.0.0` ed è la build
reale del progetto AvaloniaUI stesso, non un fork di terzi.

### Architetturale

- Il documento smette di essere un unico oggetto editabile-e-visibile: diventano due controlli
  sincronizzati a mano (guardie di rientranza `_pushingToEditor`/`_pushingToPreview` nel
  code-behind, stesso principio del flag `applyingRemote` di `editor.js` ma senza bridge JSON di
  mezzo — è una sincronizzazione C#↔C# diretta, non un protocollo).
  `AvaloniaEdit.TextEditor` non ha una `StyledProperty` `Text` bindabile a due vie di serie
  (stesso limite storico di AvalonEdit su WPF) — la sincronizzazione passa dagli eventi
  (`Document.TextChanged`), non da un binding XAML diretto su `Markdown`.
- Nessuna toolbar di formattazione pronta: se si vuole recuperare anche solo in parte la comodità
  dei 13 comandi di Toast UI, va scritta a mano sopra le API di selezione/documento di
  AvaloniaEdit — lavoro reale, non incluso in questa sonda.
- `Markdown.Avalonia` non richiede alcun merge esplicito in `App.axaml` (a differenza di
  `FluentTheme`/`RecordumStyles.axaml`): il proprio stile di default si applica al controllo da
  solo, dentro il suo stesso assembly — un dettaglio comodo, non un vincolo.

## Cosa mostrano gli screenshot

`docs/design/2026-09-09-avalonia-editor-probe.md` (questo file) rimanda a
`tests/Recordum.Avalonia.Tests/bin/Debug/net10.0/linux-x64/Screenshots/editor-{light,dark}.png` —
un documento reale (titolo H1, paragrafo con **grassetto** e *corsivo*, una lista puntata, un
link, un blocco di codice C# con syntax highlighting) renderizzato correttamente in entrambe le
varianti tema, sorgente a sinistra e anteprima a destra nello stesso frame. Da notare guardando
gli screenshot: il pannello di anteprima passa da chiaro a scuro **da solo** — nessun codice di
questa sonda lo istruisce a farlo; è `MarkdownStyleFluentTheme` di Markdown.Avalonia che si
aggancia al `ThemeVariant` della finestra ospitante.

## Raccomandazione

**Viable con perdite nominate.** Lo strato tecnico funziona ed è persino più leggero in righe di
codice proprio (−19% sul controllo) — ma non è un porting 1:1: cambia il modello di editing
(WYSIWYG → sorgente+anteprima separati) per l'unico controllo che l'autore usa **ogni giorno**
per il proprio diario, e la dipendenza chiave (`Markdown.Avalonia` per Avalonia 12) è
un'alpha con segnali di stallo, non una libreria matura.

Le tre strade, in ordine di preferenza:

1. **Nativo (questa sonda), con un seguito esplicito**: adottare AvaloniaEdit+Markdown.Avalonia
   **solo se** accompagnato da (a) una toolbar minima di formattazione (almeno grassetto/corsivo/
   lista/link/code — i comandi più usati in un diario) per attenuare la perdita di comodità, e
   (b) una decisione consapevole su `12.0.0-a3`: pinnarla e monitorare l'upstream, o forkare/
   vendorizzare se lo stallo si conferma. È la strada scelta per questa sonda perché **rimuove
   davvero** WebView2 (l'obiettivo dichiarato della migrazione) e il costo tecnico è basso — ma
   "basso costo tecnico" non è "nessuna perdita di prodotto", ed è l'autore a dover decidere se
   la perdita WYSIWYG è accettabile per il proprio uso quotidiano.
2. **Tenere un webview su Avalonia** (esiste un controllo basato su CEF, es. famiglie
   `WebView.Avalonia`/CEF-Avalonia): preserverebbe il WYSIWYG e tutti gli asset esistenti
   (editor.js/html/Toast UI) senza modifiche, ma **vanifica il motivo della migrazione** — si
   scambia una dipendenza nativa pesante (WebView2, Windows-only in pratica) con un'altra
   (CEF, multipiattaforma ma enorme e con la propria complessità di packaging/aggiornamento) —
   non è "gratis" solo perché il codice esistente si porta senza modifiche.
3. **Non tentata qui**: un motore WYSIWYG nativo per Avalonia costruito da zero. Fuori scala per
   una sonda, e non risulta esistere una libreria matura da referenziare oggi.

La scelta finale è un compromesso prodotto/costo che spetta all'autore, non una conclusione
tecnica automatica — questa sonda dà i numeri e le due strade concrete, non decide al posto suo.
