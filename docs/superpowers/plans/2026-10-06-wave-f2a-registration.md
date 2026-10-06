# Onda F2a — Registrazione su Avalonia — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dopo F2a, aprendo un'abitudine dalla Home di `Recordum.UI` si **registra**: la fascia Oggi dei tipi azione,
contatore, astinenza e stato, i quick-add dei cinque tipi tranne il diario (F2d), il cronometro di sessione, la barra dei
timer attivi, e il percorso "ieri" (si sceglie il giorno sul calendario e si registra lì, secondo R9).

**Architecture:** I ViewModel esistono tutti in `Recordum.Presentation` e la composizione Avalonia li risolve già
(`ShellIntegrationTests.Selecting_a_row_opens_a_tab_and_builds_a_habit_screen`): F2a scrive le **view**. La `HabitScreen`
di WPF (1412 righe) non si traduce: nasce una shell nuova con la fascia Oggi, la barra dei chip e **una sola colonna,
Cronologia**, che ospita calendario, quick-add, cronometro e registrazioni del giorno. Le altre colonne arrivano con le
loro onde. In Presentation si toccano tre cose, tutte difetti già presenti anche in WPF: gli eventi dei timer, l'ora
delle registrazioni mostrata in UTC, il tick della barra dei timer.

**Tech Stack:** C# / .NET 10; Avalonia 12.1.2 + `Avalonia.Headless.XUnit` (xunit.v3) per `Recordum.UI` e i suoi test;
`Recordum.Presentation` e `Recordum.Data` con xunit 2.9.3; `CommunityToolkit.Mvvm` 8.4.

**Spec:** [`docs/superpowers/specs/2026-09-10-avalonia-as-the-platform-design.md`](../specs/2026-09-10-avalonia-as-the-platform-design.md)
§6 (riga F2) e §13 (cuciture); [`docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md`](../specs/2026-09-25-avalonia-v1-plan-review.md)
§5.2-5.3 (ritmi: "ieri" deve costare quanto "oggi") e §6 Passo 3; [`docs/DOGFOODING.md`](../../DOGFOODING.md) (manifesto).
Lo spezzamento di F2 in F2a-F2d è in `docs/ROADMAP.md` (2026-10-06). **Forma del piano (D5)**: decisioni, interfacce,
obblighi di test e trappole; il codice lo scrive l'implementatore leggendo la view WPF indicata, che resta il riferimento
di comportamento (non di markup).

## Global Constraints

- **Branch**: `f2a-registration`, creato da `f2-perf1-home` (che contiene F1 e F1-PERF1, non ancora in `main`). Mai
  commit su `main`; l'hook `guard-main.js` legge il branch *prima* del comando, quindi si cambia branch in una chiamata
  separata. Aggiornato un `.md` → commit e push subito.
