# Direzione visiva, onda 2a — La schermata dell'abitudine

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la schermata dell'abitudine della spec §6.1–6.2 su Avalonia — testa con un solo comando che registra e quattro
numeri per caso, colonne apribili con ×, Cronologia con selettore di periodo, mappa che cambia forma, giorno scelto con la
sua nota, elenco del periodo reso — più la colonna Note portata da WPF; senza perdere una funzione.

**Architecture:** la logica nuova sta in Presentation in tipi **nuovi** (`HabitCase`, `HabitFiguresViewModel`,
`HistoryPeriodViewModel`, `HistoryMapViewModel`) o in proprietà **aggiunte** ai ViewModel che WPF condivide; WPF compila e
non cambia. Su Avalonia la fascia "Oggi" (`TodayStripView`) e la barra dei chip spariscono: le sostituisce la testa
(`HabitHeadView`), che ospita i quick-add che oggi stanno nella Cronologia. La mappa nuova legge i marker dallo stesso
`LoadMarkersAsync` del calendario di oggi, così colori e note restano una sola sorgente.

**Tech Stack:** .NET 10, Avalonia 12.1.2, CommunityToolkit.Mvvm, AvaloniaEdit (`DiaryEditor`), xunit v3 + Avalonia.Headless (UI),
xunit (Presentation, Data).

**Spec:** `docs/superpowers/specs/2026-10-28-ux-direction-design.md` (approvata il 2026-10-28) — §6.1, §6.2, §9 (onda 2),
§10, Appendice A ("Schermata dell'abitudine", "Diario"). Mockup: `anteprima-home-v2.html`, `combinazioni.html`,
`inventario.html` (periodo), `integrazioni.html` parte 2. **Il testo della spec vince sui mockup** (nome a 20 px, cinque
periodi). Decisione dell'autore del 2026-10-29: l'onda 2 si fa in due piani — **2a** (questo: abitudine e colonna Note) e
**2b** (gruppo con Panoramica e "Insieme", Configura in cinque blocchi, scelta del colore §7.1).

## Global Constraints

- Branch: `ux2-habit-screen` (esiste, da `main` a `c2887bd1`).
- Valgono i token e le regole dell'onda 1 (`docs/superpowers/plans/2026-10-28-ux-wave1-foundations-home.md`, Global
  Constraints): scala 12/13/14/16/20, `Brush.*`, `Opacity.*`, `HabitTextBrushConverter` per i valori nel colore dell'abitudine.
- Pulsanti: **colore dell'abitudine solo per il comando che registra** (in testa); inchiostro (`Button.primary`) per l'azione
  principale di una finestra; contorno per le secondarie; `Button.ghost` per "annulla". Icone Lucide, nessun glifo (`✎`, `▾`).
- Testa: nome **20/600 nella variante "testo"** del colore dell'abitudine (UX-7); filo di 3 px nel colore in cima (c'è già).
- Percentuali **sui giorni previsti dal programma** (`IScheduleEvaluator`), non sui giorni di calendario. Ogni finestra "30
  giorni" / "90 giorni" parte **dal più tardi** fra (oggi − 29/89) e il giorno di creazione dell'abitudine.
- "Volume" = somma del **primo campo quantità** del giorno (per `SortOrder`), col suo nome e la sua unità; mai prodotti fra campi.
- **Il giorno si chiede a `DayBoundary`** (`_dayBoundary` di `HabitScreenViewModel`, risolto una volta in `LoadAsync`); mai
  `DateOnly.FromDateTime(clock.UtcNow…)`. Una registrazione retroattiva si stampiglia a `NoonOf(giorno)` (lo fa già
  `QuickAddViewModelBase.ResolveCompletionTimestamp`).
- WPF non si tocca e deve compilare: in Presentation solo tipi nuovi e membri aggiunti. Si cambia il comportamento di un
  membro condiviso solo dove lo dice un task (Task 1: `OnCalendarDateSelected` delega a `SelectDayAsync`; Task 5: `OnNoteEvent`
  ricarica la nota del giorno).
- Le chiavi `EntriesPeriod` (Core) e `CalendarViewMode` (Presentation) **non** prendono valori nuovi: li legge WPF. Il periodo
  nuovo è un tipo nuovo.
- Ogni stringa nuova in `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs` (**solo via shell**). Test che
  leggono etichette: `TestCulture.Pin()`; test di Presentation con date/numeri: cultura `it-IT` fissata.
- Avalonia: `InitializeComponent()`, `x:DataType` ovunque, niente `ContentControl` con `ContentTemplate` statico (CLAUDE.md §8:
  si costruisce la view dal tipo, come `QuickAddTemplate`); `[AvaloniaFact]` per i test che toccano tipi Avalonia.
- Build con `--no-incremental`; mai `-p:UseAppHost=false`; `TreatWarningsAsErrors`. **Un test di regressione si prova rosso.**
- Un clic dell'utente non perde mai testo scritto (nota del giorno, nota della colonna Note).

## Review Focus

1. **Prima del reset** (alle 04:30 con reset 05:00): "oggi" della testa, i quattro numeri, la mappa e il giorno scelto sono
   tutti il giorno prima; una registrazione fatta lì va su quel giorno → Task 2 (`Before_the_reset_today_is_yesterday`), Task 1.
2. **Abitudine nuova o vuota** (creata oggi; mai registrata; creata 5 giorni fa): nessun numero è `NaN`, `∞` o negativo, le
   finestre "30 giorni" contano i giorni da quando esiste, la serie migliore è 0, la mappa "Tutto" mostra l'anno corrente
   → Task 2 (`New_habit_has_sane_figures`), Task 4 (`All_with_no_data_is_the_current_year`).
3. **Registrare per un altro giorno**: il comando registra sul giorno scelto, "annulla" toglie solo da quel giorno, cambiare
   abitudine riporta il giorno a oggi → Task 1 (`Recording_follows_the_selected_day`, `Undo_touches_only_the_selected_day`,
   `A_new_subject_starts_on_today`).
