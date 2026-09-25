# Avalonia come piattaforma — design della migrazione

> **Stato**: approvata in brainstorming il 2026-09-10. Il piano d'implementazione si scrive da questa spec.
>
> **Sostituisce** `2026-09-09-avalonia-linux-slice-design.md` e il suo piano, superati nello scope: descrivevano
> una *fetta verticale per Linux*, cioè un esperimento per decidere. La decisione è presa.

---

> **Emendamento 2026-09-25** — revisione in [`2026-09-25-avalonia-v1-plan-review.md`](2026-09-25-avalonia-v1-plan-review.md),
> con quattro decisioni dell'autore. (1) Prima di altro porting, un **Passo 0** di messa in sicurezza: SEC-1 non era mai arrivato in
> `main`; il bundle SQLCipher è deprecato e porta SQLite 3.39.2; lo sblocco può riscrivere il salt di un DB vero; su Linux il backup
> scrive un dump in chiaro in `/tmp` e l'auto-backup si spegne da solo. (2) **Dogfooding su Windows e su Linux** (D1): **F2 si
> allarga** — nota veloce, backup e restore incrociato fra le due macchine, e su Linux picker dei template e builder. (3) **Niente
> promemoria prima del dogfooding** (D2): la regola di convivenza di §7 resta intatta, E14 torna dopo F8. (4) **Merge in `main`
> adesso e a ogni onda chiusa** (D4), non più "a F8". (5) I piani da F2 sono fatti di **decisioni, interfacce e test** (D5), non di
> codice completo. Le Onde 2.9, 3, 4 e 5 e gli exit criteria riletti per Avalonia sono in §6-§7 della revisione; §14 di questa spec è
> superato: R9 è stato eseguito sulla VM l'11/09.

## 1. Cosa decide questa spec, e cosa non ri-discute

**Non ri-discute la scelta del framework.** Avalonia **è** la piattaforma: sostituisce la shell WPF e assorbe
la Fase 5 (MAUI) e la futura Web/PWA. La decisione è dell'autore, presa dopo quattro sonde di misura. Chi
legge questo documento cercando l'argomentazione la trova in `docs/design/2026-09-09-avalonia-*.md`,
`2026-09-10-avalonia-live-preview-probe.md` e nel riassunto di CLAUDE.md §7 — non qui.

**Questa spec struttura il lavoro.** Dice in che ordine si porta e perché quell'ordine, che ne è di WPF nel
frattempo e a quale condizione si spegne, dove finiscono i 126 file di ViewModel, cosa succede al motore dei
grafici e al runtime plugin della Fase 7, e cosa non si promette.

Il freeze del 2026-06-16 (*"nessuna nuova fase finché il desktop non è una v1.0 funzionante"*) resta sospeso
consapevolmente, come già per la workspace shell e per la linea Linux. La ragione è una scadenza reale:
**l'Onda 3 è coerenza estetica, cioè interfaccia pura** — farla su WPF sapendo di migrare butterebbe via una
fase intera.

## 2. Lo stato di partenza — la pila, e le misure

### 2.1 `avalonia-linux-slice` è una pila, non un ramo laterale

Fatto mai scritto prima d'ora, e decide l'ordine di tutto il resto. Il branch **contiene**, dal basso:

| Strato | Commit sopra `main` | Stato |
|---|---:|---|
| `wave-2-visual-pass` | 10 | pass visivo dell'Onda 2, **fermo a metà**, non mergiato |
| `r9-day-boundary` | 30 | R9 chiuso — **47 file sorgente WPF e 46 file di test (9 nuovi) mai eseguiti** |
| sonde Avalonia | 13 | le quattro misure, `Recordum.Avalonia` con 25 file |

`main` è allineato a `origin/main`. La migrazione di piattaforma è quindi appoggiata su due rami WPF non
atterrati, uno dei quali ha una suite mai girata (§14).

### 2.2 Le misure che il piano non deve ri-verificare

Tutte prese sul repo, non dedotte. Le prime cinque vengono dalle sonde del 2026-09-09/10; le altre da questa
sessione.

