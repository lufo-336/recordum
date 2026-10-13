# Onda F2b — Creare e configurare su Avalonia — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dopo F2b, su Avalonia (quindi su Linux, la casa dei dati del dogfooding, D11) si **crea** un'abitudine — dal
picker dei template o dal builder con l'anteprima viva — e la si **configura** dopo, dalla colonna Configura della
schermata dell'abitudine con le sue sezioni.

**Architecture:** Come in F2a i ViewModel esistono in `Recordum.Presentation` (`HabitTemplatePickerViewModel`,
`HabitBuilderViewModel` coi suoi mattoni e l'anteprima, `HabitEditorViewModel` con le sezioni) e la composizione Avalonia
li risolve: F2b scrive le **view** e le **due finestre** dietro `IHabitTemplatePickerLauncher` e `IHabitBuilderLauncher`,
che oggi in Avalonia sono implementazioni Null. La schermata dell'abitudine passa da una colonna a **due** (Cronologia e
Configura), quindi arrivano anche il layout a più colonne, il ridimensionamento e la modalità focus che F2a aveva
rimandato.

**Tech Stack:** C# / .NET 10; Avalonia 12.1.2 + `Avalonia.Headless.XUnit` (xunit.v3); `Recordum.Presentation` e
`Recordum.Data` con xunit 2.9.3; `CommunityToolkit.Mvvm` 8.4.

**Spec:** [`docs/superpowers/specs/2026-09-10-avalonia-as-the-platform-design.md`](../specs/2026-09-10-avalonia-as-the-platform-design.md)
§6 (riga F5: `EditorPane` + le section view + `SectionTemplates`, `HabitBuilderView` + `BuilderPreviewPane`, i quattro
picker); [`docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md`](../specs/2026-09-25-avalonia-v1-plan-review.md)
§6 Passo 3 (per Linux il picker dei template e il builder, perché senza non si crea la prima abitudine) e criteri E6/E7
(template che si spiegano da soli, builder con anteprima viva); `docs/DOGFOODING.md`; CLAUDE.md §2 principio 8 (template =
composizione salvata, `BuilderTemplateParityTests`). **Perimetro deciso dall'autore il 2026-10-13**: creazione **e**
Configura completa (anticipata da F5). Forma del piano (D5): decisioni, interfacce, obblighi di test, trappole; la view
WPF indicata è il riferimento di comportamento, non di markup.

## Global Constraints

- **Branch**: `f2b-creation`, creato da `f2a-registration` (pila non mergiata: `f2a-registration` ← `f2-perf1-home` ←
  `avalonia-linux-slice` ← `main`). Mai commit su `main`; cambiare branch in una chiamata separata (hook `guard-main.js`).
  Aggiornato un `.md` → commit e push subito.