- **Ambiente Linux**: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`. Build:
  `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo`. **Mai `-p:UseAppHost=false`** da riga di comando.
- **Baseline** a `f2-perf1-home` (`5755991`): Core **150**, Data **541** (+7 DPAPI `WindowsOnly` che su Linux falliscono:
  `--filter "Category!=WindowsOnly"`), Plugins **66**, ExportEngine **37**, Presentation **955**, UI **190** con
  `--filter "Category!=Performance"` (192 senza filtro). Ogni task riporta i conteggi nuovi; un filtro vuoto sembra verde,
  quindi si controlla il totale eseguito.
- **WPF è congelato**: non si tocca. Le correzioni in Presentation e Data valgono anche per WPF, ed è voluto: sono
  difetti di dati o di comportamento, che il congelamento ammette.
- **Presentation non nomina tipi UI** (`NoWpfReferenceTests`; TFM `net10.0`). Un ViewModel che ha bisogno dell'host lo
  chiede con un'astrazione (CLAUDE.md §4: due implementazioni e un test della registrazione DI).
- **Avalonia**: `InitializeComponent()`, mai `AvaloniaXamlLoader.Load(this)`; `x:DataType` su ogni view e template;
  `[AvaloniaFact]` per ogni test che tocca tipi Avalonia; `using Xunit;` esplicito; mai referenziare
  `Recordum.Presentation.Tests`. In headless il primo controllo può non fare il layout: `RunJobs()` +
  `ForceRenderTimerTick()` dopo `Show()`; un PNG non prova niente senza un'asserzione strutturale.
- **Un `ContentControl` con `ContentTemplate` statico materializza il template anche a `Content` null**: i rami per
  tipo si scelgono con un `IDataTemplate` (come `HomeStateTemplate`) o con `DataTemplate` tipizzati; mai `IsVisible`
  come garanzia che un template non venga costruito.
- **Un difetto di disegno o di formattazione si misura**: pixel letti dal frame (`HomeRowViewTests.Pixel`: il frame
  headless è `Rgba8888`), testo reso, valore salvato nel servizio. Non si deduce dalle proprietà.
- **Ogni test di regressione si prova contro il codice rotto** (rosso, poi ripristino). **Attenzione**: `git checkout`
  non ripristina un file non ancora tracciato; per i file nuovi la mutazione si annulla a mano e si verifica col diff.
- **Un parametro di stato nuovo va provato con un caso in cui il default risponde diverso** (il parametro muto).
- **Il tempo da `IClock`, il giorno da `DayBoundary`** (`DayDerivationGuardTests`, `DayBoundaryParityTests`).
- **Stringhe**: `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs`, quest'ultimo **solo via shell**
  (l'hook blocca Edit/Write sui `*.Designer.cs`). Molte stringhe della `HabitScreen` esistono già: cercarle prima.
- **Linguaggio visivo** (CLAUDE.md §11): icone Lucide in `Themes/Icons.axaml`, **mai glifi o emoji** (la barra dei timer
  WPF usa ⏸ ▶ ■ ✎ ✕: su Avalonia diventano icone); chiaro e scuro curati uguali; il colore identifica l'abitudine.
- **`TreatWarningsAsErrors`** globale. Footer dei commit: `Co-Authored-By:` del modello che esegue.

## Decisioni prese scrivendo il piano

L'esecutore non le ri-discute; sono qui perché l'autore possa disfarle **prima** dell'esecuzione.

1. **Una sola colonna, Cronologia, e dentro c'è il calendario.** La spec metteva `CalendarNavigator` in F3, ma il percorso
   "ieri" (revisione §5.3 punto 2, ora in F2) passa dal calendario: il giorno selezionato diventa la data del quick-add
   (`HabitScreenViewModel` lo fa già, righe 707 e 607). Entrano anche le **registrazioni del giorno** con la
   cancellazione, perché durante il dogfooding un errore si corregge cancellando e registrando di nuovo. Restano a F3:
   modalità "Periodo" delle registrazioni (`DatePicker`), `DayNoteView`, l'editor inline di una registrazione.
2. **Niente modalità focus, niente ridimensionamento né splitter delle colonne**: servono da due colonne in su (F3). I
   chip delle colonne non ancora portate (Panoramica, Diario, Dettaglio, Note, Configura) sono `IsVisible="False"`
   statici con un commento che nomina l'onda, come F1-RAIL1. Il pulsante Export dell'intestazione idem (F6).
3. **Il diario resta fuori**: la view Oggi del diario apre la colonna Diario o la nota veloce, che arrivano con F2d. In
   F2a il tipo diario non ha fascia Oggi (il ramo del template torna null) e la Cronologia mostra calendario e
   registrazioni senza quick-add, come già fa WPF (`QuickAddForCronologia` è null per i diari).
4. **Gli eventi dei timer vanno sul bus.** Oggi `TimerStatusBarViewModel` si carica solo all'avvio (`MainWindowViewModel`
   riga 142) e nessuno lo aggiorna: un cronometro avviato dalla schermata non compare nella barra, anche in WPF. Si
   aggiungono a `WellKnownEvents` (`Recordum.Plugins.Contracts`, aggiunta compatibile) `TimerStarted`, `TimerPaused`,
   `TimerResumed`, `TimerStopped`, pubblicati da `ActiveTimerService`; la barra si iscrive e si ricarica.
5. **La barra dei timer si scandisce da sola**: `TimerStatusBarViewModel` prende un `ITickSourceFactory` e tiene un tick
   da 1 s solo mentre ha righe. In WPF il tick sta nel code-behind della view (`DispatcherTimer`): su Avalonia non si
   ricopia.
6. **L'ora delle registrazioni si mostra nel fuso dell'abitudine.** `CompletionRowViewModel.Display` formatta oggi
   l'istante **UTC** (commento alla riga 74: "debito di presentazione più vecchio di R9"). Con la verifica di R9 a schermo
   in F2a, un orario sfasato di una o due ore renderebbe la verifica illeggibile. `Display` passa da `DayBoundary.TimeZone`.
7. **L'accento della schermata è il colore dell'abitudine**, come in WPF (`HabitScreen.xaml` righe 26-33). In Avalonia le
   risorse non hanno DataContext: il code-behind della shell imposta, a ogni cambio di `Node.Color`, `Brush.Accent` **e**
   la famiglia `SystemAccentColor` (con le sei sfumature `Light1-3`/`Dark1-3`) nelle risorse della view, perché i
   controlli Fluent (slider, check, focus) leggono quelle e non `Brush.Accent` (vedi il commento in `App.axaml`).
8. **I numeri si interpretano senza separatore delle migliaia.** In WPF `NumericInputRule` rifiuta "2.5" in italiano
   invece di salvarlo come 25. Il piano F1 (decisione 9) ha misurato che i binding compilati di Avalonia usano la cultura
   del thread e rifiutano "2.5" in it-IT, ma **non ha misurato** "2,5" in en-US né "1.500": se il convertitore di
   default accetta il raggruppamento, il valore si moltiplica in silenzio. Il Task 5 **misura per primo** e, se serve,
   aggiunge un convertitore che usa gli stessi `NumberStyles` di `NumericInputRule`.
9. **Le registrazioni del giorno non hanno ✎.** `CompletionRowViewModel` ha `BeginEdit`/`SaveEdit`, ma il template WPF
   non ha mai avuto un editor: ✎ accende `IsEditing` e non succede niente di visibile (stesso schema di F1-5). Su
   Avalonia il pulsante non si mostra; il difetto WPF va a tracker. L'editor arriva con F3.
10. **Lo stile "segmento" diventa globale**: `RadioButton.segment` (oggi locale in `HomeView.axaml`) e un nuovo
    `CheckBox.segment` per i chip vanno in `Themes/ControlStyles.axaml`.

## Struttura dei file

| File | Responsabilità | Task |
|---|---|---|
| `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml(.cs)` | shell: intestazione, accento, fascia Oggi, chip, colonna Cronologia | 1 |
| `src/Recordum.UI/Themes/ControlStyles.axaml` | `RadioButton.segment` (spostato), `CheckBox.segment` | 1 |
| `src/Recordum.UI/Views/Workspace/WorkspaceView.axaml` | il `DataTemplate` di `HabitScreenViewModel` costruisce la shell | 1 |
| `src/Recordum.UI/Views/HabitScreen/Today/*` | `TodayStripView`, `TodayContentTemplate`, quattro view per tipo | 2 |
| `src/Recordum.UI/Views/HabitScreen/Calendar/CalendarNavigatorView.axaml(.cs)` | mese e settimana, marcatori, navigazione | 3 |
| `src/Recordum.UI/Views/HabitScreen/QuickAdd/*` | `QuickAddTemplate` (selettore), azione, contatore a tap, astinenza | 4 |
| `src/Recordum.UI/Views/HabitScreen/QuickAdd/MetricCellView`, `SeriesQuickAddView`, `MeasuredQuickAddView` | celle delle metriche, serie, misurato | 5 |
| `src/Recordum.Presentation/ViewModels/Timer/TimerStatusBarViewModel.cs`, `src/Recordum.Data/Services/ActiveTimerService.cs`, `src/Recordum.Plugins.Contracts/Events/WellKnownEvents.cs` | eventi dei timer, tick della barra | 6 |
| `src/Recordum.UI/Views/Timer/TimerStatusBarView.axaml`, `.../HabitScreen/TimerControlView.axaml`, `MainWindow.axaml` | barra dei timer e cronometro di sessione | 6 |
| `src/Recordum.Presentation/ViewModels/HabitScreen/CompletionRowViewModel.cs` | `Display` nel fuso dell'abitudine | 7 |
| `src/Recordum.UI/Views/HabitScreen/Records/*` | registrazioni del giorno e periodi di astinenza | 7 |
| `tests/Recordum.UI.Tests/HabitScreen/*` | test delle view; integrazione R9 sul DB vero | 1-8 |

## Review Focus

Le cinque condizioni che la spec implica, che nessun test dei task esercita da solo, e che colpirebbero di più chi usa
l'app. Ogni riga ha il suo test nel task indicato.

1. **Un numero scritto col separatore dell'altra lingua** ("2.5" con l'interfaccia in italiano, "2,5" in inglese,
   "1.500"): ci si aspetta un rifiuto visibile, mai un valore dieci o mille volte più grande. → Task 5, test 5.1.
2. **Registrare per ieri a cavallo del reset** (reset alle 04:00, adesso le 02:30 locali): la registrazione va sul giorno
   scelto sul calendario e il calendario la mostra lì, non sul giorno dopo. → Task 8, test 8.1.
3. **Cambiare abitudine mentre un salvataggio è in volo**: la registrazione va all'abitudine su cui è partita, e la
   schermata nuova non mostra i dati della vecchia. → Task 4, test 4.4.
4. **Un cronometro avviato, poi l'app chiusa e riaperta**: la barra lo mostra all'avvio col tempo giusto, e fermarlo
   registra la sessione. → Task 6, test 6.4.
5. **Cambiare tema con la schermata aperta**: accento, calendario e celle si ritingono senza riaprire il tab (in WPF il
   calendario non si ritingeva, F0-Y2). → Task 1, test 1.3, e Task 3, test 3.3.

---

### Task 1: La shell della schermata

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml`, `HabitScreenView.axaml.cs`
- Modify: `src/Recordum.UI/Views/Workspace/WorkspaceView.axaml` (il `DataTemplate` di `HabitScreenViewModel`),
  `src/Recordum.UI/Themes/ControlStyles.axaml`, `src/Recordum.UI/Views/HomeView.axaml` (lo stile segmento esce)
- Test: `tests/Recordum.UI.Tests/HabitScreen/HabitScreenViewTests.cs`, `ShellIntegrationTests.cs` (il test che apre un tab)

**Riferimento WPF:** `HabitScreen.xaml` righe 980-1110 (intestazione, fascia, chip) e 1278-1346 (cornice della colonna).

**Interfaces:**
- Consumes: `HabitScreenViewModel` (`Node`, `Name`, `TodayStrip`, `ShowTodayStrip`, `ShowChipBar`, `IsGroupNode`,
  `IsCronologiaOpen`, `ShowCronologia`, `ToggleColumnCommand` con `HabitColumn.Cronologia`).
- Produces: `HabitScreenView` con tre segnaposto nominati che i task seguenti riempiono: `TodayHost` (Task 2),
  `CalendarHost`, `QuickAddHost`, `TimerHost`, `RecordsHost` dentro la colonna (Task 3, 4, 6, 7). `CheckBox.segment` e
  `RadioButton.segment` in `ControlStyles.axaml`.

**Decisioni e trappole:**
- Il `DataTemplate` di `WorkspaceView` resta tipizzato (non un `ContentTemplate` statico): il commento che c'è spiega perché.
- L'accento (decisione 7) si aggiorna su `PropertyChanged` di `Node` e su `DataContextChanged`; il tab riusato per un'altra
  abitudine (anteprima) cambia `Node` senza ricostruire la view.
- L'intestazione mostra icona e nome; il nome segue la rinomina dalla Home (è lo stesso ViewModel? verificarlo e, se no,
  annotarlo nel ledger).
- I chip non portati e Export: decisione 2. Un gruppo (`IsGroupNode`) in F2a mostra intestazione e chip senza colonne: la
  Panoramica è di F3; annotarlo a tracker, non costruire un segnaposto.

- [ ] **Step 1 — test, rossi**:
  - 1.1 `Opening_a_habit_tab_builds_the_habit_screen`: in `ShellIntegrationTests`, dopo la selezione di una riga il tab
    attivo contiene un `HabitScreenView` col nome dell'abitudine reso.
  - 1.2 `The_screen_accent_is_the_habit_colour`: un `Border` dentro la view con `Background="{DynamicResource
    Brush.Accent}"` misura (pixel) il colore dell'abitudine, e una abitudine di colore diverso, aperta nello stesso tab,
    lo cambia. Il default dell'app (`#3B82F6`) deve differire dai due colori del test.
  - 1.3 `The_accent_survives_a_theme_switch`: con la schermata aperta si passa a `ThemeVariant.Dark`; l'accento resta il
    colore dell'abitudine e lo sfondo segue il tema.
  - 1.4 `Only_the_cronologia_chip_is_visible_in_f2a` e `The_cronologia_chip_toggles_its_column`.
- [ ] **Step 2** — implementazione; i test verdi; le mutazioni: togliere l'aggiornamento dell'accento su `Node` (1.2
  rosso), togliere la famiglia `SystemAccentColor` (un `CheckBox` dentro la schermata torna blu: aggiungere quel pixel a
  1.2 se la mutazione non va rossa).
- [ ] **Step 3** — suite UI e Presentation; commit `feat(ui): shell della schermata dell'abitudine (F2a)`.

### Task 2: La fascia Oggi

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Today/TodayStripView.axaml(.cs)`, `TodayContentTemplate.cs`,
  `ActionTodayView`, `CounterTodayView`, `ResistanceTodayView`, `StateTodayView` (`.axaml` + `.axaml.cs`)
- Test: `tests/Recordum.UI.Tests/HabitScreen/TodayStripTests.cs`

**Riferimento WPF:** `Views/HabitScreen/Today/*.xaml` (fra 25 e 45 righe l'una).

**Interfaces:**
- Consumes: `TodayStripViewModel.Content` (`ITodayContent`: `ActionTodayContent`, `CounterTodayContent`,
  `ResistanceTodayContent`, `StateTodayContent`; `JournalTodayContent` resta senza view, decisione 3).
- Produces: `TodayContentTemplate : IDataTemplate` che sceglie la view per tipo e torna null per il diario e per un
  contenuto sconosciuto; `TodayStripView` collassata quando `Content` è null o il template torna null.

**Decisioni e trappole:**
- Lo slider dello stato: passi interi, `IsSnapToTickEnabled`, il pulsante "Salva" spento finché `CanRecord` è falso.
  `Value` è two-way su `SliderValue`: verificare che lo slider non scriva un valore all'avvio (il primo `Value` dello
  slider è 0, e un binding two-way può spingerlo nel ViewModel prima che il ViewModel abbia caricato il suo).
- L'orologio dell'astinenza scorre con `ITickSource` (`DispatcherTickSourceFactory` già registrato); nei test si usa un
  tick finto, mai un'attesa.

- [ ] **Step 1 — test, rossi**:
  - 2.1 per ognuno dei quattro tipi, la view costruita è quella del tipo, e per il diario non si costruisce niente (né la
    fascia né il bordo: si cerca nell'intera schermata).
  - 2.2 `Recording_from_the_strip_saves_through_the_service`: azione (spunta), contatore (+1), stato (slider a 4 e
    Salva) arrivano al servizio finto con il valore giusto; lo stato salvato è l'intero, non l'etichetta.
  - 2.3 `Opening_a_state_habit_does_not_write_a_value`: aprire la schermata di uno stato con un valore già registrato non
    cambia `SliderValue` né chiama il servizio.
  - 2.4 l'orologio dell'astinenza avanza di un secondo a ogni tick finto.
- [ ] **Step 2** — implementazione, verdi, mutazione di 2.3 (binding `TwoWay` senza la guardia che il ViewModel ha, se
  ce l'ha; se il ViewModel non ha guardia e il test passa comunque, il test è muto: riscriverlo finché la mutazione lo
  rompe).
- [ ] **Step 3** — commit `feat(ui): fascia Oggi della schermata (F2a)`.

### Task 3: Il calendario

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Calendar/CalendarNavigatorView.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/HabitScreen/CalendarNavigatorViewTests.cs`

**Riferimento WPF:** `Views/HabitScreen/Calendar/CalendarNavigator.xaml` (333 righe).

**Interfaces:**
- Consumes: `CalendarNavigatorViewModel` (`SelectedDate`, le celle `CalendarDayCellViewModel`, i blocchi del mese,
  `CalendarViewMode`, i comandi di navigazione), `CompletedColorHex`, `NoteColorHex`.
- Produces: la view montata in `CalendarHost` (Task 1).

**Decisioni e trappole:**
- Le tinte dei marcatori vengono da `CompletedColorHex`/`NoteColorHex` (stringhe): si convertono con i convertitori che
  esistono (`HexToBrushConverter`, `HabitTintConverter`); oggi e selezionato usano `Brush.Accent`, cioè il colore
  dell'abitudine (decisione 7).
- W2V-4 (il cerchietto copre il numero del giorno) e U4 (numero scentrato) sono nella lista da ricontrollare a schermo:
  la view nuova li evita per costruzione (il numero sopra il cerchio, centrato con allineamento, non con margini).
- Tutto `DynamicResource`, così il cambio di tema ritinge (F0-Y2 in WPF).

- [ ] **Step 1 — test, rossi**:
  - 3.1 `Clicking_a_day_selects_it_and_moves_the_quick_add_date`: clic (puntatore vero, `MouseDown`/`MouseUp`) sulla
    cella di ieri → `Calendar.SelectedDate` è ieri e `QuickAdd.CompletionDate` pure.
  - 3.2 `Today_is_the_day_of_the_boundary`: con reset alle 04:00 e l'orologio finto alle 02:30 locali, la cella marcata
    come oggi è il giorno prima della data di calendario.
  - 3.3 `The_calendar_retints_on_a_theme_switch`: il pixel di sfondo di una cella vuota cambia passando al tema scuro.
  - 3.4 la navigazione al mese precedente e il ritorno a oggi; la modalità settimana mostra sette celle.
- [ ] **Step 2** — implementazione, verdi, mutazione di 3.2 (la view che calcola "oggi" da sé invece di leggerlo dal
  ViewModel deve andare rossa).
- [ ] **Step 3** — commit `feat(ui): calendario della Cronologia (F2a)`.

### Task 4: Quick-add di azione, contatore a tap e astinenza

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/QuickAdd/QuickAddTemplate.cs`, `ActionQuickAddView`,
  `CounterTapQuickAddView`, `AbstinenceQuickAddView`
- Test: `tests/Recordum.UI.Tests/HabitScreen/QuickAddViewTests.cs`

**Riferimento WPF:** `QuickAddTemplateSelector.cs`; `HabitScreen.xaml` righe 226-265 (contatore a tap), 499-551
(astinenza), e il template dell'azione nelle risorse.

**Interfaces:**
- Consumes: `HabitScreenViewModel.QuickAddForCronologia` (`QuickAddViewModelBase`: `ActionQuickAddViewModel`,
  `CounterTapQuickAddViewModel`, `AbstinenceQuickAddViewModel`, `MeasuredQuickAddViewModel`,
  `CounterSeriesQuickAddViewModel`; `JournalQuickAddViewModel` non arriva qui).
- Produces: `QuickAddTemplate : IDataTemplate` con un ramo per tipo; i rami di misurato e serie li aggiunge il Task 5
  (fino ad allora tornano null).

**Decisioni e trappole:**
- `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`: un doppio clic su "Salva" non deve registrare due volte.
  Se il ViewModel non ha la guardia dentro il metodo, la si aggiunge lì (Presentation) col suo test.
- `ErrorMessage` del ViewModel si mostra sotto il pulsante.

- [ ] **Step 1 — test, rossi**:
  - 4.1 un ramo per tipo, e per un ViewModel sconosciuto il template non costruisce niente.
  - 4.2 `A_tap_on_yesterday_records_a_retroactive_completion`: con ieri selezionato sul calendario, il tap dell'azione
    registra `IsRetroactive = true` a `DayBoundary.NoonOf(ieri)`; con oggi selezionato, `IsRetroactive = false` all'ora
    dell'orologio finto (il default deve dare l'altra risposta: è il caso del parametro muto).
  - 4.3 `A_double_click_on_save_records_once`.
  - 4.4 `Switching_habit_during_a_save_records_on_the_first`: il servizio finto trattiene il salvataggio; si seleziona
    un'altra abitudine; si rilascia; la registrazione porta l'id della prima, e la schermata mostra la seconda.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 4.2 (data fissa a oggi) e 4.3 (guardia tolta).
- [ ] **Step 3** — commit `feat(ui): quick-add di azione, contatore e astinenza (F2a)`.

### Task 5: Celle delle metriche, quick-add misurato e a serie

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/QuickAdd/MetricCellView.axaml(.cs)`, `MeasuredQuickAddView`,
  `SeriesQuickAddView`; se la misura lo chiede, `src/Recordum.UI/Converters/PlainNumberConverter.cs`
- Modify: `QuickAddTemplate.cs` (i due rami)
- Test: `tests/Recordum.UI.Tests/HabitScreen/MetricCellTests.cs`, `SeriesQuickAddTests.cs`

**Riferimento WPF:** `HabitScreen.xaml` righe 267-498 (cella), 552-714 (serie e misurato); `NumericInputRule.cs`.

**Interfaces:**
- Consumes: `SeriesCellViewModel` (`Kind`, `IntegerValue`, `DecimalValue`, `TextValue`, `DurationValue`,
  `DurationUnit`, `TimeUnitOptions`, `ScalePoints`, `ReferencePoints`, `StepCount`, `IsChronoRunning`, e il
  cronometro della cella), `SeriesRowViewModel`, `SeriesChipViewModel`, `CounterSeriesQuickAddViewModel`,
  `MeasuredQuickAddViewModel`.
- Produces: i due rami di `QuickAddTemplate`; `PlainNumberConverter` se serve (decisione 8).

**Decisioni e trappole:**
- **Prima di scrivere la cella, misurare** in un test (decisione 8): `TextBox` con binding compilato a un `decimal?`, in
  it-IT e in en-US, con "2,5", "2.5", "1.500", "1,500". Si annotano nel ledger i valori finiti nel ViewModel; ogni
  valore moltiplicato in silenzio obbliga al convertitore.
- La scala: slider a passi con tacche ed etichette alle estremità; il valore salvato è sempre l'intero (CLAUDE.md §5).
- La durata: valore decimale + unità; il ViewModel normalizza in secondi al salvataggio (U8). Il cronometro della cella
  usa l'orologio condiviso del ViewModel.
- I chip delle serie: rinomina in linea con Invio che conferma ed Esc che annulla (la stessa trappola di F1: Esc e poi
  perdita del fuoco **non** devono salvare); "togli serie" spento con una sola serie.

- [ ] **Step 1 — test, rossi**:
  - 5.1 (Review Focus 1) `A_number_with_the_other_separator_is_rejected_not_multiplied`: in it-IT "2,5" → 2,5 e "2.5" →
    rifiuto visibile (`DataValidationErrors`) col valore del ViewModel invariato; in en-US il simmetrico; "1.500" e
    "1,500" mai 1500 in silenzio. La cultura si fissa nel test e si ripristina.
  - 5.2 `The_scale_saves_the_step_integer`: lo slider portato al terzo passo salva 3 (con etichette diverse dai numeri).
  - 5.3 `A_duration_in_minutes_is_saved_in_seconds`.
  - 5.4 `Esc_in_a_series_rename_keeps_the_old_name_even_after_focus_loss`.
  - 5.5 `The_last_series_cannot_be_removed`.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 5.1 (convertitore tolto, se aggiunto) e 5.4.
- [ ] **Step 3** — commit `feat(ui): celle delle metriche e quick-add a serie (F2a)`.

### Task 6: Cronometro di sessione e barra dei timer

**Files:**
- Modify: `src/Recordum.Plugins.Contracts/Events/WellKnownEvents.cs`, `src/Recordum.Data/Services/ActiveTimerService.cs`,
  `src/Recordum.Presentation/ViewModels/Timer/TimerStatusBarViewModel.cs`, `src/Recordum.UI/Views/MainWindow.axaml`,
  `src/Recordum.UI/Themes/Icons.axaml` (`Icon.Play`, `Icon.Pause`; `Icon.Square` esiste)
- Create: `src/Recordum.UI/Views/Timer/TimerStatusBarView.axaml(.cs)`, `src/Recordum.UI/Views/HabitScreen/TimerControlView.axaml(.cs)`
- Test: `tests/Recordum.Data.Tests/Services/ActiveTimerServiceEventTests.cs`,
  `tests/Recordum.Presentation.Tests/ViewModels/Timer/TimerStatusBarViewModelTests.cs`,
  `tests/Recordum.UI.Tests/Timer/TimerStatusBarViewTests.cs`

**Riferimento WPF:** `Views/Timer/TimerStatusBar.xaml(.cs)`; `HabitScreen.xaml` righe 34-77 (`TimerControlViewModel`).

**Interfaces:**
- Produces: `WellKnownEvents.TimerStarted|TimerPaused|TimerResumed|TimerStopped` (`"timer.started"` ecc.), pubblicati da
  `ActiveTimerService` dopo il commit, con l'id dell'abitudine nel payload come gli eventi esistenti;
  `TimerStatusBarViewModel(IActiveTimerService, IClock, IHabitNodeService, IEventBus, ITickSourceFactory)` che si
  ricarica sugli eventi (sul thread UI, `SubscribeOnUi`) e scandisce le righe solo mentre ce ne sono.
- La firma del costruttore cambia: l'elenco dei chiamanti lo dà il compilatore (WPF compreso), non un `grep`.

**Decisioni e trappole:**
- Decisioni 4 e 5. Un evento pubblicato prima del commit farebbe ricaricare la barra su dati vecchi: si pubblica dopo.
- Il test della registrazione DI (CLAUDE.md §4): il costruttore nuovo deve risolvere in tutte e due le composizioni.
- Icone, non glifi.

- [ ] **Step 1 — test, rossi**:
  - 6.1 (Data) avviare, mettere in pausa, riprendere e fermare pubblicano ciascuno il proprio evento, dopo il salvataggio.
  - 6.2 (Presentation) `A_timer_started_elsewhere_appears_in_the_bar`: un evento `TimerStarted` fa comparire la riga.
    **Va provato sul codice di oggi**: senza iscrizione è rosso.
  - 6.3 (Presentation) la barra non tiene un tick quando è vuota, e lo avvia alla prima riga.
  - 6.4 (UI, Review Focus 4) con un `active_timer` già nel DB di `ShellFixture`, la finestra principale mostra la barra
    all'avvio col tempo trascorso dall'orologio finto; "ferma" registra la sessione e la barra sparisce.
  - 6.5 (UI) avviare il cronometro dalla Cronologia fa comparire la barra senza riaprire niente.
- [ ] **Step 2** — implementazione, verdi, mutazioni su 6.2 e 6.3.
- [ ] **Step 3** — commit `feat: eventi dei timer, barra dei timer e cronometro su Avalonia (F2a)`.

### Task 7: Le registrazioni del giorno

**Files:**
- Modify: `src/Recordum.Presentation/ViewModels/HabitScreen/CompletionRowViewModel.cs` (`Display`)
- Create: `src/Recordum.UI/Views/HabitScreen/Records/CompletionListView.axaml(.cs)`, `AbstinencePeriodListView.axaml(.cs)`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/CompletionRowViewModelTests.cs`,
  `tests/Recordum.UI.Tests/HabitScreen/RecordsViewTests.cs`

**Riferimento WPF:** `HabitScreen.xaml` righe 78-205 e 1299-1340 (solo la modalità Giorno).

**Interfaces:**
- Consumes: `HabitRecordsViewModel` (`CurrentList`, `IsEmpty`), `CompletionListViewModel`, `CompletionRowViewModel`
  (`Display`, `FieldDisplays`, `Notes`, `DeleteCommand`), `AbstinencePeriodListViewModel`.
- Produces: le due view, montate in `RecordsHost`.

**Decisioni e trappole:**
- Decisioni 6 e 9. Il selettore Giorno/Periodo non si mostra (F3).
- La cancellazione passa da `IConfirmationDialog` (`AvaloniaConfirmationDialog`); un annullo lascia la riga.
- Dopo la cancellazione la Home si aggiorna (evento `CompletionDeleted`): lo verifica il Task 8 sul DB vero.

- [ ] **Step 1 — test, rossi**:
  - 7.1 (Presentation) `Display_is_the_wall_clock_of_the_habit`: con fuso Europe/Rome e una registrazione alle 21:30 UTC
    di un giorno d'estate, `Display` è le 23:30. Con il fuso di default (UTC) il test darebbe 21:30: è il parametro muto.
  - 7.2 (UI) le righe mostrate sono quelle del giorno selezionato, e cambiando giorno cambiano.
  - 7.3 (UI) `Cancelling_the_confirmation_keeps_the_record` e `Confirming_deletes_it`.
  - 7.4 (UI) nessun pulsante di modifica nelle righe (decisione 9).
- [ ] **Step 2** — implementazione, verdi, mutazione su 7.1.
- [ ] **Step 3** — tracker: la voce del ✎ muto di WPF nella sezione F2a; commit `feat(ui): registrazioni del giorno (F2a)`.

### Task 8: Il percorso "ieri" sul DB vero, misure, chiusura

**Files:**
- Test: `tests/Recordum.UI.Tests/HabitScreen/RetroactivePathIntegrationTests.cs`,
  `tests/Recordum.UI.Tests/HabitScreen/HabitScreenPerformanceTests.cs` (`Category=Performance`),
  `tests/Recordum.UI.Tests/HabitScreen/HabitScreenScreenshotTests.cs`
- Modify: `docs/phase-4-issues.md` (sezione F2a), `docs/ROADMAP.md`, `docs/MILESTONES.md`

**Decisioni e trappole:**
- I test d'integrazione usano `ShellFixture` (SQLCipher su file); lo stato del workspace persistito nel DB condiviso ha
  già fatto diventare rossa la CI una volta (`b1fe1dd`): ogni test lascia il DB come l'ha trovato, o usa una fixture sua.
- Le schermate in chiaro e in scuro, per ognuno dei quattro tipi con fascia, finiscono negli artefatti della CI e si
  **guardano**; ogni schermata ha un'asserzione strutturale accanto.

- [ ] **Step 1 — test**:
  - 8.1 (Review Focus 2) `Yesterday_after_midnight_lands_on_yesterday`: reset 04:00, orologio finto alle 02:30 locali del
    giorno D+1; si apre l'abitudine dalla Home, si sceglie sul calendario il giorno D-1 e si registra; il DB ha la
    registrazione a `NoonOf(D-1)`, la cella D-1 ha il marcatore, la cella D (che per il confine è "oggi") no, e la riga
    Home riflette la registrazione solo se D-1 rientra nella sua striscia.
  - 8.2 cancellare dalla Cronologia toglie la registrazione dal DB e dalla striscia della riga Home.
  - 8.3 (misura) aprire il tab di un'abitudine con un anno di registrazioni: tempo stampato, soglia larga (si registra il
    numero, come F1-PERF1).
  - 8.4 schermate.
- [ ] **Step 2** — review dell'intero branch (modello più forte), CI verde su Linux e Windows.
- [ ] **Step 3** — documenti: tracker (sezione F2a con i finding, F0-Y2 e W2V-4/U4 se chiusi per costruzione),
  ROADMAP "Dove siamo", MILESTONES; nella ROADMAP la **checklist a schermo per l'autore**: su GNOME la registrazione dei
  quattro tipi, "ieri" vicino al reset (R9), la barra dei timer dopo un riavvio, le celle con l'altro separatore, chiaro
  e scuro; i W2V della sezione §3 della checklist del pass visivo. Commit e push.