| Cosa | Misura |
|---|---|
| ViewModel | **126 file, 12.335 righe**; **6 agganci a WPF** (4 `DispatcherTimer`, 1 `Application.Current?.Shutdown()`, 1 `TryFindResource`) più un settimo, `HomeViewModel.ApplyDropAsync(…, TreeDragDropBehavior.DropPlan)` |
| Stili | delle **844** righe di `BaseStyles.xaml` attribuibili a un controllo, **~682 (81%) non hanno contropartita**: erano il costo di disfare Aero. Residuo scritto: **64 righe** |
| Controlli disegnati a mano | **nessun disegno immediato** in tutto `src/` (zero `OnRender`/`DrawingContext`). `HeatmapStrip`, il caso peggiore, 190 → 224 righe (**+6%** al netto dei commenti); gli altri sono porting diretti |
| Editor | il controllo cala del **19%** (219 → 177 righe); spariscono **704 KB** di Toast UI vendorizzato, il bridge JS↔.NET, le due tripwire tema/font e `IWebView2Availability` |
| Esperienza di scrittura | **Live Preview stile Obsidian raggiunta**: 358 righe, marcatori nascosti, grezzo sulla riga del cursore, provata contro il codice rotto |
| View | **47 view, 5.770 righe XAML + 1.331 di code-behind**. `HabitScreen.xaml` da solo ne pesa **1412, il 24%** |
| Il resto della shell | 37 converter (35 legati a `System.Windows`), 28 service (10 interfacce, 18 implementazioni, 7 legate), 4 behavior, 900 righe di `Bootstrap/`. I **5 controlli disegnati a mano** delle sonde sono `HeatmapStrip`, `YearHeatmapControl`, `StreakTimelineControl` e i due wrapper LiveCharts |
| Stringhe | **504 chiavi**, importate da **38** file di ViewModel |
| Test | **201 file** (esclusi gli `obj/`), di cui **23** nominano un tipo Windows → **178 liberi**. Dentro `ViewModels/`: **99 su 101** liberi, e i 2 che non lo sono sono esattamente la cucitura `DropPlan` |

## 3. Le quattro decisioni, chiuse

1. **Nome del progetto**: `Recordum.Avalonia` → **`Recordum.UI`**. Non fa più ombra al namespace radice
   `Avalonia` (niente `global::`), e non incorpora il framework nel nome — visto che Avalonia diventa
   *la* piattaforma, dirlo nel nome è informazione temporanea. Lascia spazio alla forma standard futura
   (`Recordum.UI` libreria + `Recordum.UI.Desktop` / `.Browser` come teste). **Si rinomina in `F0`, prima
   delle 47 view**: dopo costerebbe ogni file.
2. **Font**: **un serif imbustato nell'assembly** via `avares://`, una faccia sola identica su ogni sistema.
   Coerente col resto — il marchio è una R serif, il diario è deliberatamente serif, l'AppSetting `ui.font`
   ha `Serif` come default. Un ripiego per-OS farebbe sembrare l'app diversa su ogni macchina, che è
   esattamente ciò che l'Onda 3 vuole smettere di fare. **La spec fissa il meccanismo; la faccia si sceglie
   guardando.** Verificare la licenza di ridistribuzione prima di imbustare.