4. **Nota del giorno scritta e non salvata**, poi clic su un altro giorno o modifica della stessa nota dalla colonna Note:
   il testo non si perde e non viene sovrascritto → Task 5 (`Changing_day_saves_the_day_note`,
   `A_dirty_day_note_is_not_overwritten_by_an_event`).
5. **Periodi ai bordi**: ‹ › attraverso l'anno (dicembre → gennaio), › fermo sul periodo che contiene oggi, "Dal… al…" con
   l'inizio dopo la fine, "Tutto" su sei anni di dati → Task 4 (`Next_stops_at_today`, `Month_steps_across_the_year`,
   `Custom_range_is_ordered`, `All_has_one_heatmap_per_year`).

---

### Task 1: Il caso dell'abitudine e il giorno scelto

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitScreen/HabitCase.cs`
- Modify: `src/Recordum.Presentation/ViewModels/HabitScreen/HabitScreenViewModel.cs`,
  `src/Recordum.Presentation/ViewModels/HabitScreen/QuickAdd/ActionQuickAddViewModel.cs`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/HabitCaseTests.cs`,
  `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/SelectedDayTests.cs` (nuovi)

**Interfaces:**
- Produces: `public enum HabitCase { Action, CounterTap, Measured, Series, Session, State, Abstinence, Journal }`;
  `public static class HabitCases { public static HabitCase Resolve(HabitType type, HabitConfig? config, int metricCount); }`.
  Regole, in quest'ordine: Resistance → Abstinence; Journal → Journal; State → State; Action → Action; Counter con
  `HasIntegratedTimer && TimerMode is not null` → Session; Counter senza metriche → CounterTap; una metrica e non
  `Repeatable` → Measured; altrimenti → Series. (È la stessa scelta di `BuildCounterQuickAddAsync` e di
  `TimerControlViewModel.IsApplicable`, scritta una volta.)
- Produces (su `HabitScreenViewModel`, additivi): `HabitCase? Case` (null per gruppi e senza soggetto, aggiornato in
  `LoadAsync` e su `HabitUpdated`); `DateOnly SelectedDay`; `bool IsSelectedDayToday`; `Task SelectDayAsync(DateOnly day)`;
  `IRelayCommand BackToTodayCommand`; `IAsyncRelayCommand UndoLastCommand` con `bool CanUndoLast` (il giorno scelto ha almeno
  una registrazione — `!Records.IsEmpty`, il pannello in modalità Giorno — e `Case` non è Abstinence né Journal).
- Produces (su `ActionQuickAddViewModel`, additivi): `[ObservableProperty] bool IsDone`; `Task LoadDayAsync(CancellationToken ct = default)`
  (come `CounterTapQuickAddViewModel.LoadDayAsync`: registrazioni nella finestra `DayBoundary.RangeOf` di `CompletionDate`);
  `IAsyncRelayCommand ToggleCommand` (registra se non fatto, altrimenti cancella la più recente del giorno; guardia
  `IsSaving` dentro il metodo).