- **Ambiente Linux**: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`. Build:
  `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo`. **Mai `-p:UseAppHost=false`**.
- **Baseline** a `2fd0509`: Core **150**, Data **544** (`--filter "Category!=WindowsOnly"`), Plugins **66**, ExportEngine
  **37**, Presentation **974**, UI **256** (`--filter "Category!=Performance"`). Si controlla il totale eseguito.
- **La suite UI si prova in due lingue** prima di ogni push: `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8` e
  `LANG=it_IT.UTF-8 LC_ALL=it_IT.UTF-8` (F2a: la CI è rimasta rossa cinque push per un test che dipendeva dalla cultura del
  sistema). Ogni test che confronta date, numeri o etichette localizzate fissa la cultura con `TestCulture.Pin()`
  (`tests/Recordum.UI.Tests/Fixtures/TestCulture.cs`).
- **La CI si guarda a ogni push**, non solo al primo.
- **WPF è congelato**. Le correzioni in Presentation e Data valgono anche per WPF, ed è voluto se sono difetti di dati o
  di comportamento.
- **Presentation non nomina tipi UI**; un'astrazione nuova vuole due implementazioni e un test della registrazione DI.
- **Il parametro muto** (CLAUDE.md §7): un parametro nuovo opzionale su un servizio o un ViewModel condiviso è il difetto
  già pagato tre volte in F2a. Si preferisce obbligatorio; se resta opzionale, un test dalla composizione vera deve
  fallire quando manca.
- **Avalonia**: `InitializeComponent()`; `x:DataType` ovunque; `[AvaloniaFact]`; `using Xunit;`; dati su DB privato
  (`tests/Recordum.UI.Tests/Fixtures/PrivateDbHost.cs`), mai sul DB condiviso di `ShellFixture`; finestre chiuse in
  `finally`; tick e orologi finti. Un `ContentControl` con `ContentTemplate` statico materializza il template a `Content`
  null: i rami per tipo con un `IDataTemplate` (vedi `QuickAddTemplate`, `RecordsTemplate`).
- **Un difetto di disegno o di formattazione si misura** (pixel letti dal frame, `Rgba8888`; testo reso; valore salvato).
- **Ogni test di regressione si prova contro il codice rotto**; `git checkout` non ripristina un file non tracciato.
- **Template = composizione salvata** (CLAUDE.md §2.8): `BuilderTemplateParityTests` resta verde; nessun template nuovo.
- **Stringhe**: `Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` **solo via shell**. Le chiavi sono puntate
  (`Calendar.Previous` → accessor `Calendar_Previous`). Molte esistono già: cercarle prima.
- **Linguaggio visivo** (CLAUDE.md §11): icone Lucide in `Themes/Icons.axaml`, mai glifi o emoji; i pulsanti di sola
  icona hanno `AutomationProperties.Name` e `ToolTip.Tip`; chiaro e scuro curati uguali; editor a tre livelli (sempre
  visibili nome, tipo, colore, icona; sezioni chiuse; opzioni rare dietro "Avanzate").
- **`TreatWarningsAsErrors`** globale; nei progetti di test nessun warning nuovo nei file toccati.

## Decisioni prese scrivendo il piano

L'esecutore non le ri-discute; sono qui perché l'autore possa disfarle **prima** dell'esecuzione.

1. **Due finestre modali vere** per il picker dei template e per il builder, dietro i due launcher, con le decorazioni di
   sistema su tutti e due i sistemi (il chrome nostro resta solo alla finestra principale, D7). Al successo il launcher
   torna l'id della nuova abitudine; la Home ricarica e la seleziona (già così in `HomeViewModel.OpenNewHabitAsync`), e la
   selezione apre il tab.
2. **Si riaccendono le porte d'ingresso spente in F1** (F1-RAIL1): la voce "Nuovo → Abitudine" della Home e il pulsante
   "Crea la tua prima abitudine" dello stato vuoto (con il binding "solo su Attive"). Le guardie di F1 che ne asserivano
   lo spegnimento si capovolgono.
3. **Configura è la seconda colonna** della schermata. Arrivano quindi: il layout "fill-then-scroll" (le colonne aperte si
   dividono la larghezza, sotto 320 px ciascuna scorre in orizzontale, `OpenColumnsMinWidth`), il ridimensionamento con la
   maniglia sul bordo destro di ogni colonna (doppio clic = larghezza di default, persistito come in WPF:
   `SetColumnWidth`/`ResetColumnWidth`/`PersistColumnWidthsAsync`) e la modalità focus (`ToggleFocusCommand`,
   `ExitFocusCommand`). Il chip "Configura" si riaccende; gli altri chip spenti restano spenti fino alla loro onda.
4. **Le sezioni di Configura sono dodici** (tredici in WPF): Programma, Serie, Obiettivo, Orario di reset, Registrazione, Riga Home,
   Metriche, Scale, Cronometro, Tag, Note, Condivisione AI — tranne **Notifiche**, che non si porta finché non arriva E14
   (D2). Ogni sezione dentro un expander chiuso; nome, descrizione, tipo, colore e icona sempre visibili in testa.
5. **Il contagocce del colore non c'è su Avalonia**: `IScreenColorPicker` è Windows-only e in Avalonia è Null; il
   pulsante non si mostra (voce nel tracker se serve dopo).
6. **I quattro difetti di Configura e builder noti in WPF si evitano per costruzione**, con una prova ciascuno: A8 (le
   sezioni sbordano dalla colonna: a 320 px nessun controllo esce dal bordo), A12 (l'autosalvataggio non si percepisce: lo
   stato "Salvataggio… / Salvato" resta visibile in fondo alla colonna, fuori dallo scorrimento), W2V-9 (i ToggleButton del
   builder non tematizzati: usano i nostri stili, accesi nel colore d'accento), W2V-19 (l'anteprima "In Home" è piatta: usa
   la stessa striscia per tipo della Home vera, `StripVisual`). W2V-16, W2V-18 (etichette e ordine dei mattoni) si
   ricontrollano a schermo; W2V-11 (unità e scale non modificabili) è una funzione mancante e resta nel tracker.
7. **Gruppi**: un gruppo appena creato dalla Home apre il suo tab con Configura (già fa così `OnHomeGroupCreated`): in
   Configura si rinomina e si ricolora. Questo chiude anche F1-5 (la riga gruppo offriva "Rinomina" senza casella) nel
   senso che la rinomina di un gruppo ha di nuovo una superficie; la voce del menu di riga resta come in WPF.
8. **Il cambio dell'orario di reset reinterpreta lo storico** (CLAUDE.md §5: `reset_time` non è versionato): Configura
   non lo cambia in silenzio. Se la sezione WPF ha già un avviso lo si porta; se non lo ha, si aggiunge una riga di testo
   che lo dice, sotto il campo.

## Struttura dei file

| File | Responsabilità | Task |
|---|---|---|
| `src/Recordum.UI/Controls/ColorSwatchPicker.axaml(.cs)`, `HsvColorPicker.axaml(.cs)`, `IconPicker.axaml(.cs)` | i picker condivisi da builder ed editor | 1 |
| `src/Recordum.UI/Views/HabitBuilder/HabitBuilderView.axaml(.cs)`, `src/Recordum.UI/Services/AvaloniaHabitBuilderLauncher.cs` | il builder e la sua finestra | 2 |
| `src/Recordum.UI/Views/HabitBuilder/BuilderPreviewPane.axaml(.cs)` | l'anteprima viva | 3 |
| `src/Recordum.UI/Views/HabitTemplatePickerWindow.axaml(.cs)`, `src/Recordum.UI/Services/AvaloniaHabitTemplatePickerLauncher.cs`, `HomeView.axaml`, `EmptyState.axaml` | il picker, il suo launcher, le porte d'ingresso | 4 |
| `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml(.cs)` | due colonne, ridimensionamento, focus | 5 |
| `src/Recordum.UI/Views/HabitEditor/EditorPane.axaml(.cs)`, `SectionTemplate.cs` | testata, host delle sezioni, stato | 6 |
| `src/Recordum.UI/Views/HabitEditor/Sections/*` | le dodici sezioni | 7, 8 |
| `tests/Recordum.UI.Tests/HabitBuilder/*`, `HabitEditor/*`, `HabitScreen/*` | test delle view; integrazione sul DB vero | 1-9 |

## Review Focus

1. **Chiudere il tab (o cambiare abitudine) subito dopo aver scritto il nome in Configura**: in WPF il nome si scrive
   alla perdita del fuoco; ci si aspetta che la modifica sia salvata, non persa. → Task 6, test 6.4.
2. **Annullare il builder dopo aver preparato un'unità o una scala personalizzata**: ci si aspetta che niente resti nel
   DB (`StagedReferenceData`). → Task 2, test 2.4.
3. **Doppio clic su "Crea" nel picker o su "Lancia" nel builder**: una sola abitudine. → Task 2 test 2.3, Task 4 test 4.3.
4. **Finestra stretta, due colonne aperte**: niente sborda (A8), Configura scorre in verticale, le colonne in orizzontale
   sotto i 320 px ciascuna. → Task 5 test 5.2, Task 8 test 8.3.
5. **Cambiare l'orario di reset con la schermata aperta**: la Cronologia e la fascia Oggi si aggiornano subito (il servizio
   pubblica `HabitUpdated` da F2a), e l'avviso della decisione 8 è visibile. → Task 7, test 7.3.

---

### Task 1: I picker di colore e d'icona

**Files:**
- Create: `src/Recordum.UI/Controls/ColorSwatchPicker.axaml(.cs)`, `HsvColorPicker.axaml(.cs)`, `IconPicker.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/Controls/ColorPickerTests.cs`, `IconPickerTests.cs`

**Riferimento WPF:** `src/Recordum.WPF/Views/ColorSwatchPicker.xaml`, `HsvColorPicker.xaml`, `IconPicker.xaml`.

**Interfaces:**
- Produces: `ColorSwatchPicker` con proprietà stilizzate `Color` (stringa `#RRGGBB`, two-way), `RecentColors`,
  `FavoriteColors`, `AddFavoriteCommand`, `RemoveFavoriteCommand` (quelle che `EditorPane.xaml` e `HabitBuilderView.xaml`
  legano); il pannello HSV per un colore libero; `IconPicker` con `Icon` (chiave, two-way, nullable = nessuna icona) sul
  catalogo di `Themes/Icons.axaml`.

**Decisioni e trappole:** decisione 5 (niente contagocce). Un colore libero scritto a mano si valida (`#RRGGBB`); un
valore non valido non arriva al ViewModel. Le tessere sono pulsanti con nome accessibile (il nome del colore o il suo hex).

- [ ] **Step 1 — test, rossi**: 1.1 un clic su una tessera scrive il suo colore nella proprietà legata; 1.2 il colore
  scelto è marcato come selezionato (misurato: un bordo o un pixel); 1.3 HSV: trascinare il cursore cambia il colore, un
  hex non valido non lo cambia; 1.4 l'icon picker scrive la chiave e "nessuna icona" scrive null; 1.5 ogni tessera e ogni
  icona ha un nome accessibile.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 1.1 e 1.3.
- [ ] **Step 3** — commit `feat(ui): picker di colore e d'icona (F2b)`.

### Task 2: Il builder e la sua finestra

**Files:**
- Create: `src/Recordum.UI/Views/HabitBuilder/HabitBuilderView.axaml(.cs)`, `src/Recordum.UI/Services/AvaloniaHabitBuilderLauncher.cs`
- Modify: `src/Recordum.UI/Bootstrap/AvaloniaShellServices.cs` (il launcher vero al posto del Null)
- Test: `tests/Recordum.UI.Tests/HabitBuilder/HabitBuilderViewTests.cs`

**Riferimento WPF:** `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml` (696 righe: testata, composizione,
mattoni Programma/Obiettivo/Sessione, micro-form di unità e scala personalizzate), `WpfHabitBuilderLauncher.cs`.

**Interfaces:**
- Consumes: `HabitBuilderViewModel` (`SetParent`, `InitializeAsync`, `CreatedHabitNodeId`, i mattoni
  `ScheduleBrickViewModel`, `GoalBrickViewModel`, `SessionBrickViewModel`, `FieldBlockViewModel`, `StagedReferenceData`),
  i picker del Task 1.
- Produces: `AvaloniaHabitBuilderLauncher : IHabitBuilderLauncher` — finestra modale sulla finestra principale, torna
  l'id creato o null; lascia un posto per l'anteprima del Task 3 (un host nominato `PreviewHost`, vuoto).

**Decisioni e trappole:** decisioni 1 e 6 (W2V-9). I campi numerici: la stessa regola di F2a (separatore dell'altra lingua
rifiutato, ciò che si salva è ciò che si vede; vedi `MetricCellView` e il suo code-behind). `AsyncRelayCommand.ExecuteAsync`
non consulta `CanExecute`: la guardia contro il doppio "Lancia" sta nel ViewModel.

- [ ] **Step 1 — test, rossi**: 2.1 il builder compone un'azione semplice, un contatore con un campo quantità e unità, uno
  stato su scala, un'astinenza; "Lancia" crea il nodo giusto nel DB privato e il launcher torna il suo id; 2.2 "Annulla"
  chiude e torna null; 2.3 (Review Focus 3) doppio "Lancia" crea un'abitudine sola; 2.4 (Review Focus 2) un'unità e una
  scala personalizzate preparate e poi annullate non lasciano righe nel DB; 2.5 i ToggleButton accesi hanno il colore
  d'accento (pixel, W2V-9); 2.6 la registrazione DI risolve il launcher vero, non il Null.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 2.3 e 2.4.
- [ ] **Step 3** — commit `feat(ui): builder delle abitudini (F2b)`.

### Task 3: L'anteprima viva del builder

**Files:**
- Create: `src/Recordum.UI/Views/HabitBuilder/BuilderPreviewPane.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/HabitBuilder/BuilderPreviewPaneTests.cs`

**Riferimento WPF:** `src/Recordum.WPF/Views/HabitBuilder/BuilderPreviewPane.xaml`; ViewModel
`ViewModels/HabitBuilder/Preview/` (`BuilderPreviewModel`, `SamplePreviewFactory`, `SampleServices`).

**Interfaces:** Consumes: `HabitBuilderViewModel.Preview` (o il nome reale: leggerlo), la `HeatmapStrip` della Home.
Produces: la view montata in `PreviewHost`.

**Decisioni e trappole:** decisione 6 (W2V-19): la riga d'anteprima usa la stessa striscia per tipo della riga Home
(`StripVisual`, `StripFractions`), non una striscia piatta. L'anteprima segue la composizione mentre la si cambia.

- [ ] **Step 1 — test, rossi**: 3.1 cambiare il tipo composto cambia la striscia dell'anteprima (la `Kind` della
  `HeatmapStrip` e un pixel); 3.2 nome, colore e icona dell'anteprima seguono la testata; 3.3 l'anteprima di un contatore
  mostra la cifra chiave come la Home.
- [ ] **Step 2** — implementazione, verdi, mutazione su 3.1.
- [ ] **Step 3** — commit `feat(ui): anteprima viva del builder (F2b)`.

### Task 4: Il picker dei template e le porte d'ingresso

**Files:**
- Create: `src/Recordum.UI/Views/HabitTemplatePickerWindow.axaml(.cs)`, `src/Recordum.UI/Services/AvaloniaHabitTemplatePickerLauncher.cs`
- Modify: `AvaloniaShellServices.cs`, `src/Recordum.UI/Views/HomeView.axaml` ("Nuovo → Abitudine"),
  `src/Recordum.UI/Views/EmptyState.axaml` (il pulsante dello stato vuoto), i test di F1 che ne asserivano lo spegnimento
- Test: `tests/Recordum.UI.Tests/HabitTemplatePickerTests.cs`, `HomeViewTests.cs`, `EmptyStateTests.cs`

**Riferimento WPF:** `src/Recordum.WPF/Views/HabitTemplatePicker.xaml(.cs)`, `WpfHabitTemplatePickerLauncher.cs`.

**Interfaces:** Consumes: `HabitTemplatePickerViewModel` (`LoadAsync(parentId)`, `CreatedHabitNodeId`,
`OpenBuilderRequested`, `ParentForBuilder`), `IHabitBuilderLauncher` (Task 2). Produces:
`AvaloniaHabitTemplatePickerLauncher : IHabitTemplatePickerLauncher` — se il picker chiede il builder, apre il builder col
genitore giusto e torna il suo risultato.

**Decisioni e trappole:** decisioni 1 e 2. I template si spiegano da soli (E6): la descrizione di ogni template è
visibile, non solo al passaggio del mouse. Il genitore della creazione segue la selezione della Home
(`ResolveCreationParentId`).

- [ ] **Step 1 — test, rossi**: 4.1 scegliere un template crea l'abitudine nel DB privato, la Home la mostra e la
  seleziona, si apre il tab; 4.2 "Composizione libera" apre il builder col genitore del picker; 4.3 (Review Focus 3)
  doppio "Crea" crea un'abitudine sola; 4.4 "Nuovo → Abitudine" è visibile e apre il picker; il pulsante dello stato vuoto
  compare solo sulla scheda Attive; 4.5 con un gruppo selezionato la nuova abitudine nasce dentro il gruppo.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 4.3 e 4.5.
- [ ] **Step 3** — tracker: F1-RAIL1 aggiornato (riacceso "Nuovo → Abitudine" e lo stato vuoto); commit
  `feat(ui): picker dei template e porte di creazione riaccese (F2b)`.

### Task 5: La schermata a due colonne

**Files:**
- Modify: `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/HabitScreen/HabitScreenColumnsTests.cs`

**Riferimento WPF:** `HabitScreen.xaml` righe 1049-1110 (chip, focus, fill-then-scroll), la maniglia delle colonne e
`HabitScreen.xaml.cs` (`OnColumnResizeDelta`, `OnColumnResizeCompleted`, `OnColumnResizeReset`).

**Interfaces:** Consumes: `ShowCronologia`, `ShowConfigura`, `IsConfiguraOpen`, `OpenColumnsMinWidth`, `FocusedColumn`,
`IsFocusActive`, `ToggleFocusCommand`, `ExitFocusCommand`, `GetColumnWidth`/`SetColumnWidth`/`ResetColumnWidth`/
`PersistColumnWidthsAsync`. Produces: un host nominato `ConfiguraHost` nella colonna Configura (vuoto, lo riempie il Task 6).

**Decisioni e trappole:** decisione 3. Una colonna chiusa non costruisce il suo contenuto (si usa un `Content` davvero
null o un selettore, non `IsVisible`). Il ridimensionamento si persiste solo a fine trascinamento.

- [ ] **Step 1 — test, rossi**: 5.1 il chip Configura apre e chiude la colonna, e chiusa non è costruita (si cerca nella
  view intera); 5.2 (Review Focus 4) con due colonne in una finestra di 600 px nessuna colonna è sotto i 320 px: compare lo
  scorrimento orizzontale; in 1200 px le due colonne riempiono la larghezza senza scorrimento; 5.3 trascinare la maniglia
  cambia la larghezza e la persiste al rilascio, il doppio clic la riporta al default; 5.4 la modalità focus mostra una
  colonna sola e il pulsante di ritorno.
- [ ] **Step 2** — implementazione, verdi, mutazione su 5.1.
- [ ] **Step 3** — commit `feat(ui): schermata a due colonne con ridimensionamento e focus (F2b)`.

### Task 6: La testata di Configura e l'host delle sezioni

**Files:**
- Create: `src/Recordum.UI/Views/HabitEditor/EditorPane.axaml(.cs)`, `SectionTemplate.cs`
- Test: `tests/Recordum.UI.Tests/HabitEditor/EditorPaneTests.cs`

**Riferimento WPF:** `src/Recordum.WPF/Views/EditorPane.xaml`, `Views/HabitEditor/Sections/SectionTemplates.xaml`.

**Interfaces:** Consumes: `HabitEditorViewModel` (`Name`, `Description`, `SelectedHabitType`, `SelectedNodeType`, `Color`,
`Icon`, `RecentColors`, `FavoriteColors`, i comandi dei preferiti, `Sections` coi `SectionViewModelBase`, `StatusText`,
`EditorStatus`). Produces: `EditorPane` montato in `ConfiguraHost`; `SectionTemplate : IDataTemplate` con un ramo per tipo
di sezione (i rami arrivano coi Task 7 e 8, fino ad allora tornano null), e **nessun** ramo per le notifiche (decisione 4).

**Decisioni e trappole:** decisioni 4, 6 (A12) e 7. Il tipo non si cambia dopo la creazione (F0-11): in testata è un'etichetta.
Per un gruppo la testata mostra nome, colore e icona e le sezioni non applicabili non si mostrano.

- [ ] **Step 1 — test, rossi**: 6.1 cambiare nome, colore e icona salva sul DB privato e la riga Home e l'intestazione della
  schermata si aggiornano; 6.2 lo stato "Salvataggio… / Salvato" è visibile anche con le sezioni aperte e scorse in fondo
  (A12: è fuori dallo scorrimento); 6.3 un gruppo appena creato dalla Home apre il tab con Configura e si rinomina da lì;
  6.4 (Review Focus 1) scrivere il nome e chiudere subito il tab (o selezionare un'altra abitudine) salva il nome; 6.5 la
  sezione Notifiche non viene costruita.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 6.2 e 6.4.
- [ ] **Step 3** — commit `feat(ui): testata di Configura e host delle sezioni (F2b)`.

### Task 7: Sezioni del ritmo

**Files:**
- Create: `src/Recordum.UI/Views/HabitEditor/Sections/SchedulingSectionView`, `StreakSectionView`, `GoalSectionView`,
  `ResetTimeSectionView`, `RegistrationSectionView`, `HomeRowSectionView` (`.axaml` + `.axaml.cs`)
- Modify: `SectionTemplate.cs`
- Test: `tests/Recordum.UI.Tests/HabitEditor/RhythmSectionsTests.cs`

**Riferimento WPF:** le sei view omonime in `src/Recordum.WPF/Views/HabitEditor/Sections/` (10-47 righe l'una).

**Decisioni e trappole:** decisione 8 (avviso sul reset). Chi scrive `HabitConfig`/`Goal` fuori dai servizi si porta il
proprio gate (`ValidateCrossFields`, `ck_goal_period_consistency`): qui si passa dai ViewModel, che passano dai servizi.

- [ ] **Step 1 — test, rossi**: 7.1 per ognuna delle sei sezioni, una modifica tipica arriva al DB privato (programma
  "giorni specifici", modalità della serie, obiettivo con periodo, orario di reset, gesto di registrazione, striscia della
  riga Home); 7.2 la striscia della riga Home cambiata in Configura cambia la riga Home vera; 7.3 (Review Focus 5) cambiare
  l'orario di reset con la schermata aperta aggiorna il giorno "oggi" del calendario e mostra l'avviso.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 7.2 e 7.3.
- [ ] **Step 3** — commit `feat(ui): sezioni del ritmo di Configura (F2b)`.

### Task 8: Sezioni del contenuto

**Files:**
- Create: `src/Recordum.UI/Views/HabitEditor/Sections/MetricsSectionView`, `ScalesSectionView`, `TimerSectionView`,
  `TagsSectionView`, `NotesSectionView`, `AiSharingSectionView` (`.axaml` + `.axaml.cs`)
- Modify: `SectionTemplate.cs`
- Test: `tests/Recordum.UI.Tests/HabitEditor/ContentSectionsTests.cs`

**Riferimento WPF:** le sei view omonime in `src/Recordum.WPF/Views/HabitEditor/Sections/`.

**Decisioni e trappole:** decisione 6 (A8: le sezioni stanno nella colonna, il ComboBox "Scala" di WPF era largo 600 px).
La condivisione AI è spenta al primo avvio (CLAUDE.md §2.3) e la sezione non la accende da sola. Il passaggio a
"Ripetibile" (Metriche/Registrazione) non pubblica `HabitUpdated` (F2a-9): se la schermata ha bisogno di ricostruire il
quick-add, il task lo verifica e, se serve, pubblica dal servizio che scrive la config, non dalla view.

- [ ] **Step 1 — test, rossi**: 8.1 per ognuna delle sei sezioni, una modifica tipica arriva al DB privato (unità di una
  metrica, scala, modalità del cronometro, un tag aggiunto e tolto, colore del marcatore delle note, condivisione AI);
  8.2 cambiare la modalità del cronometro aggiorna il cronometro della Cronologia; 8.3 (Review Focus 4, A8) a 320 px di
  colonna nessun controllo di nessuna sezione esce dal bordo destro (si misurano i rettangoli, con tutte le sezioni aperte);
  8.4 la condivisione AI resta spenta finché non la si accende.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 8.2 e 8.3.
- [ ] **Step 3** — commit `feat(ui): sezioni del contenuto di Configura (F2b)`.

### Task 9: Creare e configurare sul DB vero, misure, chiusura

**Files:**
- Test: `tests/Recordum.UI.Tests/HabitEditor/CreateAndConfigureIntegrationTests.cs`,
  `tests/Recordum.UI.Tests/HabitBuilder/BuilderScreenshotTests.cs`, `HabitEditor/EditorScreenshotTests.cs`
- Modify: `docs/phase-4-issues.md` (sezione "Onda F2b"), `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1 — test**: 9.1 dalla finestra principale su DB privato: picker → template → l'abitudine compare nella Home,
  si apre il tab, si registra (F2a), si apre Configura, si cambia l'obiettivo, la Cronologia e la fascia Oggi lo riflettono;
  9.2 builder → contatore con un campo e un'unità personalizzata → l'unità esiste, il quick-add a serie la mostra; 9.3
  schermate in chiaro e in scuro di picker, builder con anteprima, Configura con le sezioni aperte, ognuna con
  un'asserzione strutturale, guardate e descritte.
- [ ] **Step 2** — review dell'intero branch (controllore) e CI verde.
- [ ] **Step 3** — documenti: tracker (sezione F2b, e lo stato di A8, A12, W2V-9, W2V-19, F1-5, F1-RAIL1; W2V-11 resta
  aperto), ROADMAP "Dove siamo" (una pagina, niente cronaca), MILESTONES; nella ROADMAP la checklist a schermo per l'autore
  su GNOME: creare dai template e dal builder, configurare ogni sezione, la finestra stretta, chiaro e scuro, e i W2V-16,
  W2V-18 da ricontrollare. Commit e push.