3. **Toolbar dell'editor**: **minima, dentro il porting** (`F4`) — grassetto, corsivo, lista, link, codice.
   Da tenere distinta dalla **copertura delle costruzioni markdown** (liste, citazioni, link, codice, task,
   tabelle), che è un buco reale verso Toast UI e va colmata nello stesso giro: costo lineare, nessun
   meccanismo nuovo, il difficile (nascondere senza perdere offset, l'eccezione del caret) è già risolto.
4. **Ordine rispetto al pass visivo dell'Onda 2**: **R9 atterra, il pass visivo passa su Avalonia** (§8).

## 4. `Recordum.Presentation` — la mossa che si ripaga da sola

Progetto **`net10.0`, zero UI**, referenziato da entrambe le shell finché ce ne sono due.

**Il guadagno non dipende dalla migrazione**: `Recordum.Presentation.Tests` porta **178 file di test fuori da
Windows**, quindi girano su Linux. Il debito "la suite WPF non è eseguibile qui" si riduce ai 23 file che
toccano davvero WPF. Varrebbe la pena farlo anche se Avalonia non esistesse.

### Il confine, e perché non si può sbagliare

La regola è una sola — **tocca `System.Windows` → resta nella shell** — e **la impone il compilatore**, perché
il progetto è `net10.0` senza `-windows`. Non c'è modo di sbagliare in silenzio.

| Cartella | Va in `Recordum.Presentation` | Resta nella shell |
|---|---|---|
| `ViewModels/` | tutti i 126 file | — |
| `Resources/` | `Strings.resx` / `Strings.it.resx` / `Strings.Designer.cs` | i 5 dizionari XAML (`BaseStyles`, `Tokens`, `Light`, `Dark`, `Icons`, `WindowChromeStyles`) |
| `Services/` | le 10 interfacce + le implementazioni pure (`FavoriteColorStore`, `RecentColorStore`, `DebouncedColorRecorder`, `HsvColor`, `ShadeRamp`, `IconCatalog`, `TimerRecoveryService`, `BackupSchedulerRunnerAdapter`) | le 7 `Wpf*` più `ScreenColorPicker`, `MessageBoxConfirmationDialog`, `UiEventSubscriptionExtensions` |
| `Bootstrap/` | `ApplicationInitializer`, `ServiceRegistrar`, `RecordumOptions`, `DemoDataSeeder`, `BackupSchedulerHost`/`Options` | `CultureBootstrap`; `IWebView2Availability`, che **muore** con `F4` |
| `Formatting/` | `CounterTotalDisplay`, `DurationFormat` | — |
| `Behaviors/` | il solo `DropPlan` + `ResolveDrop` (valore puro + `switch` su enum) | il behavior vero, che maneggia eventi |
| `Converters/` | — | tutti e 37 |

### Come si esegue

Il namespace diventa `Recordum.Presentation.*` con una sostituzione meccanica su `src/` e `tests/`. È rumoroso
— tocca ~350 file — ma **verificabile da un comando, non da un'opinione**: o compila o no.

La vecchia spec rimandava proprio questo, e aveva ragione allora: *"spostare 126 file citati da 872 test dentro
il branch che deve diventare v1.0 renderebbe impossibile capire quale delle due cose ha rotto cosa"*. Quella
ragione è scaduta il giorno in cui il porting **è** il lavoro invece di una fetta esplorativa accanto ad esso.
Resta però la sua metà valida, ed è il motivo di §14: si estrae **sopra un R9 già atterrato e con la suite
verde**, mai sopra 46 file di test mai eseguiti.

**Nota operativa**: `Strings.Designer.cs` è un designer RESX **mantenuto a mano, non generato da EF**, e
`guard-generated.js` lo blocca per pattern (`*.Designer.cs`) — quindi va editato via shell. Il punto cieco
trasloca con il file: la nuova posizione va scritta in CLAUDE.md §8 nello stesso commit, o si riscopre a metà
porting.

## 5. `Recordum.UI` — la shell

`net10.0` + Avalonia 12.1.2. Ospita view, controlli, stili, converter, e i pochi servizi che toccano il
sistema. Non referenzia mai `Recordum.WPF`.

I 25 file già scritti dalle sonde (token, `RecordumStyles.axaml`, `HeatmapStrip`, editor + Live Preview,
quattro view di sonda) si tengono: sono codice vero, non impalcatura. Le **view di sonda** invece sono
impalcatura e si cancellano quando la view vera che le sostituisce esiste.

**Decisione di design che segue dalla sonda tema**: dove il default Fluent **coincide** col linguaggio che
Recordum si era costruito a mano su WPF — sottolineatura di selezione sui tab, thumb circolare dello slider,
chevron rotante dell'Expander, popup arrotondati con ombra — **si adotta il default invece di reimplementarlo**.
Non erano scelte di Recordum: erano design moderno che WPF non aveva e che andava costruito a mano. È l'81%.

## 6. L'ordine di porting, e il suo criterio

**Il criterio**: l'app Avalonia diventa usabile per l'uso quotidiano dell'autore il prima possibile. Così il
porting smette di essere un atto di fede e diventa una cosa che si controlla ogni giorno. Front-carica anche il
rischio, perché Home e registrazione sono le schermate più intrecciate.

**Vincolo strutturale che attraversa le onde**: `HabitScreen.xaml` è **1412 righe** e ospita cinque colonne
(Oggi, Registrazioni, Diario, Statistiche, Configura) più i `DataTemplate` quick-add nelle proprie risorse.
**Non si porta in un colpo**: la sua shell nasce in `F2` e le colonne si aggiungono una per onda. È lo stesso
motivo per cui l'Onda 2.5 non estrasse quei template — con la differenza che qui non si estrae, si riscrive.

| Onda | Cosa | Dopo, l'app Avalonia… |
|---|---|---|
| **F0** Fondamenta | `Recordum.Presentation` (§4); rinomina in `Recordum.UI`; font imbustato; token + i 64 righe di stile; i 3 controlli non-grafico (`HeatmapStrip` già fatto, `YearHeatmapControl`, `StreakTimelineControl`) | …non fa ancora niente di nuovo, **ma 178 file di test girano su Linux** |
| **F1** Home | `MainWindow` + chrome della finestra **riscritto** (`ExtendClientAreaToDecorationsHint` + `BeginMoveDrag`: ~130 righe, API concettualmente diversa, **zero riuso letterale**); `WorkspaceView`, `WorkspaceRibbon`, `HomeView`, `HabitRowTemplate`, `GroupRowTemplate`, `EmptyState`. Cuciture: `ITickSource` (`HomeClockTicker`) e `DropPlan` consumato dal drag-drop Avalonia | …apre il DB cifrato vero, mostra l'albero, la riga Home vive |
| **F2** Registrazione ⟵ **soglia di dogfooding** | shell di `HabitScreen` + colonna Oggi (`TodayStripView`, le 5 view per tipo, `TodayContentTemplates`), i quick-add dei 5 tipi, `TimerStatusBar` | …**registra**. Il dogfooding migra qui |
| **F3** Dettaglio | colonna Statistiche (heatmap anno, timeline streak, goal, **senza** i due grafici LiveCharts) e colonna Registrazioni (`CalendarNavigator`, `DayNoteView`, registrazioni astinenza) | …si può guardare indietro |
| **F4** Diario | `NotesTabView`, `JournalQuickEntryView`, editor con Live Preview + costruzioni markdown mancanti + toolbar minima | …**muoiono** 704 KB di Toast UI, il bridge JS, `IWebView2Availability` |
| **F5** Configura + builder | `EditorPane` + le 13 section view + `SectionTemplates`; `HabitBuilderView` + `BuilderPreviewPane`; i quattro picker (colore, HSV, icona, template) | …compone e configura abitudini |
| **F6** Impostazioni, backup, export | `SettingsWindow`, i 3 dialog di backup, `ExportDialog`; i dialog di file su `StorageProvider`; l'equivalente di `CultureBootstrap`; la chiave del DB senza DPAPI | …è completa tranne i grafici |
| **F7** Grafici | lo spezzamento del `ChartEngine` (§9), i due wrapper, `ChartsView`, il builder dei grafici | …ha parità di funzioni |
| **F8** Spegnimento | si cancellano `src/Recordum.WPF` e `tests/Recordum.WPF.Tests`; il renderer WPF del ChartEngine; CLAUDE.md §3/§4 riscritte | …è l'unica |

**I converter viaggiano con la prima view che li usa**, non in blocco: portarne 37 prima che una view li chieda
è lavoro morto che nessun test copre.

## 7. Che ne è di WPF, e quando si spegne

**Durante la transizione WPF è la shell che spedisce**, ed è quella su cui gira il dogfooding fino a `F2`.
È **congelata**: si tocca solo per difetti trovati dogfoodando.

**Come si applica una correzione durante il congelamento:**

- il difetto è nella logica → si corregge in `Recordum.Presentation`, e **lo prendono entrambe le shell**;
- il difetto è nel *chrome* di una schermata WPF che sta per morire → **si registra a tracker e non si corregge**.
  È lo stesso ragionamento con cui l'Onda 3 è stata spostata: rifinire una superficie che si sta cancellando è
  il lavoro che si butta via due volte.

**La regola di convivenza si inverte** rispetto alla vecchia spec. Non più *"le feature nuove si fanno solo su
WPF e Avalonia insegue"*, ma: **nessuna feature nuova su nessuna delle due finché la piattaforma non è una sola.**
Il motivo della vecchia regola — evitare che il "due volte" diventi permanente per distrazione — vale ancora,
e con una shell in via di cancellazione la forma giusta è il congelamento.

**Criterio di spegnimento** (una lista, non una sensazione). WPF si cancella in un commit quando:

1. `F1`–`F7` sono chiuse;
2. il pass visivo dell'Onda 2 è stato rifatto sulle schermate Avalonia e i findings `W2V-*` ancora aperti sono
   stati ri-controllati lì;
3. `Recordum.Presentation.Tests` e i test di `Recordum.UI` sono verdi, e **nessuno dei 23 test WPF-bound prova
   ancora qualcosa che Avalonia non provi** — quelli che provano qualcosa si riscrivono headless, gli altri
   muoiono col codice che coprivano;
4. il dogfooding gira su Avalonia da almeno due settimane senza che sia servito tornare a WPF;
5. i criteri d'uscita v1.0 sono stati valutati sulla shell Avalonia.

Non si tiene "per sicurezza": tenerla significa ogni correzione due volte, per sempre, per una persona sola.

## 8. Il pass visivo dell'Onda 2, e il dogfooding

**R9 deve atterrare**, e prima di tutto il resto: è correttezza dei dati, è il prerequisito di E14 e del
dogfooding, e ha 46 file di test mai eseguiti (§14). Va sulla VM, suite verde, merge in `main`.

**La metà restante del pass visivo passa su Avalonia.** Ciò che resta da guardare — §3 registrazione, §1
Configura "Riga Home", §2 colonne default, §4 journal, più i quattro residui del builder — è verifica di
schermate WPF che stanno per essere sostituite: verificarle due volte è lo stesso spreco che ha fatto spostare
l'Onda 3. I findings `W2V-*` restano aperti a tracker e si ri-controllano sulle schermate portate, alle onde
corrispondenti (`F2` per §3, `F5` per §1 e il builder, `F1`/`F2` per §2, `F4` per §4).

Va detto senza ammorbidirlo: **il pass visivo trova anche difetti di logica, non solo di resa** — `W2V-1`
(cultura) e `W2V-6` (eventi astinenza) erano bug veri, già corretti sulla pila. Spostare il pass su Avalonia
sposta la scoperta di quelli non ancora trovati **più avanti nel tempo**, e li fa emergere su una shell nuova
dove la causa è più ambigua. È il costo accettato in cambio di non rifinire due volte.

**Il dogfooding (E21) non aspetta il porting.** Parte su WPF, sulla VM, appena R9 atterra — come già
pianificato — con la disciplina dati già scritta (solo build di `main`, backup prima di ogni build nuova,
migrazioni provate su copia). **Migra su Avalonia a `F2`**, che è il motivo per cui `F2` sta dove sta.

## 9. Il motore dei grafici

Non è un porting, è un'operazione architetturale. `Recordum.Plugins.ChartEngine` oggi è `net10.0-windows` +
`UseWPF`, ma **la mappatura serie→modello è già priva di dipendenze WPF**: usa solo tipi di `LiveChartsCore`.
Il punto di rottura sono **due righe** in `CreateView`, che istanziano i tipi `…SkiaSharpView.WPF`.

Si spezza in:

- **`Recordum.Plugins.ChartEngine`** (`net10.0`, nessuna UI) — la mappatura, i paint, la registrazione della
  capability via `IExtensionPoint`;
- **due renderer sottili**, uno per shell, che fanno solo `CreateView`.

Il renderer WPF si cancella a `F8`. **Lo spezzamento resta**: una mappatura senza UI è ciò che permetterebbe a
un renderer web futuro di riusarla, ed è la stessa disciplina degli strati che ha reso possibile questa
migrazione (§2.2).

`F7` è anche il punto in cui si incrociano i findings già aperti sui grafici (`F0-*` dell'Onda 0, `W1a-VF1`,
`A4`, `W23-VF7`): **non si porta un grafico rotto** — se una superficie è già dichiarata rotta a tracker, o si
chiude lì o si dice esplicitamente che resta rotta anche dopo.

## 10. L'editor markdown

**La Live Preview è la strada**, non la source mode: resa mentre si scrive, marcatori nascosti, sintassi grezza
solo sulla riga del cursore. 358 righe già scritte e provate contro il codice rotto.

**Si chiude il Ruling 6, rimasto "da proporre all'autore" nel ledger**: `Markdown.Avalonia 12.0.0-a3` **esce
dalle dipendenze**. Era l'unico rischio serio della sonda editor — un'alpha pubblicata *prima* della stabile e
ferma da allora, tenuta solo perché serviva un pannello di anteprima. **La Live Preview non ha un pannello di
anteprima**, quindi la dipendenza esce dal percorso critico da sola. Se un domani servirà la Reading view, si fa
con **Markdig** (parser .NET standard, zero dipendenze UI) più un renderer nostro.

`AvaloniaEdit` resta: pacchetto `Avalonia.AvaloniaEdit` (**non** `AvaloniaEdit`, fermo a 0.10.12 su Avalonia
pre-1.0), dichiara `Avalonia >= 12.0.0`, è la build del progetto AvaloniaUI stesso.

Con `F4` spariscono 704 KB di asset vendorizzati, il bridge JS↔.NET, le due `DependencyProperty` tripwire per
tema e font, il fallback di disponibilità — e **la causa radice di C1 (Onda 2.4) e W24-Q1 (Onda 2.5)**. La
classe di difetto (un `ContentControl` con `ContentTemplate` statico materializza il template anche a `Content`
null) **non sparisce necessariamente** — è un footgun generico di templating, da ri-verificare su Avalonia — ma
la posta in gioco crolla: da "un processo browser fantasma che copre l'interfaccia e che WPF non può
sovradisegnare" a "qualche oggetto .NET sprecato".

## 11. Fase 5, Web, runtime plugin: cosa questa migrazione abilita e cosa non fa

- **Fase 5 (mobile)** e **Web/PWA**: Avalonia le rende *possibili dallo stesso codice di interfaccia*, ed è la
  ragione per cui la migrazione si fa adesso invece che dopo la v1.0. **Non sono pianificate, non hanno una
  data, non entrano in questa spec.** Il ponte dati resta `.recbak`, come la roadmap già prevede.
- **Runtime plugin della Fase 7**: vuole ancora un webview. Su Avalonia significa CEF o i controlli per-OS.
  Vale la correzione già registrata: **non serve che la shell sia un webview perché i plugin lo siano** — WPF
  fa esattamente questo oggi, con WebView2 dentro una colonna. L'architettura della Fase 7 non cambia; cambia
  quale pacchetto webview si sceglie, e la scelta si fa quando ci si arriva.
- **Sync e schema**: non si toccano. Lo schema resta 2.18.

## 12. Verifica, e il suo limite

**I test d'interfaccia girano su Linux**, con `Avalonia.Headless.XUnit` e il backend Skia: un `[AvaloniaFact]`
renderizza un frame vero e lo salva come PNG. Si può **guardare** una schermata senza che sia l'autore a
mandarne una — che finora è stato l'unico occhio del progetto.

**Il limite va scritto perché è già costato una volta**: un assert sul peso del file **non certifica cosa mostra
l'immagine**. La sonda Live Preview ha prodotto uno screenshot vuoto con un test verde. La forma giusta è quella
che quella sonda ha poi adottato — una **prova strutturale** accanto allo screenshot (se un marcatore è
nascosto, la riga visuale è più corta di quella del documento) — più la regola già in CLAUDE.md §8: **un test di
regressione va provato contro il codice rotto**.

**Cosa resta all'autore**: aprirla e dire se *sembra Recordum*. Quello non lo sostituisce un PNG.

## 13. Le cuciture, tutte e sette

Elencate qui perché non vengano riscoperte una per volta.

| # | Aggancio | Dove | Quando | Come |
|---|---|---|---|---|
| 1-4 | `DispatcherTimer` in `HomeClockTicker`, `LiveElapsedClock`, `AbstinenceQuickAddViewModel`, `ChartBuilderViewModel` | ViewModel | `F0`/`F1` | `ITickSource` in Core, due implementazioni. Avalonia ha `DispatcherTimer` con lo stesso nome in `Avalonia.Threading`, quindi la seconda è una riga. Guadagno collaterale: il timer diventa sostituibile nei test, che oggi lo saltano perché `Application.Current` è null |
| 5 | `Application.Current?.Shutdown()` | `MainWindowViewModel:144` | `F1` | interfaccia di applicazione, o un callback iniettato |
| 6 | `TryFindResource("Brush.Danger")` | `HabitScreenViewModel:963` | `F2` | il ViewModel non deve conoscere i brush: restituisca un token, sia la view a risolverlo |
| 7 | `TreeDragDropBehavior.DropPlan` | `HomeViewModel:244` | `F0` | `DropPlan` e `ResolveDrop` sono valore puro e `switch` su enum: traslocano in `Recordum.Presentation`, il behavior resta nella shell. Libera anche gli ultimi 2 test di ViewModel |

## 14. Il debito verso la VM, nominato invece che ereditato

La pila di §2.1 porta **47 file sorgente WPF e 46 file di test (9 nuovi, 37 modificati) che non sono mai stati
eseguiti** — i test di R9 e quelli delle cuciture compilano e basta, perché la suite WPF non gira su Linux.

**Non è debito del porting: è debito di R9.** Le due cose che la prima stesura di questa spec metteva
insieme — *eseguire* la suite di R9 e *mergiare* R9 in `main` — sono separabili, e solo la prima tocca il
porting (riformulato il 2026-09-10, dopo i Task 1-3 dell'onda `F0`):

- **Blocca il Task 7, e solo lui**: una singola esecuzione della suite WPF di R9 sulla VM, serializzata
  (`-- xUnit.ParallelizeTestCollections=false`, gotcha W24-E1). La ragione è precisa e non è la
  diagnosticabilità: è che **il Task 7 rinomina 126 file**, e far atterrare eventuali correzioni di R9
  *attraverso* una rinomina di quella taglia è l'unico scenario davvero brutto dell'onda. Una esecuzione
  dice se quel rischio esiste.
- **Non blocca niente**: gli step "rosso-prima" dei task WPF di R9, la verifica a schermo, e il merge in
  `main`. Si fanno quando conviene.

**La diagnosi non è ambigua come la prima stesura temeva.** L'estrazione è meccanica: sposta file e
rinomina namespace, non cambia comportamento. Dopo il Task 7 un **errore di compilazione** è
dell'estrazione, un **assert che fallisce** è di R9 — si distinguono per il tipo, senza bisogno di un
baseline verde. E c'è un rovescio che vale la pena dire: dopo il Task 8, **cinque dei nove test nuovi di
R9 girano su Linux per la prima volta**, quindi l'estrazione *riduce* il debito di R9 invece di esserne
bloccata.

**Rischio dichiarato per la verifica a schermo di R9**, da dire prima e non da scoprire: passando da UTC a
locale lo storico si **ri-interpreta**, e un numero di streak può cambiare al primo avvio. È corretto, non
è una regressione.

Il comando pronto per la VM sta in [`docs/dev-environment-sync.md`](../../dev-environment-sync.md), sezione
*"Handoff per la VM — onda F0"*.

I 9 test nuovi mai eseguiti: `NumericInputRuleTests`, `CultureBootstrapTests`, `SettingsCultureViewTests`,
`CultureServiceTests`, `DetailDayBoundaryTests`, `TodayStripDayBoundaryTests`, `GeneralSettingsCultureTests`,
più le due fixture `FakeResetTimeResolver` e `TestTimeZones`.

## 15. Ambiente: le trappole già pagate

Da non ripagare. Le prime tre sono misurate; la quarta è la loro morale.

1. **`Avalonia.Headless.XUnit` dipende da xunit.v3**, non dal 2.9.3 del resto del repo, e un progetto v3
   **pretende un apphost**. Per questo `UseAppHost=false` vive in `Directory.Build.props` (property
   sovrascrivibile) e **non va mai passato da riga di comando**, dove è una global property che vince sempre e
   rompe quella build.
2. **In headless il primo `AvaloniaEdit` della sessione non fa il layout**: `TextView` resta 0×0 e lo screenshot
   esce vuoto — **e il test passa lo stesso** se l'assert guarda il peso del file. Si prima con una vista
   *diversa*, poi `ForceRenderTimerTick()` + `RunJobs()`. Primare con la **stessa** vista non funziona: provato.
3. **`InitializeComponent()`, mai `AvaloniaXamlLoader.Load(this)`**: il secondo carica lo XAML ma **non cabla i
   campi `x:Name`**. Silenzioso fino a un `NullReferenceException` a runtime. Vale per ogni view e ogni
   controllo con almeno un elemento nominato — cioè per quasi tutte le 47.
4. La morale comune alle tre: **su questo stack un guasto si presenta come un verde, non come un rosso.** Un
   test d'interfaccia che non può fallire è peggio di nessun test.

## 16. Cosa questa spec NON promette

- **Non promette che la v1.0 arrivi prima.** Arriverà più tardi in calendario, su una piattaforma che
  sopravvive. È il baratto che la decisione ha già accettato.
- **Non promette che il porting sia invisibile.** Il `DatePicker` Fluent è un *pattern d'interazione diverso*
  dal dropdown-calendario di WPF, non lo stesso controllo con altri colori; il chrome della finestra si
  riscrive con un'API concettualmente diversa; l'editor cambia modello (da WYSIWYG a Live Preview). Ognuna è
  una decisione di prodotto da vedere a schermo, non uno styling.
- **Non promette che il porting sia meccanico.** Lo è per i ViewModel — sei righe su 12.335 — e per gli stili,
  dove l'81% non ha contropartita. **Non** lo è per le 47 view, che si riscrivono.
- **Non promette parità visiva riga per riga**: dove il default Fluent coincide col design che WPF costruiva a
  mano, si adotta Fluent (§5).
- **Non promette mobile né web.** Li rende possibili; non li pianifica.
- **Non risolve il runtime plugin della Fase 7**: lo lascia dov'era, con una scelta di pacchetto in più da fare
  quando ci si arriverà.
- **Non tocca schema, sync, né i dati.** Lo schema resta 2.18, il ponte fra macchine resta `.recbak`.

## 17. Come si esegue questa spec

**Questa spec è la mappa, non un piano.** `F0`–`F8` è troppo grande per un solo piano d'implementazione, e
fingere il contrario produrrebbe un documento che nessuno può eseguire.

**Ogni onda ha il suo piano, scritto dalla sua sezione di questa spec, a ridosso dell'esecuzione** — lo stesso
metodo delle onde della v1.0 (*"i piani delle onde successive si scrivono a ridosso dell'esecuzione, ciascuna
dalla propria spec"*). Scriverli tutti adesso significherebbe pianificare `F7` sulla base di ciò che oggi si
crede di `F1`.

**Il piano che si scrive ora è `F0`**, e solo quello. È l'onda giusta da cui partire perché è l'unica il cui
valore **non dipende dall'esito della migrazione**: l'estrazione di `Recordum.Presentation` libera 178 file di
test da Windows anche se tutto il resto si fermasse domani.

**Prerequisito bloccante di `F0`**: §14 — R9 atterrato in `main`, con la suite verde sulla VM. Il piano di `F0`
lo dà per fatto e lo dichiara nella propria sezione dei vincoli; non lo esegue.

Ogni onda si chiude come le altre di questo repo: review di branch (mai solo per-task — le due review di branch
dell'Onda 2.4 e 2.5 hanno trovato quattro Critical che le review per-task, cieche fuori dal proprio diff, non
potevano vedere), suite verde, e per le onde da `F1` in poi una verifica a schermo.

## 18. Domande lasciate aperte, deliberatamente

1. **La faccia del serif da imbustare** — il meccanismo è deciso (§3.2), la faccia si sceglie guardando, con la
   licenza di ridistribuzione verificata.
2. **Come si sblocca il DB a regime su Linux.** DPAPI è Windows-only, quindi la strada è `PassphraseKeyProvider`
   (Argon2id), che esiste dal 2026 e non è mai stata la strada principale di nessuno. Per un utente vero si
   valuterà il portachiavi di sistema (libsecret) o una sessione ricordata. Da decidere entro `F6`.
3. **Se Avalonia abbia l'equivalente del difetto `FrameworkElement.Language`.** WPF non legge
   `CultureInfo.CurrentCulture`: ogni binding formatta e interpreta attraverso `Language`, il cui default di
   metadato è `en-US` — da lì venivano le date americane in un'app italiana (`W2V-1`). **Non è noto se Avalonia
   si comporti allo stesso modo**, e la risposta cambia se `CultureBootstrap` va portato o cancellato. È una
   sonda da fare presto, non un'assunzione: il primo task che tocca una data la deve chiudere.
4. **Packaging su Linux** (AppImage, Flatpak, `.deb`) e la sua interazione con Velopack, che l'Onda 4 prevede
   per Windows. Non prima di `F6`.
5. **`IScreenColorPicker`** è Win32 isolato. Su Linux vuole X11/Wayland, e Wayland in particolare non consente a
   un'applicazione di leggere il pixel sotto il cursore senza un portal. Può restare una capacità solo-Windows
   dietro l'interfaccia, che è già nullable. Da decidere a `F5`.