- `SelectDayAsync` è il corpo di oggi di `OnCalendarDateSelected` (conferma del diario, `CompletionDate`, `LoadDayAsync` dei
  quick-add a tocco e azione, Notes, DayNote, riepilogo, `Records.ShowDayAsync`) più `Calendar.RestoreSelection(day)` (il
  calendario di WPF resta allineato senza rialzare l'evento); `OnCalendarDateSelected` lo chiama. `LoadAsync` di un soggetto
  nuovo riparte da oggi (o dal giorno della bozza del diario, come oggi). `UndoLast` cancella con `ICompletionService.DeleteAsync`
  la registrazione più recente del giorno scelto; l'evento del bus ricarica il resto.

- [ ] **Step 1: test rossi.** `HabitCaseTests`: un `[Theory]` con le otto righe delle regole, più
  `Repeatable_single_field_is_series` e `Timer_wins_over_fields` (Counter con due metriche e timer → Session).
  `SelectedDayTests` (clock 28/10/2026 10:00, reset 05:00): `A_new_subject_starts_on_today`;
  `Recording_follows_the_selected_day` (`SelectDayAsync(27/10)` → `QuickAdd.CompletionDate` = 27/10 e un `SaveAsync`
  dell'azione scrive `IsRetroactive` con l'istante `NoonOf(27/10)`); `Undo_touches_only_the_selected_day` (una registrazione
  il 27 e una il 28, giorno scelto 27: resta quella del 28); `Undo_is_off_for_abstinence_and_journal`;
  `Back_to_today_returns_to_today`; `Action_toggle_removes_the_day_completion` (`IsDone` vero → `ToggleCommand` → zero
  registrazioni quel giorno, `IsDone` falso).
- [ ] **Step 2: rosso visto** (`dotnet test tests/Recordum.Presentation.Tests --filter "FullyQualifiedName~HabitCaseTests|FullyQualifiedName~SelectedDayTests"`, contare i test eseguiti).
- [ ] **Step 3: implementare.**
- [ ] **Step 4: verdi**, poi Presentation intera e build di `Recordum.slnx -p:EnableWindowsTargeting=true` (WPF compila).
- [ ] **Step 5: commit** `feat(presentation): il caso dell'abitudine e il giorno scelto per registrare`.

### Task 2: I quattro numeri — azione, contatore a tocco, stato, diario

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitScreen/Figures/HabitFiguresViewModel.cs`, `HabitFigure.cs`
- Modify: `HabitScreenViewModel.cs` (proprietà `Figures`, caricata in `LoadAsync` e sugli eventi di completamento),
  `Strings*.resx` + `Strings.Designer.cs`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/Figures/HabitFiguresTests.cs` (nuovo)

**Interfaces:**
- Consumes: `HabitCase` (Task 1).
- Produces: `public sealed record HabitFigure(string Value, string Caption)`;
  `HabitFiguresViewModel(ICompletionService, IStreakAggregator, IAbstinenceService, IMetricDefinitionService,
  IReferenceDataQueryService, IGoalService, IHourlyDistributionQueryService, IClock, IScheduleEvaluator? scheduler = null,
  ITickSourceFactory? ticks = null)` — costruito da `HabitScreenViewModel` con le dipendenze che ha già (senza scheduler
  usa `new Recordum.Data.Services.Evaluators.ScheduleEvaluator()`: verificare che abbia un costruttore senza argomenti);
  `[ObservableProperty] ImmutableArray<HabitFigure> Items` (3 o 4 elementi); `[ObservableProperty] string? UsualHour`
  (ora del picco degli ultimi 90 giorni, come `ActionTodayContent.ResolveUsualHour`; null per Abstinence e senza dati);
  `[ObservableProperty] decimal? DailyTarget` (obiettivo giornaliero del contatore a tocco, per "5/8");
  `Task LoadAsync(HabitNode node, HabitConfig? config, HabitCase @case, DayBoundary day, CancellationToken ct = default)`;
  `IDisposable` (l'orologio vivo dell'astinenza, Task 3).
- Definizioni (fonti già esistenti: `GetHeatmapAsync` conteggi per giorno, `GetDailySumAsync` somme per giorno, entrambe
  già col confine dell'abitudine; `IStreakAggregator.GetHistoryAsync` dalla creazione; nessuna query nuova in Data):

| Caso | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| Action | serie attuale (la run `IsCurrent`) | serie migliore (run più lunga dalla creazione) | % 30 g (giorni fatti ÷ giorni previsti nella finestra) | in tutto (giorni con ≥ 1 registrazione) |
| CounterTap | oggi (conteggio) | media 30 g (conteggi ÷ giorni della finestra, una cifra decimale) | serie obiettivo (run `IsCurrent`) | in tutto (somma dei conteggi) |
| State | oggi (etichetta della scala del valore di oggi, "—" se manca) | media 30 g (media dei giorni con valore, una cifra) | andamento: "↗" / "↘" / "→" (media degli ultimi 7 giorni con valore contro i 23 prima, soglia 0,25; "—" se un lato è vuoto) | giorni segnati "27/30" (giorni con valore / giorni della finestra) |
| Journal | voci della settimana (da lunedì a oggi) | serie (run `IsCurrent`) | voci in tutto | — (tag del mese: facoltativo, escluso) |

- Stringhe (en / it): `Figures.CurrentStreak` current streak / serie attuale · `Figures.BestStreak` best streak / serie
  migliore · `Figures.Pct30` last 30 days / 30 giorni · `Figures.Total` in total / in tutto · `Figures.Today` today / oggi ·
  `Figures.Avg30` 30-day average / media 30 g · `Figures.GoalStreak` goal streak / serie obiettivo · `Figures.Trend` trend /
  andamento · `Figures.DaysMarked` days marked / giorni segnati · `Figures.EntriesWeek` entries this week / voci della
  settimana · `Figures.Streak` streak / serie · `Figures.Entries` entries / voci. Numeri con i separatori della cultura
  ("1.932", "6,4"); percentuale "63%".

- [ ] **Step 1: test rossi** (`it-IT`, clock 28/10/2026 10:00, reset 05:00, servizi finti con dati costruiti):
  `Action_figures` (serie 4, migliore 12, 63%, 148); `Action_pct_counts_only_scheduled_days` (programma lun-mer-ven,
  fatti tutti i giorni previsti degli ultimi 30 → "100%"; lo stesso dato su "ogni giorno" → meno di 100%: il test morde);
  `CounterTap_figures` con `DailyTarget` = 8; `State_figures` con l'etichetta e "27/30"; `State_trend_arrows` (su, giù,
  piatto, "—"); `Journal_has_three_figures`; `Before_the_reset_today_is_yesterday` (alle 04:30 del 28 "oggi" del contatore
  è il conteggio del 27); `New_habit_has_sane_figures` (creata alle 08:00 di oggi, nessun dato: "0", "0%", nessun `NaN`;
  creata 5 giorni fa con 5 giorni fatti → "100%", non "17%"); `Usual_hour_is_the_peak_hour`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare** (una funzione privata per caso; finestra e giorni previsti calcolati
  una volta). **Step 4: verdi + Presentation intera.**
- [ ] **Step 5: commit** `feat(presentation): i quattro numeri di azione, contatore a tocco, stato e diario`.

### Task 3: I quattro numeri — misurato, serie, sessione, astinenza

**Files:**
- Modify: `Figures/HabitFiguresViewModel.cs`, `Strings*.resx` + `Strings.Designer.cs`
- Test: `HabitFiguresTests.cs`

**Interfaces:**
- Consumes: Task 2. Riusa `GoalProgressCardViewModel` (stessa definizione di avanzamento: `Actual`, `PercentComplete`,
  `PeriodLabel`, `IsEmpty`) costruendolo con le dipendenze che `HabitFiguresViewModel` ha già.

| Caso | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| Measured, con obiettivo | `Actual` dell'obiettivo, didascalia `PeriodLabel` ("questa settimana") | % obiettivo (`PercentComplete`) | media al giorno (somma della finestra 30 g ÷ giorni) | in tutto (somma dalla creazione) |
| Measured, senza obiettivo | oggi | serie attuale | media al giorno | in tutto |
| Series | serie oggi (registrazioni di oggi) | volume oggi (primo campo quantità; senza: "serie in tutto") | giorni a settimana (giorni con registrazioni negli ultimi 28 ÷ 4, una cifra) | sedute (giorni con registrazioni dalla creazione) |
| Session | oggi (minuti) | serie (run `IsCurrent`) | media 30 g (minuti al giorno) | in tutto (ore, una cifra sotto 10) |
| Abstinence | adesso (orologio vivo dall'inizio del periodo aperto, come `StreakSummaryViewModel`; "Non iniziata" senza periodo aperto) | il periodo più lungo (giorni) | ricadute in 90 g (periodi chiusi con fine negli ultimi 90 giorni, come `StreakSummaryViewModel`) | giorni liberi in tutto (somma delle durate dei periodi, l'aperto fino ad adesso) |

- I campi tempo: `MetricType.Seconds` con somme in secondi; si scrivono in minuti o ore con
  `Recordum.Core.Formatting.SecondsDisplay` dove serve. **Trappola**: verificare quale metrica riceve il tempo quando
  `TimerControlViewModel` ferma una sessione e usare quella (la prima `Seconds` per `SortOrder`, se è così).
- Stringhe (en / it): `Figures.AvgPerDay` per day / media al giorno · `Figures.GoalPct` of goal / obiettivo ·
  `Figures.SetsToday` sets today / serie oggi · `Figures.VolumeToday` volume today / volume oggi · `Figures.SetsTotal` sets
  in total / serie in tutto · `Figures.DaysPerWeek` days a week / giorni a settimana · `Figures.Sessions` sessions / sedute ·
  `Figures.Now` now / adesso · `Figures.Longest` longest / il più lungo · `Figures.Lapses90` lapses in 90 days / ricadute in
  90 g · `Figures.CleanDays` clean days in total / giorni liberi in tutto · `Figures.Value.Minutes` "{0} min" ·
  `Figures.Value.Hours` "{0} h" · `Figures.Value.Days` "{0} d" / "{0} g".

- [ ] **Step 1: test rossi**: `Measured_with_goal` ("96", "questa settimana", "64%"); `Measured_without_goal`;
  `Series_volume_is_the_first_quantity_field` (campi ripetizioni × kg, 4 serie da 10 rip. → volume 40 con il nome del
  campo, **non** 400·kg); `Series_without_quantity_shows_total_sets`; `Session_figures_in_minutes_and_hours`;
  `Abstinence_figures` (periodi 10 g chiuso, 41 g chiuso, 12 g aperto: "il più lungo" 41, "giorni liberi" 63);
  `Abstinence_not_started`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + Presentation intera.**
- [ ] **Step 5: commit** `feat(presentation): i quattro numeri di misurato, serie, sessione e astinenza`.

### Task 4: Il periodo e la mappa

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitScreen/History/HistoryPeriod.cs`, `HistoryPeriodViewModel.cs`,
  `HistoryMapViewModel.cs`, `HistoryDayCell.cs`
- Modify: `HabitScreenViewModel.cs` (proprietà `Period` e `Map`; i marker da `LoadMarkersAsync` di oggi), `Strings*`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/History/HistoryPeriodTests.cs`, `HistoryMapTests.cs`

**Interfaces:**
- Produces: `public enum HistoryPeriod { Week, Month, ThreeMonths, Year, All, Custom }`.
- Produces: `HistoryPeriodViewModel(IClock clock)`: `[ObservableProperty] HistoryPeriod Period` (default `Month`);
  `DateOnly From`, `DateOnly To`, `string Label`; `void SetContext(DayBoundary day, DateOnly habitCreated)`;
  `IRelayCommand PreviousCommand`, `NextCommand` (`CanExecute` falso quando il periodo contiene oggi), `TodayCommand`;
  `void SetCustom(DateOnly a, DateOnly b)` (ordina gli estremi; `Period` = Custom); `event EventHandler? Changed`.
  Settimana da lunedì; Mese = mese di calendario; 3 mesi = il mese dell'ancora e i due prima; Anno = anno di calendario;
  Tutto = dal 1° gennaio dell'anno di creazione a oggi. Etichette: "20–26 ott 2026", "ottobre 2026", "ago – ott 2026",
  "2026", "Tutto", "3 ott – 12 ott 2026" (formati della cultura).
- Produces: `public enum MapShape { Week, Month, Heatmap, YearHeatmaps }`;
  `public sealed partial class HistoryDayCell : ObservableObject` con `DateOnly Date`, `bool InRange` (fuori mese / fuori
  periodo), `bool IsToday`, `bool IsFuture`, `[ObservableProperty] int Step` (0–4), `[ObservableProperty] bool IsLapse`,
  `[ObservableProperty] int NoteCount`, `[ObservableProperty] bool IsSelected`, `string? Value` (per il tooltip).
- Produces: `HistoryMapViewModel(CalendarMarkerProvider markers)`: `MapShape Shape`; `ObservableCollection<HistoryDayCell> Cells`
  (Week, Month); `ObservableCollection<IReadOnlyList<HistoryDayCell>> Weeks` (colonne di 7 per la heatmap);
  `ObservableCollection<HistoryYear> Years` con `record HistoryYear(int Year, IReadOnlyList<IReadOnlyList<HistoryDayCell>> Weeks)`
  (Tutto); `Task LoadAsync(DateOnly from, DateOnly to, MapShape shape, DateOnly today, DateOnly? selected, CancellationToken ct)`;
  `IRelayCommand<HistoryDayCell> SelectCommand` (ignora i giorni futuri); `event EventHandler<DateOnly>? DaySelected`.
  Forma: Week → Week, Month → Month, ThreeMonths/Year/Custom → Heatmap, All → YearHeatmaps.
- Marker → cella in una funzione sola: `Tint == Danger && TintStep == 4` → `IsLapse` (la ricaduta è un taglio); `Tint ==
  Danger && TintStep == 1` (giorno di buco) → `Step` 0; altrimenti `Step = TintStep`.
- `HabitScreenViewModel`: `Map.DaySelected` → `SelectDayAsync`; `SelectDayAsync` aggiorna `IsSelected` sulle celle;
  `Period.Changed` → `Map.LoadAsync` e l'elenco del periodo (Task 5); gli eventi che oggi chiamano `Calendar.JumpAsync()`
  ricaricano anche la mappa. Stringhe: `History.Period.Week|Month|ThreeMonths|Year|All` (Week/Settimana, Month/Mese, 3
  months/3 mesi, Year/Anno, All/Tutto), `History.Period.Custom` From… to… / Dal… al…, `History.Period.Today` Today / Oggi.

- [ ] **Step 1: test rossi.** `HistoryPeriodTests` (`it-IT`, oggi 28/10/2026): `Week_is_monday_to_sunday`;
  `Month_steps_across_the_year` (da gennaio 2026 `Previous` → dicembre 2025, `Next` → gennaio 2026); `Next_stops_at_today`;
  `Three_months_is_the_month_and_two_before`; `All_starts_on_january_of_the_creation_year`; `Custom_range_is_ordered`
  (`SetCustom(12/10, 3/10)` → From 3/10); `Labels` (le cinque etichette sopra).
  `HistoryMapTests`: `Week_has_seven_cells`; `Month_marks_days_outside_the_month` (`InRange` falso per i giorni di
  settembre e novembre nella griglia di ottobre); `Lapse_is_a_cut_not_a_red_day`; `Future_days_are_not_selectable`;
  `All_has_one_heatmap_per_year` (dati dal 2021: sei `Years`, l'ultimo 2026); `All_with_no_data_is_the_current_year`;
  `Notes_count_reaches_the_cell`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + Presentation intera.**
- [ ] **Step 5: commit** `feat(presentation): un periodo che comanda mappa e Cronologia; la mappa cambia forma`.

### Task 5: Il giorno scelto, la sua nota, l'elenco del periodo

**Files:**
- Modify: `HabitScreenViewModel.cs`, `CompletionRowViewModel.cs` (additivo), `CompletionListViewModel.cs` (additivo), `Strings*`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/HistoryListTests.cs`, `DayNoteSyncTests.cs` (nuovi)

**Interfaces:**
- Consumes: `HistoryPeriodViewModel` (Task 4).
- Produces: `HabitScreenViewModel.PeriodRecords : HabitRecordsViewModel` — un secondo pannello (stessi figli: nuova
  `CompletionListViewModel` e nuova `AbstinencePeriodListViewModel`) in `Mode = Periodo`, `SelectedPeriod = Personalizzato`,
  `CustomFrom/To` = `Period.From/To`; ricaricato su `Period.Changed` e sugli eventi di completamento. `Records` resta il giorno
  scelto (Giorno).
- Produces (su `CompletionRowViewModel`): `string DayLabel` ("mer 28 ott", cultura corrente, giorno del confine);
  `string TimeLabel` ("7:42", ora locale, formato breve della cultura); `string? Preview` (prima riga non vuota di `Notes`,
  senza i segni Markdown di titolo/elenco; per un diario il testo della voce). Chiude UX-9 dove si usano al posto di
  `Display`.
- Produces (su `CompletionListViewModel`): `[ObservableProperty] bool OnlyWithNotes` (filtra le righe con `Preview` non
  vuota senza rileggere il DB); `bool IsFilteredEmpty`.
- Comportamenti: `SelectDayAsync` salva la nota del giorno prima di cambiare giorno quando `DayNote.CanSave`;
  `OnNoteEvent` ricarica `DayNote` per il giorno scelto **solo se non ha modifiche** (oggi non lo ricarica: la nota scritta
  nella colonna Note non compariva nel giorno). Stringhe: `History.Filter.All` All / Tutto, `History.Filter.WithNotes` With
  notes / Con note, `History.List.Empty` Nothing recorded in this period. / Niente registrato in questo periodo.,
  `History.DayNote.Placeholder` Write a note for this day… / Scrivi una nota per questo giorno…, `History.Day.Today`
  "{0} · today" / "{0} · oggi".

- [ ] **Step 1: test rossi.** `HistoryListTests`: `Period_list_follows_the_period` (settimana → solo le registrazioni dal 26
  ottobre al 1 novembre, dalla più nuova); `Rows_have_day_and_time_labels` (`it-IT`: "mer 28 ott", "7:42"; con reset 05:00
  una registrazione delle 04:30 del 29 ha `DayLabel` del 28); `With_notes_filters_without_reloading`;
  `Journal_preview_has_no_markdown` ("## Al lago" → "Al lago"); `Abstinence_uses_the_period_list`.
  `DayNoteSyncTests`: `A_note_saved_in_the_notes_column_shows_in_the_day` (salvata da `NotesViewModel` per il giorno scelto →
  `DayNote.Text` uguale); `A_dirty_day_note_is_not_overwritten_by_an_event`; `Changing_day_saves_the_day_note`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + Presentation intera + build WPF.**
- [ ] **Step 5: commit** `feat(presentation): il giorno scelto con la sua nota e l'elenco del periodo`.

### Task 6: La testa della schermata

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Head/HabitHeadView.axaml(.cs)`, `HeadCommandTemplate.cs`
- Modify: `src/Recordum.UI/Views/HabitScreen/HabitScreenView.axaml(.cs)` (via la riga del nome, `TodayHost`, `ChipBar`;
  `ExitFocusButton` resta), `src/Recordum.UI/Views/HabitScreen/CronologiaColumnView.axaml(.cs)` (via `QuickAddHost` e
  `TimerHost`), `Strings*`
- Test: `tests/Recordum.UI.Tests/HabitScreen/HabitHeadViewTests.cs` (nuovo), `HabitScreenViewTests.cs` aggiornato

**Interfaces:**
- Consumes: `Case`, `SelectedDay`, `IsSelectedDayToday`, `BackToTodayCommand`, `UndoLastCommand` (Task 1); `Figures` (Task 2–3).
- Produces: `HabitHeadView` (`x:DataType="vm:HabitScreenViewModel"`), larga quanto la scheda:
  riga 1 — nome (`x:Name="HabitName"`, 20/600, `HabitTextBrushConverter`), sotto il programma (`ScheduleText`) e "di solito
  verso le {0}" se `Figures.UsualHour`; a destra il comando (`x:Name="CommandHost"`, costruito da `HeadCommandTemplate` per
  `Case`: in questo task ospita le view dei quick-add così come sono, `TimerControlView` per Session, `JournalTodayView` per
  Journal) e sotto `x:Name="UndoButton"` ("annulla", `Button.ghost`, `UndoLastCommand`) · `x:Name="OtherDayButton"` ("per un
  altro giorno", apre un `CalendarDatePicker` in un `Flyout`; la scelta chiama `SelectDayAsync`; date future spente);
  con `!IsSelectedDayToday` una riga "per {giorno}" + "torna a oggi";
  riga 2 — i numeri (`x:Name="Figures"`, `ItemsControl` su `Figures.Items`: valore 20/600 nella variante "testo",
  didascalia 12 attenuata; `UniformGrid` a 4 colonne che va a 2 sotto i 420 px);
  in alto a destra il pulsante Esporta, nascosto come oggi (F6);
  riga 3 — i pulsanti delle colonne (`x:Name="ColumnButtons"`): Diario (solo diari, `Icon.PenLine`, chiude **UX-13**),
  Cronologia (`Icon.Calendar`), Note (`Icon.BookOpen`), Configura (`Icon.SlidersHorizontal`); Dettaglio non ha pulsante (F3); `ToggleButton` legati a
  `IsDiarioOpen`, `IsCronologiaOpen`, `IsNoteOpen`, `IsConfiguraOpen`. Un gruppo vede solo nome e Configura (la sua testa è
  di 2b).
- Produces: `public static string ScheduleText.Describe(HabitConfig? config, CultureInfo culture)` in Presentation
  (`ViewModels/HabitScreen/ScheduleText.cs`): Daily "Every day"/"Ogni giorno"; SpecificDays i giorni brevi della cultura
  ("lun, mer, ven"); WeeklyX "{0} times a week"/"{0} volte a settimana"; EveryXDays "Every {0} days"/"Ogni {0} giorni";
  Monthly "Monthly"/"Ogni mese"; Custom e null → stringa vuota. Test in Presentation (`ScheduleTextTests`).
- Stringhe: `Head.UsualHour` usually around {0} / di solito verso le {0} · `Head.Undo` undo / annulla · `Head.OtherDay`
  for another day / per un altro giorno · `Head.RecordingFor` for {0} / per {0} · `Head.BackToToday` back to today / torna a
  oggi · `Schedule.*` sopra.
- La fascia `TodayStripViewModel` resta caricata (la legge WPF); su Avalonia nessuna view la mostra più.

- [ ] **Step 1: test rossi.** `HabitHeadViewTests` (`[AvaloniaFact]`, `TestCulture.Pin()`): `Name_is_20_semibold_in_the_text_variant`;
  `Schedule_and_usual_hour_under_the_name`; `Head_shows_four_figures` (Action) e `Journal_head_shows_three`;
  `One_command_per_case` (per ognuno degli otto casi, `CommandHost` contiene una e una sola view del tipo atteso);
  `Other_day_shows_the_day_and_back_to_today`; `Undo_is_hidden_for_abstinence`; `There_is_no_today_strip_nor_chip_bar`
  (nessun `TodayStripView`, nessun `CheckBox.segment` nell'albero della schermata: **UX-8**); `Cronologia_has_no_quick_add`;
  `Narrow_head_has_no_overlap` (scheda a 480 px: rettangoli di nome, comando, numeri e pulsanti delle colonne non si
  intersecano); `Column_buttons_use_lucide_icons` (nessun `TextBlock` con "✎").
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI (`Category!=Performance`).**
- [ ] **Step 5: commit** `feat(ui): la testa dell'abitudine — un comando, quattro numeri, i pulsanti delle colonne (UX-7, UX-8, UX-13)`.

### Task 7: Gli otto comandi

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Head/ActionCommandView.axaml(.cs)`, `CounterTapCommandView`, `StateCommandView`,
  `AbstinenceCommandView`, `SessionCommandView`, `JournalCommandView`
- Modify: `HeadCommandTemplate.cs`, `src/Recordum.UI/Views/HabitScreen/QuickAdd/MeasuredQuickAddView.axaml` e
  `SeriesQuickAddView.axaml` (restyle: via la riga "Registrando per", che ora sta sotto il comando), `TimerControlView.axaml`, `Strings*`
- Test: `tests/Recordum.UI.Tests/HabitScreen/HeadCommandTests.cs` (nuovo)

**Interfaces:**
- Consumes: `ActionQuickAddViewModel.IsDone/ToggleCommand` (Task 1), `Figures.DailyTarget` (Task 2), i quick-add di oggi,
  `TimerControlViewModel`, `JournalTodayContent`.
- Il pulsante che registra ha la classe `record`: bordo e testo nella variante "testo" del colore, livello di tinta
  `Opacity.RowTint`; "fatto" = pieno nel colore con il testo `Brush.OnInk`. Lo stile `Button.record` sta in
  `Themes/ControlStyles.axaml`.
- Per caso (spec §6.2): **Action** `ToggleButton` "Fatto oggi" / "Fatto" su un altro giorno, con l'icona check, legato a
  `IsDone`/`ToggleCommand`; **CounterTap** "−" (`UndoCommand` del quick-add, sul giorno scelto) · "+1" · "5/8" (`DayCount`/`DailyTarget`, solo con
  l'obiettivo); **Measured** e **Series** le view dei quick-add con il pulsante finale in `record` ("Registra" / "Registra
  {0} serie"); **Session** fermo: "▶ Inizia" (`record`) e "+ a mano" (apre sotto le celle del quick-add a serie); mentre corre:
  il tempo 20/600 +tnum nella variante "testo", Pausa/Riprendi, Segmento successivo (solo `TimerMode.Session`), Ferma;
  **State** con una sola cella scala e al più 10 gradini: un gradino per etichetta (`ToggleButton` in fila, l'etichetta sotto),
  un clic registra quel valore per il giorno scelto; altrimenti le celle e "Registra"; accanto "ultimo: {0}"
  (`Today_State_LastValue`); **Abstinence** "Ho ceduto…" (`record` col `Brush.Danger` come colore) che apre un `Flyout` con
  la frase e "Conferma" → `LapseCommand`; senza periodo aperto "Inizia" (`StartCommand`); **Journal** "Scrivi" o "Nota
  veloce" secondo il gesto (`PrimaryCommand` di `JournalTodayContent`, apre la colonna Diario).
- Stringhe: `Head.Action.DoneToday` Done today / Fatto oggi · `Head.Action.Done` Done / Fatto · `Head.Session.Start` Start /
  Inizia · `Head.Session.Manual` + by hand / + a mano · `Head.Abstinence.Lapse` I gave in… / Ho ceduto… ·
  `Head.Abstinence.LapseConfirm` Record a lapse now? The current period ends here. / Registrare una ricaduta adesso? Il
  periodo in corso finisce qui. · `Head.Confirm` Confirm / Conferma · `Head.Counter.Progress` "{0}/{1}".

- [ ] **Step 1: test rossi.** `HeadCommandTests` (headless, servizi veri su DB di test come gli altri test di schermata):
  `Action_click_records_and_click_again_removes`; `Action_on_another_day_records_there`; `CounterTap_plus_and_minus`
  (+1 due volte, − una: `DayCount` 1; "1/8" con l'obiettivo); `State_step_click_records_that_value`;
  `Abstinence_lapse_asks_first` (clic su "Ho ceduto…" non chiude il periodo finché non si conferma);
  `Session_start_shows_the_running_controls`; `Journal_command_opens_the_diary_column`;
  `Record_button_uses_the_habit_text_variant` (bordo == variante "testo" calcolata per il colore dell'abitudine);
  `There_is_no_recording_for_row` (nessun `TextBlock` con `Abitudine_QuickAdd_RecordingFor` nelle view dei quick-add).
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): gli otto comandi in testa, nel colore dell'abitudine`.

### Task 8: La colonna Cronologia

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/History/PeriodBarView.axaml(.cs)`, `HistoryMapView.axaml(.cs)`,
  `DayBlockView.axaml(.cs)`, `PeriodListView.axaml(.cs)`
- Modify: `CronologiaColumnView.axaml(.cs)` (corpo nuovo), `Records/CompletionListView.axaml`,
  `Records/AbstinencePeriodListView.axaml`
- Delete: `src/Recordum.UI/Views/HabitScreen/Calendar/CalendarNavigatorView.axaml(.cs)` (nessun altro la usa: verificarlo)
- Test: `tests/Recordum.UI.Tests/HabitScreen/CronologiaColumnTests.cs` (nuovo o esteso)

**Interfaces:**
- Consumes: `Period`, `Map`, `Records`, `PeriodRecords`, `DayNote` (Task 4–5).
- Corpo, dall'alto: **barra del periodo** (cinque segmenti, ‹ etichetta ›, "Dal… al…" con due `CalendarDatePicker` in un
  `Flyout` → `SetCustom`, "Oggi"); **mappa** (`HistoryMapView` per `Shape`: Week = 7 caselle da 44 px col giorno breve sopra;
  Month = griglia 7×6, numero del giorno **12 px centrato** (UX-14), fuori mese = numero in `Brush.TextFaint` senza cella,
  giorno vuoto del mese = cella `Brush.Cell`, oggi con l'anello di 1,5 px `Brush.Text`, selezione = bordo di 2 px
  `Brush.Ink` (≠ completamento, F0-13), il valore solo nel tooltip; Heatmap = celle da 11 px a colonne di settimana;
  YearHeatmaps = una heatmap per anno con l'anno sopra, dal più recente; gradini con `Brush.Cell` e `Opacity.Heat1..4` del
  colore dell'abitudine; `IsLapse` = una tacca verticale di 2 px `Brush.Background` al centro della cella piena; il segno
  delle note = il punto di oggi nel colore `NoteMarkerColor`); **il giorno** (`DayBlockView`: "mercoledì 28 ottobre · oggi",
  le registrazioni del giorno con ora e × (`Records`), la nota del giorno in `Font.Serif` 14 con bordo tratteggiato e il
  segnaposto quando è vuota, "Salva" `Button.primary` visibile solo con `DayNote.CanSave`; per un diario la nota resta
  nascosta come oggi); **Cronologia** (`PeriodListView`: titolo 16/600, filtro Tutto · Con note, righe "giorno · anteprima ·
  ora" con `DayLabel`, `Preview`, `TimeLabel`; per un diario la riga mostra la voce **resa** con `c:DiaryEditor
  IsReadOnly="True"` e le foto, chiude **UX-10**; per l'astinenza i periodi con "In corso"; vuoto = `History.List.Empty`).
- Nessuna didascalia ripete il nome della colonna dentro la colonna (UX-8): le sezioni si chiamano "Mappa", il giorno, "Cronologia".
- Stringhe: `History.Map` Map / Mappa · `History.List` History / Cronologia.

- [ ] **Step 1: test rossi** (`[AvaloniaFact]`, PNG solo con l'asserzione): `Period_bar_switches_the_map_shape` (Mese → 42
  celle; Settimana → 7; Anno → colonne di settimana; Tutto con dati 2025–2026 → due blocchi d'anno);
  `Month_day_numbers_are_12px_and_centered`; `Out_of_month_differs_from_empty` (nessuna cella di sfondo sotto un giorno fuori
  mese, `Brush.Cell` sotto un giorno vuoto); `Selected_is_not_completed` (bordo ink sulla selezionata, fondo invariato);
  `Clicking_a_day_selects_it_for_recording` (clic sul 27 → testa "per lunedì 27 ottobre"); `Day_note_is_serif_with_a_dashed_frame`;
  `Delete_a_record_from_the_day`; `Journal_history_is_rendered` (una voce "## Al lago\n**fredda**": nessun `TextBlock` con
  "##" o "**"); `With_notes_filter`; `Next_is_disabled_on_the_current_period`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): la Cronologia — periodo, mappa, il giorno con la nota, l'elenco reso (UX-9, UX-10, UX-14)`.

### Task 9: La colonna Note e le colonne

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Notes/NotesColumnView.axaml(.cs)`
- Modify: `HabitScreenView.axaml(.cs)` (quarta colonna; `UpdateColumns`), `Strings*` solo se manca qualcosa (le chiavi
  `Notes_*`, `Records_Mode_*`, `Period_From/To` ci sono)
- Test: `tests/Recordum.UI.Tests/HabitScreen/NotesColumnTests.cs`, `HabitScreenViewTests.cs`

**Interfaces:**
- Consumes: `NotesForColumn : NotesViewModel?` (esiste: null a colonna chiusa), `ShowNote`, `NoteWidth`.
- Porting di `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml` (riferimento di comportamento): Giorno/Periodo con il
  periodo (`EntriesPeriod`) e Dal/Al; elenco delle note (`Notes`, `SelectedNote`: data d'ancoraggio, categoria, ora,
  "modificata alle…", riepilogo); editor `c:DiaryEditor` legato a `EditorMarkdown` (TwoWay); Nuova · Salva (`Button.primary`)
  · Elimina con la conferma sul posto (`IsConfirmingDelete`); allegati (miniature da `Thumbnail`, segnaposto con
  `Icon.ImageOff`, aggiungi, × per togliere, clic = visore); visore a tutta colonna (`IsLightboxOpen`, `LightboxContent`,
  ‹ › con `CanNavigateLightbox`, chiudi ed Esc); cronologia delle versioni (`IsShowingHistory`, `History.Versions`,
  anteprima resa con `DiaryEditor` in sola lettura, Ripristina, Indietro).
- Colonne: ordine Diario · Cronologia · Note · Configura; Note a stella con `MinWidth` 320 come Diario; la maniglia resta solo
  sul confine Cronologia|Configura; il corpo della Note si costruisce a colonna aperta e si butta alla chiusura (come
  Cronologia e Configura). Ogni testa di colonna: titolo 13/600 attenuato, "a tutta finestra" e × (ci sono; uguale per Note).
- Prima di cambiare nota o chiudere la colonna con `IsDirty`: la stessa domanda che `NotesViewModel` fa oggi (verificarlo;
  se non la fa, il cambio di selezione salva — mai perdere il testo).

- [ ] **Step 1: test rossi:** `Note_button_opens_the_notes_column`; `Notes_column_lists_and_edits` (seleziona, scrive, Salva
  → la nota nel DB ha il testo); `Delete_asks_in_place`; `Attachments_show_thumbnails_and_open_the_viewer` (Esc chiude);
  `Version_history_restores`; `Four_columns_fit_with_scroll` (tutte aperte a 1000 px: nessuna sotto 320, lo `ScrollViewer`
  scorre); `A_note_written_in_the_column_shows_in_the_day_block` (Cronologia e Note aperte, salva in Note → il blocco del
  giorno la mostra).
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): la colonna Note portata da WPF`.

### Task 10: Schermate, inventario, prestazioni, documenti

**Files:**
- Modify: test di screenshot della schermata dell'abitudine, `docs/phase-4-issues.md`, `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1: PNG in chiaro e in scuro**, ognuno con un'asserzione strutturale: la schermata per ciascuno degli otto casi
  con Cronologia aperta (periodo Mese); Settimana, Anno e Tutto per un'azione; un giorno passato scelto; la colonna Note con
  una nota e un allegato; il visore; tutte le colonne aperte; la schermata a 480 px. Guardarli **prima** di chiudere il task
  (memoria dell'autore: i difetti visivi li trovano le immagini); ciò che si vede storto diventa un test.
- [ ] **Step 2: prestazioni.** In locale, alternando due volte prima (primo commit del branch) e dopo: `Category=Performance`
  di UI e Data; più un test nuovo `[Trait("Category","Performance")]` che apre la schermata di un'abitudine con tre anni di
  registrazioni giornaliere e misura `LoadAsync` + mappa "Tutto" (riferimento: la Home con 300 righe < 500 ms). Numeri nel
  report.
- [ ] **Step 3: inventario.** Le voci "Schermata dell'abitudine" e "Diario" dell'Appendice A: ognuna trovata nel nuovo albero o
  nel suo comando (colonne Diario, Cronologia, Note, Configura; focus ed Esc; Esporta nascosto; periodo con ‹ ›; registrare
  per un altro giorno; elimina registrazione; gli otto comandi; astinenza con periodi "in corso" e ricadute in 90 g; timer
  avvia/pausa/riprendi/ferma/segmento; segno delle note; la colonna Note intera; il diario invariato). Esito nel report.
  `UX-P1`: aprire Configura in italiano nel test con la cultura fissata e leggere le etichette; se è la cultura del test,
  correggere il test.
- [ ] **Step 4: documenti.** Tracker: UX-7, UX-8, UX-9, UX-10, UX-13, UX-14 `fixed` (con il test che lo prova); ROADMAP
  "Dove siamo" (2a fatta, prossimo il piano 2b) e MILESTONES. Commit e push.
- [ ] **Step 5: review dell'intero branch** (modello più capace) con la spec in mano; un'onda di correzioni; **CI verde** su
  Linux e Windows; push. Il merge in `main` lo fa l'autore, dopo aver guardato i PNG.

## Fuori da 2a

- Gruppo (Panoramica, "Insieme", figlie segnabili), Configura in cinque blocchi, scelta del colore: piano 2b.
- I minori della review dell'onda 1 che toccano la Home e la cornice (variante "testo" sulla riga selezionata, icona di
  "fatte", intestazione col giorno dell'app, tempo del timer 15/600 nella barra, errore fatale senza marchio, avviso doppio al
  primo avvio, ricostruzioni della `HeatmapStrip`): 2b, che riusa le righe della Home per le figlie.
- `YearHeatmapControl` resta non montato (F7): la mappa di 2a disegna le sue heatmap da `HistoryMapViewModel`.
- Il Dettaglio (colonna nascosta) e l'export restano a F3 e F6.
