# Direzione visiva, onda 2b — Il gruppo, Configura in cinque blocchi, la scelta del colore

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la schermata del gruppo della spec §6.3 su Avalonia (testa con l'avanzamento di oggi e i numeri del gruppo, le
figlie segnabili col log rapido della Home, "Insieme", la mappa dell'anno), Configura nei cinque blocchi della §6.4 con le
righe-riassunto (e la Configura del gruppo), la scelta del colore della §7.1; più i minori rimasti delle onde 1 e 2a che
toccano le stesse viste. Senza perdere una funzione.

**Architecture:** una query nuova in Data (`IGroupHistoryQueryService`) dà, per ogni figlia di un gruppo, i giorni previsti
e i giorni fatti, ciascuno col confine della figlia: è la sola fonte dei numeri del gruppo, di "Insieme" e della mappa. In
Presentation tipi **nuovi** (`GroupFiguresViewModel`, `TogetherViewModel`) e membri **aggiunti** a `PanoramicaViewModel`,
`HabitScreenViewModel` e alle sezioni di Configura (un `Summary` per sezione); WPF compila e non cambia, tranne dove un task
lo dice. Su Avalonia le figlie sono **le righe della Home** (`HomeRowViewModel` + `HabitRow`), così log rapido, tinta e
striscia sono una cosa sola; la mappa del gruppo è la `HistoryMapViewModel` di 2a con un `CalendarMarkerProvider` costruito
dalla query.

**Tech Stack:** .NET 10, Avalonia 12.1.2, CommunityToolkit.Mvvm, EF Core 10 (query), xunit v3 + Avalonia.Headless (UI),
xunit (Presentation, Data).

**Spec:** `docs/superpowers/specs/2026-10-28-ux-direction-design.md` — §6.3, §6.4 (solo Configura: il creatore è
dell'onda 3), §7.1, §9 (onda 2), §10, Appendice A ("Configura"; "Home" per le righe riusate). Mockup: `gruppo.html`,
`colore.html` (la B; **la spec vince**: i Preferiti restano, dentro "Avanzate"), `integrazioni.html` parte 3,
`correzioni.html` parte 3. Modello di forma: il piano 2a (`docs/superpowers/plans/2026-10-29-ux-wave2a-habit-screen.md`).

## Global Constraints

- Branch: `ux2b-group-configure`, **impilato su `ux2-habit-screen`** (2a non è ancora in `main`). Se l'autore fa il merge di
  2a prima, si ribasa su `main`.
- Valgono i Global Constraints di 2a e dell'onda 1: scala 12/13/14/16/20, `Brush.*`, `Opacity.*`, `HabitTextBrushConverter`
  per i valori nel colore; colore dell'abitudine **solo** per il comando che registra; inchiostro (`Button.primary`) per una
  sola azione principale per finestra; contorno per le secondarie; `Button.ghost` per "annulla"; icone Lucide, nessun glifo.
- **Il giorno si chiede a `DayBoundary`**: il giorno di una registrazione è quello della **figlia** (batch
  `IResetTimeResolver.GetDayBoundariesAsync`), il "oggi" della schermata del gruppo è quello del **gruppo**
  (`_dayBoundary` di `HabitScreenViewModel`). Mai `DateOnly.FromDateTime(clock.UtcNow…)`.
- **L'inizio di un'abitudine** (UX2A-1, decisione dell'autore del 2026-10-30) è il **più vecchio** fra il giorno di creazione e il
  giorno della prima registrazione (per un'astinenza: l'inizio del primo periodo). Vale per l'abitudine sola (mappa "Tutto",
  "in tutto", finestre "30/90 giorni" di 2a) e per le figlie di un gruppo; nessun giorno prima dell'inizio è previsto. Una data
  d'inizio scelta dall'utente non serve: per un periodo qualunque c'è "Dal… al…". Archiviate e nel cestino fuori.
  I sottogruppi non sono figlie: le loro abitudini sì, a ogni profondità (come "3 di 5" in Home).
- **Fatto, per un'astinenza** = giorno pulito: coperto da un periodo, escluso il giorno della ricaduta; il periodo aperto
  copre fino a oggi. Per ogni altro tipo: almeno una registrazione quel giorno.
- WPF non si tocca e deve compilare. Comportamenti condivisi cambiati **solo** qui: Task 2 (il conto "x di y" di
  `HomeViewModel.ApplyGroupProgress` e di `PanoramicaViewModel` conta l'astinenza in corso come fatta). Ogni altro cambio
  in Presentation è additivo.
- Ogni stringa nuova in `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs` (**solo via shell**). Test con
  etichette: `TestCulture.Pin()`; test di Presentation con date e numeri: `it-IT` fissata.
- Avalonia: `InitializeComponent()`, `x:DataType` ovunque, niente `ContentControl` con `ContentTemplate` statico (la view si
  costruisce dal tipo, o solo a riga aperta); `[AvaloniaFact]` per i test con tipi Avalonia.
- Build con `--no-incremental`; mai `-p:UseAppHost=false`; `TreatWarningsAsErrors`. **Un test di regressione si prova rosso.**
- Servizio nuovo di Data: registrato in `RecordumServiceCollectionExtensions` **e** in `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
  accanto a `IHabitTreeQueryService`, con il test di registrazione DI di ciascun host.
- Configura salva campo per campo come oggi (`SectionViewModelBase.ApplyAsync`); nessun "Salva" nuovo.

## Review Focus

1. **Un'astinenza dentro un gruppo**: mentre è in corso conta come fatta oggi, in Home ("5 di 5") e nella testa del gruppo,
   con lo stesso numero; il giorno della ricaduta non è fatto → Task 1 (`Abstinence_clean_days_exclude_the_lapse_day`),
   Task 2 (`Abstaining_child_counts_as_done_in_home_and_group`).
2. **Figlie coi loro reset diversi**: una registrazione delle 04:30 su una figlia con reset 05:00 è del giorno prima, anche se
   il gruppo ha reset 00:00; il "oggi" delle colonne di "Insieme" è quello del gruppo → Task 1
   (`Done_days_use_each_childs_boundary`), Task 3 (`Together_today_is_the_groups_today`).
3. **Figlia nuova, archiviata o in un sottogruppo**: una figlia creata 5 giorni fa non rende "incompleti" i giorni prima; una
   archiviata non c'è; le figlie di un sottogruppo contano → Task 1 (`Scheduled_starts_at_creation`,
   `Archived_and_deleted_children_are_out`, `Subgroup_children_are_included_with_depth`), Task 2 (`Complete_days_ignore_days_before_a_child_existed`).
4. **Segnare dal gruppo**: il log rapido di un'azione o di un contatore a tocco registra e la riga, il "3 di 5" e i numeri si
   aggiornano senza riaprire; per un tipo a modulo (misurato, serie, sessione, diario, stato, astinenza) apre la figlia e non
   registra niente → Task 3 (`Quick_log_records_and_refreshes`, `Form_types_open_the_child`).
5. **Configura riorganizzata**: ogni campo di oggi resta raggiungibile (anche quelli spostati in "Avanzate") e si salva; un
   esadecimale non valido nel selettore non cambia il colore → Task 6 (`Every_field_of_today_is_reachable`,
   `An_advanced_field_still_saves`), Task 7 (`Invalid_hex_keeps_the_colour`).

---

### Task 1: La storia delle figlie di un gruppo (Data)

**Files:**
- Create: `src/Recordum.Core/Abstractions/Services/IGroupHistoryQueryService.cs`,
  `src/Recordum.Data/Services/Queries/GroupHistoryQueryService.cs`
- Modify: `src/Recordum.Presentation/Bootstrap/RecordumServiceCollectionExtensions.cs`,
  `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Test: `tests/Recordum.Data.Tests/Services/Queries/GroupHistoryQueryServiceTests.cs` (nuovo), i test DI dei due host

**Interfaces:**
- Produces:
  `public sealed record GroupChildHistory(Guid HabitNodeId, string Name, string ColorHex, HabitType Type, int Depth, DateOnly CreatedOn, ImmutableHashSet<DateOnly> Scheduled, ImmutableHashSet<DateOnly> Done);`
  `public interface IGroupHistoryQueryService { ValueTask<IReadOnlyList<GroupChildHistory>> GetAsync(Guid groupId, DateOnly from, DateOnly to, CancellationToken ct = default); }`
  (`CreatedOn` porta **l'inizio** dei Global Constraints, non il solo giorno di creazione: chiamarlo `StartsOn`.)
- Produces: `public interface IHabitStartQueryService { ValueTask<DateOnly> GetStartDayAsync(Guid habitNodeId, CancellationToken ct = default); }`
  — la regola dell'inizio scritta **una volta**, nel confine dell'abitudine; `GroupHistoryQueryService` usa la stessa funzione
  (in batch) e il Task 8 la usa per l'abitudine sola. Registrata come l'altra.
- Regole: figlie = abitudini discendenti non archiviate e non cancellate, in ordine d'albero (`SortOrder`, depth-first; `Depth`
  0 = figlia diretta). Confini in batch (`GetDayBoundariesAsync`), una volta. `Scheduled` = giorni da `max(from, CreatedOn)` a
  `min(to, oggi della figlia)` previsti da `ScheduleEvaluator` (un'astinenza: ogni giorno). `Done` = per l'astinenza i giorni
  puliti (Global Constraints) da `IAbstinenceService.ListPeriodsAsync`; per gli altri i giorni con ≥ 1 registrazione nel
  confine della figlia. Una sola query per le registrazioni di tutte le figlie nella finestra, non una per figlia.

- [ ] **Step 1: test rossi** (DB di test come gli altri test di query; clock 28/10/2026 10:00): `Empty_group_is_empty`;
  `Subgroup_children_are_included_with_depth` (A, poi sottogruppo con B: due righe, B con `Depth` 1);
  `Archived_and_deleted_children_are_out`; `Done_days_use_each_childs_boundary` (figlia con reset 05:00, registrazione alle
  04:30 del 28 → `Done` contiene il 27); `Scheduled_respects_the_schedule` (lun-mer-ven: in una settimana tre giorni);
  `Scheduled_starts_at_creation` (creata il 23, nessuna registrazione prima: niente prima del 23);
  `Starts_at_the_oldest_of_creation_and_first_record` (creata il 23 con una registrazione "per un altro giorno" il 18 →
  `StartsOn` 18, previsti dal 18; lo stesso per `GetStartDayAsync`); `Abstinence_starts_at_its_first_period`; `Future_days_are_neither`;
  `Abstinence_clean_days_exclude_the_lapse_day` (periodo dal 10 chiuso da una ricaduta il 15, nuovo periodo aperto dal 15:
  il giorno della ricaduta **non** è pulito anche se un periodo nuovo comincia quel giorno; 10–14 e 16–28 sì).
- [ ] **Step 2: rosso visto** (`dotnet test tests/Recordum.Data.Tests --filter "FullyQualifiedName~GroupHistoryQueryServiceTests"`, contare i test).
- [ ] **Step 3: implementare**, registrare nei due host. **Step 4: verdi** + Data intera + test DI.
- [ ] **Step 5: commit** `feat(data): i giorni previsti e fatti delle figlie di un gruppo`.

### Task 2: L'avanzamento e i numeri del gruppo

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitScreen/Figures/GroupFiguresViewModel.cs`,
  `src/Recordum.Presentation/ViewModels/GroupProgress.cs`
- Modify: `HomeViewModel.cs` (`ApplyGroupProgress` usa `GroupProgress`), `PanoramicaViewModel.cs` (idem, più
  `double TodayFraction`), `HabitScreenViewModel.cs` (parametro **opzionale in fondo** `IGroupHistoryQueryService? groupHistory = null`:
  WPF non lo passa e non ha i numeri del gruppo; proprietà `GroupFigures?`, caricata in `LoadAsync` per un gruppo **dopo**
  `Panoramica.LoadAsync`, che carica l'obiettivo, e sugli eventi di completamento e di astinenza), `Strings*`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/Figures/GroupFiguresTests.cs`,
  `tests/Recordum.Presentation.Tests/ViewModels/GroupProgressTests.cs` (nuovi)

**Interfaces:**
- Consumes: `IGroupHistoryQueryService` (Task 1), `HabitFigure` (2a), `PanoramicaViewModel.Goal` (`GoalProgressCardViewModel`).
- Produces: `public static class GroupProgress { public static bool IsDoneToday(HomeTreeRow row); public static (int Done, int Total) Count(ILookup<Guid?, HomeTreeRow> byParent, Guid groupId); }`
  — `IsDoneToday` = `IsCompletedToday` oppure (`HabitType == Resistance` e `AbstinenceStatus == Abstaining`). La usano
  `HomeViewModel` e `PanoramicaViewModel`: i due "x di y" non possono divergere.
- Produces: `GroupFiguresViewModel(IGroupHistoryQueryService history, IClock clock)`;
  `[ObservableProperty] ImmutableArray<HabitFigure> Items` (2–4 elementi);
  `Task LoadAsync(HabitNode group, DayBoundary day, GoalProgressCardViewModel goal, CancellationToken ct = default)` — una
  chiamata alla query, dal più vecchio fra la creazione del gruppo e lo `StartsOn` delle figlie, a oggi. Un giorno è **completo** quando ogni figlia prevista quel giorno è
  fatta; un giorno senza figlie previste non conta.

| # | Valore | Didascalia |
|---|---|---|
| 1 | giorni completi di fila: da oggi indietro; un giorno senza figlie previste si salta; oggi incompleto non interrompe (la giornata è aperta), un giorno passato incompleto sì | `Group.Figures.CompleteRun` complete days in a row / giorni completi di fila |
| 2 | % 30 g: giorni completi ÷ giorni con almeno una figlia prevista, finestra da `max(oggi − 29, inizio del gruppo)`; oggi con la stessa regola di `Action` in `HabitFiguresViewModel` (leggerla e riusarla) | `Figures.Pct30` (esiste) |
| 3 | obiettivo del gruppo, solo se `!goal.IsEmpty`: "{Actual} di {Target}" coi numeri della cultura | `goal.Name`, o `Group.Figures.Goal` group goal / obiettivo del gruppo se vuoto |
| 4 | la più saltata (*facoltativo*, spec §12.8): la figlia con più giorni previsti e non fatti nella finestra, oggi escluso; assente se nessuna ne ha | `Group.Figures.MostSkipped` most skipped / la più saltata |

- Stringhe in più: `Group.Progress.Today` "{0} of {1} done today" / "{0} di {1} fatte oggi" (sotto l'avanzamento in testa).

- [ ] **Step 1: test rossi** (`it-IT`, clock 28/10/2026 10:00, query finta): `Complete_run_counts_back_from_today`
  (completi 23–27, oggi a metà → 5); `A_day_without_scheduled_children_is_skipped`;
  `Complete_days_ignore_days_before_a_child_existed` (B con `StartsOn` 25: il 20 conta come completo con la sola A);
  `Pct30_is_on_days_with_something_scheduled`; `Goal_figure_only_with_a_goal`; `Most_skipped_is_the_child_with_most_misses`
  e `Most_skipped_absent_when_nothing_missed`; `Empty_group_has_no_nan` (nessuna figlia: niente `NaN`, `Items` con "0" e "0%").
  `GroupProgressTests`: `Abstaining_child_counts_as_done_in_home_and_group` (un'azione fatta, un'astinenza in corso, una non
  fatta → "2 di 3" sia in `HomeRowViewModel.GroupProgressText` sia in `PanoramicaViewModel.TodayLabel`; **rosso prima**:
  oggi dice "1 di 3"); `Relapsed_child_is_not_done`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + Presentation intera + build WPF.**
- [ ] **Step 5: commit** `feat(presentation): l'avanzamento e i numeri del gruppo; l'astinenza in corso conta come fatta`.

### Task 3: Le figlie segnabili, "Insieme", la mappa del gruppo

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitScreen/Group/TogetherViewModel.cs`, `TogetherRow.cs`
- Modify: `PanoramicaViewModel.cs` (additivo: `Rows`, `Together`, `Map`), `HabitScreenViewModel.cs` (il `Period` di 2a comanda
  anche "Insieme" per un gruppo; il log rapido che apre una figlia)
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/Group/PanoramicaRowsTests.cs`, `TogetherTests.cs` (nuovi)

**Interfaces:**
- Consumes: Task 1–2; `HistoryPeriodViewModel`, `HistoryMapViewModel`, `HistoryDayCell` (2a); `QuickActionHandlerSelector`.
- Produces (su `PanoramicaViewModel`, additivi; `Children` resta per WPF):
  `ObservableCollection<HomeRowViewModel> Rows` — costruite **come in `HomeViewModel.Rebuild`** dalle stesse `HomeTreeRow`
  (indentazione, `UiPrefs`, `AttachQuickActionSelector`, l'orologio vivo dell'astinenza), i sottogruppi come righe di gruppo,
  nessun `FrameSlices`, `CurrentTab = Active`; un costruttore che riceve in più `IHabitNodeService`, `IPurgeService`,
  `IGroupHistoryQueryService`, `IHabitUiPrefsService?`, `IStreakAggregator?`, `QuickActionHandlerSelector?` come **overload**
  (quello di oggi resta per WPF, e con quello `Rows`, `Together` e `Map` restano vuoti). `HabitScreenViewModel` usa l'overload
  quando ha `groupHistory`.
  `QuickActionExecuted` con `Recorded == false` → `ChildSelected` (apre la figlia, come la Home). Con `Recorded == true` niente:
  l'evento del bus ricarica (verificare che `HabitScreenViewModel` ricarichi la Panoramica su un completamento di una
  **figlia**, non solo del soggetto; se no, aggiungerlo).
- Produces: `public sealed record TogetherRow(Guid HabitNodeId, string Name, string ColorHex, int Depth, IReadOnlyList<HistoryDayCell> Cells);`
  `TogetherViewModel(IGroupHistoryQueryService history)`: `ObservableCollection<TogetherRow> Rows`;
  `Task LoadAsync(Guid groupId, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)` — una cella per giorno
  del periodo: `Step` 4 se fatta, 0 se prevista e non fatta, `InRange` falso se non prevista (prima dell'inizio, giorno
  libero del programma), `IsFuture` dopo oggi. Non calcola altro (spec §6.3). Per "Tutto" e "Anno" le celle sono molte:
  la view le disegna (Task 4), il ViewModel non le riduce.
- Produces: `PanoramicaViewModel.Map : HistoryMapViewModel` con un `CalendarMarkerProvider` dalla query: per giorno
  `TintStep` = 0 se nessuna figlia fatta, 4 se tutte le previste fatte, altrimenti `1 + floor(3 × fatte ÷ previste)` limitato a
  3; un giorno senza previste = 0. Selezionare un giorno della mappa non fa niente per un gruppo (`SelectCommand` resta, il
  gruppo non registra).
- `HabitScreenViewModel`: per un gruppo `Period.Changed` ricarica `Panoramica.Together` e `Panoramica.Map` (forma dal
  periodo come in 2a; di default **Anno** per la mappa del gruppo, spec "Mappa dell'anno" — il periodo è uno solo, quindi il
  default del periodo per un gruppo è Anno e "Insieme" segue).

- [ ] **Step 1: test rossi.** `PanoramicaRowsTests`: `Rows_are_home_rows_in_tree_order` (sottogruppo rientrato, `IsGroup`);
  `Quick_log_records_and_refreshes` (azione: `QuickActionCommand` → una registrazione oggi, dopo l'evento `TodayLabel` "1 di 1"
  e `GroupFigures` ricaricati); `Form_types_open_the_child` (contatore con campi → `ChildSelected` con l'Id, nessuna
  registrazione); `Abstinence_row_has_the_live_clock`.
  `TogetherTests`: `One_cell_per_day_of_the_period` (settimana → 7 per riga); `Not_scheduled_is_out_of_range`;
  `Future_days_are_future`; `Together_today_is_the_groups_today` (gruppo con reset 00:00, figlia con reset 05:00, alle 04:30 del
  28: l'ultima colonna non futura è il 28, e la registrazione delle 04:30 cade nella colonna del 27);
  `Group_map_steps_count_children` (2 di 4 → step 2; 4 di 4 → 4; 0 → 0); `Default_period_of_a_group_is_year`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + Presentation intera + build WPF.**
- [ ] **Step 5: commit** `feat(presentation): le figlie come righe della Home, "Insieme" e la mappa del gruppo`.

### Task 4: La schermata del gruppo

**Files:**
- Create: `src/Recordum.UI/Views/HabitScreen/Group/GroupHeadView.axaml(.cs)`, `PanoramicaColumnView.axaml(.cs)`,
  `TogetherGrid.cs` (controllo disegnato, come `HeatGrid`)
- Modify: `HabitScreenView.axaml(.cs)` (la colonna Panoramica, la testa del gruppo al posto di `HabitHeadView` per un
  gruppo), `HabitHeadView.axaml` (il ramo "gruppo" di 2a esce), `Strings*`
- Test: `tests/Recordum.UI.Tests/HabitScreen/GroupScreenTests.cs` (nuovo)

**Interfaces:**
- Consumes: `Panoramica` (`Rows`, `Together`, `Map`, `TodayLabel`, `TodayFraction`, `HasChildren`), `GroupFigures`, `Period`.
- **Testa** (`x:DataType="vm:HabitScreenViewModel"`): filo di 3 px nel colore (c'è); nome 20/600 nella variante "testo"; sotto
  "{n} abitudini · la giornata comincia alle {ora}" (`Group.Head.Subtitle`, ora effettiva del gruppo dal resolver); a destra,
  al posto del comando, `x:Name="Progress"`: "3 di 5" 20/600 nella variante "testo", `Group.Progress.Today` 12 attenuato, una
  barra di 6 px (fondo `Brush.Cell`, pieno nel colore, larghezza = `TodayFraction`); sotto una riga sottile i numeri
  (`x:Name="Figures"`, lo stesso `ItemsControl` di `HabitHeadView`: estrarlo in uno stile o in un `FiguresView` condiviso, non
  copiarlo); i pulsanti delle colonne: Panoramica (`Icon.LayoutList`, nuova in `Themes/Icons.axaml` dal tracciato Lucide
  `layout-list`, aggiunta a `IconCatalogTests`), Note, Configura.
- **Colonna Panoramica** (si costruisce a colonna aperta, come Cronologia; quinta colonna della griglia, prima di Diario):
  titolo `HabitScreen_Column_Panoramica`; sezione **Oggi** (titolo 13/600): un `ItemsControl` su `Panoramica.Rows` con
  `HomeRowTemplate.Instance` — le righe **sono** `HabitRow`/`GroupRow`; il menu della riga non c'è qui (lo aggancia
  `HomeView`): un tasto destro non fa niente; un clic sul nome → `SelectChildCommand` (Id), il log rapido →
  `QuickActionCommand`; vuoto = `HabitScreen_Panoramica_Empty`, una frase senza pulsante. Sezione **Insieme**: la barra del periodo di 2a (`PeriodBarView`, riusata,
  non copiata) e `TogetherGrid`: una riga per figlia, nome a sinistra (13, colonna di 140 px, rientro 12 px per `Depth`), celle
  quadrate da `min(14, larghezza ÷ giorni)` px con 2 px di spazio (sotto i 4 px niente spazio), fatta = colore pieno, prevista e
  non fatta = `Brush.Cell`, fuori = niente, oggi con l'anello; intestazione dei giorni solo per Settimana e Mese. Sezione
  **L'ultimo anno** (o l'etichetta del periodo): `HistoryMapView` di 2a su `Panoramica.Map`, la didascalia
  `Group.Map.Legend` "intensity = how many children that day; full = all" / "intensità = quante figlie quel giorno; pieno =
  tutte".
- Stringhe: `Group.Head.Subtitle` "{0} habits · the day starts at {1}" / "{0} abitudini · la giornata comincia alle {1}",
  `Group.Section.Today` Today / Oggi, `Group.Section.Together` Together / Insieme, `Group.Map.Legend`.

- [ ] **Step 1: test rossi** (`[AvaloniaFact]`, `TestCulture.Pin()`, servizi veri su DB di test): `Group_head_shows_progress_and_figures`
  ("1 di 2", la barra a metà della sua larghezza); `Group_head_has_no_record_command` (nessun `Button.record`);
  `Overview_rows_are_home_rows` (due `HabitRow` e un `GroupRow` nell'albero della colonna);
  `Quick_log_from_the_group_marks_the_child` (clic sul log rapido di un'azione → "2 di 2" in testa);
  `Clicking_a_form_child_opens_it` (l'evento d'apertura arriva con l'Id); `Together_has_a_row_per_child`
  (`TogetherGrid.RowCount` == 3); `Together_follows_the_period` (Settimana → 7 celle per riga, Mese → i giorni del mese);
  `Group_map_is_the_year_by_default`; `Empty_group_says_so`; `Narrow_group_head_has_no_overlap` (480 px).
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI (`Category!=Performance`).**
- [ ] **Step 5: commit** `feat(ui): la schermata del gruppo — avanzamento, figlie segnabili, Insieme, mappa dell'anno`.

### Task 5: Il riassunto di ogni voce di Configura

**Files:**
- Modify: `src/Recordum.Presentation/ViewModels/HabitEditor/SectionViewModelBase.cs` (additivo), le sezioni in
  `ViewModels/HabitEditor/Sections/`, `HabitEditorViewModel.cs` (additivo), `SamplePreviewFactory.cs` (un metodo pubblico),
  `Strings*`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitEditor/SectionSummaryTests.cs` (nuovo)

**Interfaces:**
- Produces: su `SectionViewModelBase` `public virtual string Summary => string.Empty;` e `protected void NotifySummary()` (alza
  `PropertyChanged(nameof(Summary))`); ogni sezione la chiama dopo `LoadAsync` e dopo ogni cambio che la tocca. Su
  `SectionViewModelBase` anche `public virtual bool HasAdvanced => false;` (vero per Streak, Goal, Timer).
- Riassunti (cultura corrente; "—" mai: una voce senza valore dice cosa manca):

| Sezione | Riassunto |
|---|---|
| Scheduling | `ScheduleText.Describe` (2a); vuoto → `Editor.Summary.Custom` Custom / Personalizzato |
| Streak | l'etichetta della modalità; con recupero, " · " + l'etichetta del recupero |
| Goal | "{nome} · {target} {unità} {periodo}"; senza → `Editor.Summary.NoGoal` No goal / Nessun obiettivo |
| ResetTime | "5:00" se propria; ereditata → `Editor.Summary.Inherited` "{0} (inherited)" / "{0} (ereditato)" |
| Metrics | i nomi dei campi separati da ", "; nessuno → `Editor.Summary.NoFields` No fields / Nessun campo |
| Scales | i nomi delle scale |
| Registration | "più volte al giorno" / "una volta al giorno" (chiavi esistenti se ci sono), e il gesto per un diario |
| Timer | spento → `Editor.Summary.Off` Off / Spento; acceso → modalità + obiettivo (`SecondsDisplay`) |
| HomeRow | l'etichetta del valore accanto al nome · quella della striscia |
| Notes | vuoto (la riga mostra il pallino del colore) |
| Tags | i nomi dei tag; nessuno → `Editor.Summary.NoTags` No tags / Nessun tag |
| AiSharing | `Editor.Summary.ComingSoon` Coming soon / In arrivo |

- Produces: `public static HomeRowPreviewModel SamplePreviewFactory.HomeRowFor(HabitType type, KeyFigureKind keyFigure, HomeStrip strip, string name, string color, string? icon)`
  — la stessa logica di campione di `BuildHomeRow`, con valore e striscia **scelti** invece di quelli di default;
  `BuildHomeRow` la chiama (un solo posto). `HomeRowSectionViewModel.Preview : HomeRowPreviewModel?`, aggiornato su
  `SelectedKeyFigure`, `SelectedStrip` e sul nome e il colore dell'editor (`HabitEditorViewModel` glieli passa).

- [ ] **Step 1: test rossi** (`it-IT`): un `[Theory]` per ogni riga della tabella (sezione caricata con un dato e senza);
  `Summary_follows_a_change` (cambiare `ScheduleType` → `PropertyChanged(Summary)` e il testo nuovo);
  `Reset_time_says_inherited`; `Home_row_preview_follows_the_choice` (striscia 14 → `Fractions.Length` 14; nome cambiato
  nell'editor → `Preview.Name`); `Builder_preview_is_unchanged` (`SamplePreviewFactory.Build` dà la stessa `HomeRow` di prima
  per i cinque tipi: fissarla nel test **prima** del refactor).
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + Presentation intera + build WPF.**
- [ ] **Step 5: commit** `feat(presentation): un riassunto per ogni voce di Configura e l'anteprima della riga`.

### Task 6: Configura in cinque blocchi

**Files:**
- Create: `src/Recordum.UI/Views/HabitEditor/ConfigRow.axaml(.cs)`, `src/Recordum.UI/Views/HabitEditor/SectionContent.cs`,
  `src/Recordum.UI/Views/HabitEditor/Sections/StreakAdvancedView.axaml(.cs)`, `GoalAdvancedView`, `TimerAdvancedView`,
  `src/Recordum.UI/Controls/RowPreview.axaml(.cs)`, `src/Recordum.UI/Views/HabitEditor/GroupContentView.axaml(.cs)`
- Modify: `EditorPane.axaml(.cs)`, `StreakSectionView.axaml`, `GoalSectionView.axaml`, `TimerSectionView.axaml` (la parte
  "Avanzate" esce, va nelle view `*Advanced`), `BuilderPreviewPane.axaml` (usa `RowPreview`), `Strings*`
- Delete: `src/Recordum.UI/Views/HabitEditor/SectionTemplate.cs` (lo sostituisce `SectionContent`; verificare che nessun altro lo usi)
- Test: `tests/Recordum.UI.Tests/HabitEditor/ConfiguraBlocksTests.cs` (nuovo), i test di Configura esistenti aggiornati

**Interfaces:**
- Consumes: `Summary`, `HasAdvanced`, `HomeRow.Preview` (Task 5); le proprietà di sezione di `HabitEditorViewModel`.
- `ConfigRow : UserControl` con `StyledProperty<SectionViewModelBase?> Section` e `StyledProperty<SectionPart> Part`
  (`enum SectionPart { Main, Advanced }`): una riga 40 px "titolo 13/500 · riassunto 13 attenuato a destra · chevron Lucide";
  un clic (o Invio/Spazio) apre il contenuto **sotto, sul posto**, costruito alla prima apertura da
  `SectionContent.Build(section, part)` (la mappa tipo → view di `SectionTemplate`, più le tre `*Advanced`) e tenuto fino alla
  chiusura della colonna; visibile con `Section.IsVisible` (e, per `Advanced`, `HasAdvanced`). Lo stato aperto è
  `Section.IsExpanded` (Main) — per Advanced uno stato locale della riga.
- `EditorPane` (habit), dall'alto, con i titoli di blocco 16/600 (`Editor.Block.*`):
  1. **Chi è** (`Editor.Block.Identity` Who it is / Chi è): nome 20/600 nella cornice dei campi, descrizione, una riga con
     tipo (etichetta, come oggi), colore (Task 7) e icona; poi `ConfigRow` di Notes (colore del segno delle note) e Tags.
  2. **Cosa registri** (`Editor.Block.Record` What you record / Cosa registri): Metrics, Scales, Registration, Timer.
  3. **Quando conta** (`Editor.Block.When` When it counts / Quando conta): Scheduling, Streak, Goal, ResetTime — con l'avviso
     che c'è oggi nella sezione (cambiarlo rilegge lo storico) **visibile nella riga chiusa** accanto al riassunto
     (`Icon.AlertTriangle` 13 col tooltip dell'avviso).
  4. **Come appare in Home** (`Editor.Block.Home` How it looks in Home / Come appare in Home): `RowPreview` su
     `HomeRow.Preview`, poi la riga HomeRow.
  5. **Avanzate** (`Editor.Block.Advanced` Advanced / Avanzate), **chiuso** di default (un `ConfigRow`-titolo che apre il
     blocco): Streak/Goal/Timer in `Part=Advanced`, AiSharing.
  Lo stato in fondo (autosalvataggio) resta fuori dallo scorrimento.
- **Ogni casella con una riga che spiega** (spec §6.4.2): ogni `CheckBox`/`ToggleSwitch` delle view di Cosa registri e Quando
  conta ha sotto una riga 12 attenuata; le chiavi che mancano si aggiungono come `Editor.Help.<Sezione>.<Campo>` (en/it),
  scritte nel report.
- `EditorPane` per un **gruppo** (`SelectedNodeType == Group`): 1. Chi è (nome, descrizione, colore, icona); 2. **Per le
  figlie** (`Editor.Block.ForChildren` For its habits / Per le figlie): ResetTime e Goal; 3. **Contenuto**
  (`Editor.Block.Content` Contents / Contenuto): `GroupContentView`, l'elenco delle figlie dirette e dei sottogruppi da
  `Panoramica.Rows` (Depth 0, nome e pallino; un clic apre la figlia con `SelectChildCommand`). Il pane ha il DataContext
  dell'editor, quindi `EditorPane` riceve `StyledProperty<PanoramicaViewModel?> Group`, che `HabitScreenView.UpdateColumns`
  imposta a `vm.Panoramica` per un gruppo quando costruisce il pane (riga `ConfiguraHost.Child = new EditorPane …`); con la
  riga `Editor.Group.ReorderHint` "Reorder and move habits from Home." / "Riordina e sposta le abitudini dalla Home.";
  4. **Salva come modello**: una riga spenta con `Editor.Summary.ComingSoon` (decisione da controllare, sotto). Le sezioni
  che oggi si vedono per un gruppo e non stanno in questi blocchi: elencarle nel report e metterle in Avanzate, mai toglierle.
- `RowPreview` (`Controls/`): la riga della Home di `BuilderPreviewPane` (barra, icona, nome, valore, `HeatmapStrip`) estratta
  in un controllo con `StyledProperty<HomeRowPreviewModel?> Model` e `StyledProperty<ThemeVariant?> Theme` (null = quello
  corrente; lo usa il Task 7 per mostrarla in chiaro e in scuro con `ThemeVariantScope`), più la tinta della riga
  (`Opacity.RowTint`) che il builder oggi non ha. Il builder la usa: stesso aspetto, tinta in più.
- Stringhe: le `Editor.Block.*`, `Editor.Group.ReorderHint`, e quelle del Task 5.

- [ ] **Step 1: test rossi** (`[AvaloniaFact]`, `TestCulture.Pin()`): `Five_blocks_in_order` (i cinque titoli, in quell'ordine);
  `Rows_show_title_and_summary` ("Programma" · "Ogni giorno"); `A_row_opens_in_place` (clic → il contenuto è il figlio
  successivo della riga, nessun'altra riga si apre); `Content_is_built_on_first_open` (prima del clic nessuna
  `SchedulingSectionView` nell'albero); `Advanced_is_closed_and_holds_the_advanced_parts` (aperto: `StreakAdvancedView`,
  `GoalAdvancedView`, `TimerAdvancedView`, `AiSharingSectionView`; in `StreakSectionView` non c'è più la tolleranza);
  `An_advanced_field_still_saves` (tolleranza della serie a 30 → `HabitConfig.StreakToleranceMinutes` 30 nel DB);
  `Every_field_of_today_is_reachable` (con **tutte** le righe aperte, ogni controllo con `x:Name` delle dodici view di sezione
  di prima del task è nell'albero: elenco dei nomi preso **prima** del refactor e scritto nel test);
  `Reset_warning_shows_on_the_closed_row`; `Home_preview_is_a_tinted_row`; `Every_checkbox_has_a_help_line` (con le righe
  aperte, ogni `CheckBox`/`ToggleSwitch` dei blocchi 2 e 3 ha un `TextBlock` di spiegazione subito sotto); `Group_configura_has_its_blocks` (Chi è, Per le
  figlie, Contenuto con le figlie, Salva come modello spenta); `Builder_preview_still_shows_the_row` (il test del builder
  esistente resta verde: dirlo nel report se è cambiato).
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): Configura in cinque blocchi con le righe-riassunto; la Configura del gruppo`.

### Task 7: La scelta del colore

**Files:**
- Modify: `src/Recordum.UI/Controls/ColorSwatchPicker.axaml(.cs)`, `tests/Recordum.UI.Tests/Styles/HabitColorsTests.cs` (la
  palette letta da `ColorSwatchPicker.Palette`, una sola fonte), `Strings*`
- Test: `tests/Recordum.UI.Tests/Controls/ColorPickerTests.cs` (aggiornato ed esteso)

**Interfaces:**
- Consumes: `RowPreview` (Task 6).
- `ColorSwatchPicker.Palette` = i 16 colori della spec §7.1, in quell'ordine: `#C2405B` `#D0603A` `#C98A1E` `#9A9A2A`
  `#5F9A3C` `#2F9C8C` `#2F8FB0` `#2F7FC1` `#4F63C4` `#7A5BC4` `#A352A8` `#C2508A` `#8A6A4F` `#6B7280` `#4B5563` `#9A8F80`.
- Le proprietà di oggi restano (`Color`, `RecentColors`, `FavoriteColors`, `AddFavoriteCommand`, `RemoveFavoriteCommand`): i tre
  chiamanti (Configura, colore del segno delle note, builder) non cambiano. Nuova: `StyledProperty<string?> PreviewName`
  (il nome dell'abitudine per l'anteprima; null = niente anteprima, come per il segno delle note).
- Il riquadro (larghezza 8 × 28 + 7 × 8 + i margini), dall'alto: i 16 cerchi da 28 px, 8 per riga, quello scelto con l'anello
  (2 px fondo + 2 px `Brush.Ink`); **Recenti** (cerchi da 24, nascosto se vuoto); **Come appare** (solo con `PreviewName`):
  due `RowPreview` affiancate, chiaro e scuro, col nome e un valore di campione; una riga **Avanzate ›**
  (`ColorPicker.Advanced`) che apre sotto, nello stesso riquadro: selettore libero (`HsvColorPicker`, c'è), **Sfumature**,
  **Preferiti** con "+ aggiungi", **esadecimale** (la casella di oggi). Avanzate resta chiusa all'apertura, tranne quando il
  colore corrente non è né nella palette né nei recenti (così chi ha un colore libero lo vede subito).
- Il fuoco all'apertura va sul cerchio scelto (o sul primo), non più sull'esadecimale. Un esadecimale non valido non cambia
  `Color` e la casella mostra la cornice d'errore (`Brush.Danger`) finché non lo è.
- Stringhe: `ColorPicker.Advanced` Advanced / Avanzate, `ColorPicker.Preview` How it looks / Come appare. `ColorPicker_Recent`,
  `ColorPicker_Favorites`, `ColorPicker_Shades`, `ColorPicker_Hex`, `ColorPicker_AddFavorite` ci sono.

- [ ] **Step 1: test rossi** (`[AvaloniaFact]`): `Palette_is_the_specs_sixteen`; `Sixteen_circles_in_two_rows_of_eight`;
  `Choosing_a_circle_sets_the_colour`; `Advanced_is_closed_by_default` e `Advanced_opens_for_a_free_colour` (`#123456`);
  `Preview_shows_light_and_dark_rows` (con `PreviewName`: due `RowPreview`, una sotto un `ThemeVariantScope` Dark);
  `No_preview_without_a_name`; `Favorites_live_in_advanced` (aggiungi → il preferito compare in Avanzate);
  `Invalid_hex_keeps_the_colour` (`#12345` → `Color` invariato, cornice d'errore; `#12345A` → colore nuovo);
  `Focus_starts_on_the_selected_circle`. `HabitColorsTests` resta verde leggendo la palette dal controllo.
- [ ] **Step 2: rosso visto.** **Step 3: implementare** (`PreviewName` legato al nome in Configura e nel builder).
  **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): la scelta del colore — sedici pronti, recenti, l'anteprima, il resto in Avanzate`.

### Task 8: I minori delle onde 1 e 2a sulle stesse viste

**Files:** quelli di ciascuna voce; test accanto ai test della vista.

Ognuna con un test rosso prima, dove si può misurare (CLAUDE.md §7); quelle che non si possono provare in headless lo dicono
nel report.

- [ ] **Step 1: la riga** (Home e figlie del gruppo): variante "testo" sulla riga **selezionata** a ≥ 4,5:1 sul suo fondo
  (oggi ~4,1:1: calcolarla sul fondo della selezione, non sulla tinta; test di contrasto come `HabitColorsTests`); `HeatmapStrip`
  costruita **una volta** per riga (oggi fino a 4: contare le ricostruzioni con un contatore interno di test e ridurle
  rimandando la ricostruzione a fine `ApplyTemplate`/`Arrange`).
- [ ] **Step 1-bis: il giorno dell'abitudine indietro rispetto a quello dell'app** (decisione dell'autore del 2026-10-30,
  opzione C): le strisce restano nel giorno dell'abitudine; nella riga (Home e figlie del gruppo) in cui il giorno
  dell'abitudine è **prima** del giorno dell'app compare, subito a sinistra della striscia, `Home.Row.ResetPending` "reset {0}"
  ("reset 5:00", 12, `Brush.TextFaint`, ora nel formato breve della cultura) col tooltip `Home.Row.ResetPending.Tip` "For
  this habit the day ends at {0}: the last box is still yesterday." / "Per questa abitudine la giornata finisce alle {0}:
  l'ultima casella è ancora ieri."; sparisce al reset dell'abitudine, col ticker che la Home ha già (`HomeClockTicker`), senza
  ricaricare. Su `HomeRowViewModel` (additivo): `bool IsDayBehindApp`, `string? ResetPendingText`; il reset effettivo
  dell'abitudine viene da `HomeTreeRow` se lo porta già (le strisce lo usano: verificarlo), altrimenti si aggiunge
  `TimeOnly EffectiveResetTime` a `HomeTreeRow` (additivo, default dell'app). Test: `Row_shows_reset_while_its_day_lags`
  (reset dell'app 00:00, dell'abitudine 05:00: alle 02:00 "reset 5:00" visibile; alle 06:00 nascosto, dopo un tick);
  `Same_reset_never_shows_it`.
- [ ] **Step 1-ter: UX2A-1 per l'abitudine sola**: `HabitScreenViewModel` prende `IHabitStartQueryService?` (parametro
  opzionale in fondo; null = la creazione, com'è per WPF) e passa l'inizio a `HistoryPeriodViewModel.SetContext` e a
  `HabitFiguresViewModel` al posto del giorno di creazione. Test, **rossi prima**: `All_includes_records_before_creation`
  (creata il 3/1/2026, registrata il 28/12/2025 → "Tutto" ha due anni, il 28/12 ha `Step` > 0);
  `Total_counts_records_before_creation`; `Pct30_window_starts_at_the_first_record`.
- [ ] **Step 2: la cornice**: icona di "fatte" accesa con un contrasto da controllo attivo (≥ 3:1); il tempo del timer 15/600
  nella variante "testo" (oggi 13/600); il marchio in serif nell'errore fatale; l'avviso sulla passphrase **una** volta al
  primo avvio.
- [ ] **Step 3: la testa di 2a**: UX2A-5 (il conteggio del giorno scelto in testa anche senza obiettivo, "−" con un
  `Icon.Minus` Lucide nuovo in `Icons.axaml` e in `IconCatalogTests`); UX2A-6 (l'astinenza in testa mostra `ErrorMessage` e
  "ricaduta il …" da `RelapsedOnDisplay`); UX2A-8 (`OnHabitUpdated` ricarica anche `PeriodRecords`); UX2A-9 (`ActionCommandView`:
  lo stato del `ToggleButton` torna a `IsDone` anche quando il comando non cambia nulla — test con la guardia `IsSaving`).
- [ ] **Step 4: verdi + suite UI e Presentation.** **Step 5: commit** `fix(ui): i minori delle onde 1 e 2a su righe, cornice e testa`.

### Task 9: Schermate, inventario, prestazioni, documenti

**Files:**
- Modify: test di screenshot, `docs/phase-4-issues.md`, `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1: PNG in chiaro e in scuro**, ognuno con un'asserzione strutturale: il gruppo con tre figlie (una fatta, un
  sottogruppo, un'astinenza) con Panoramica aperta, "Insieme" in Settimana, Mese e Anno; il gruppo vuoto; il gruppo a 480 px;
  Configura con i cinque blocchi chiusi, con una riga aperta, con Avanzate aperta; Configura del gruppo; il selettore del
  colore chiuso e con Avanzate; il builder con la riga d'anteprima nuova. **Guardarli prima di chiudere il task**; ciò che si
  vede storto diventa un test.
- [ ] **Step 2: prestazioni.** In locale, a macchina ferma, alternando due volte prima (primo commit del branch) e dopo:
  `Category=Performance` di UI e Data; più un test nuovo `[Trait("Category","Performance")]` che apre un gruppo di 12 figlie
  con tre anni di registrazioni giornaliere e misura `LoadAsync` + "Insieme" in Anno + la mappa (riferimento: la schermata
  dell'abitudine con tre anni, ~0,35 s per "Tutto"). Numeri nel report.
- [ ] **Step 3: inventario.** Le voci "Configura" dell'Appendice A, una per una, trovate nel nuovo albero (nome, descrizione,
  colore con preferiti, icona; riga in Home; metriche; scale; registrazione; programma; serie con tolleranza; obiettivo con
  giorni mobili; timer con riposo, segmenti, richiesta automatica; orario di reset con avviso; colore del segno delle note;
  condivisione AI; i tag); il gruppo: la Panoramica di WPF (oggi, figlie, obiettivo) tutta presente. Esito nel report.
- [ ] **Step 4: documenti.** Tracker: una sezione "Onda 2b" coi finding nuovi; UX2A-5, 6, 8, 9 `fixed` col test che lo prova;
  ROADMAP "Dove siamo" (2b fatta; prossimo: il piano dell'onda 3) e MILESTONES. Commit e push.
- [ ] **Step 5: review dell'intero branch** (modello più capace) con la spec in mano; un'onda di correzioni; **CI verde** su
  Linux e Windows; push. Il merge in `main` lo fa l'autore, dopo aver guardato i PNG.

## Decisioni mie (da controllare leggendo)

1. **Astinenza fatta = giorno pulito**, anche nel "x di y" della Home (oggi un'astinenza in un gruppo non è mai fatta: il
   gruppo non arriva mai a "5 di 5"). Cambia un comportamento condiviso con WPF.
2. **"Salva come modello" del gruppo è "in arrivo"**: i modelli oggi sono di una sola abitudine (il payload non ha figlie);
   un modello di gruppo è un formato nuovo e, per il principio 8, chiede anche il gruppo nel creatore. Si fa a parte.
3. **Contenuto** del gruppo elenca e apre le figlie; riordinare e spostare resta nella Home (trascinamento), con una riga che
   lo dice.
4. Il periodo di default di un gruppo è **Anno** (la "mappa dell'anno" della spec); "Insieme" segue lo stesso periodo.
5. In "Insieme" una giornata non prevista è vuota (fuori), una prevista e non fatta è la cella neutra: si vede il programma.
6. Gli **avanzati** di Serie, Obiettivo e Timer escono dalle loro righe e vanno nel blocco Avanzate (spec §6.4.5); la
   condivisione con l'AI anche.
7. Il colore del segno delle note e i tag stanno in **Chi è** (la spec mette il primo lì; i tag non li nomina).
8. I **Preferiti** restano (spec §7.1), dentro Avanzate; il mockup B li toglieva.

## Decise dall'autore il 2026-10-30

- **UX2A-1**: l'inizio è il più vecchio fra creazione e prima registrazione (Global Constraints; Task 1 e Task 8, Step 1-ter).
- Giorno dell'abitudine indietro rispetto all'intestazione della Home: "reset 5:00" nella riga (Task 8, Step 1-bis).

## Aperta

- **L'astinenza in un gruppo** (decisione mia 1): da discutere con l'autore prima del Task 1.

## Fuori da 2b

- Il creatore nei cinque blocchi, il selettore dei modelli con la traduzione, le Impostazioni a categorie, le finestre del
  backup, il diario a tutta finestra: onda 3.
- "Cosa dice il gruppo" (domande, Esplora): spec a parte, dopo la direzione visiva.
- `YearHeatmapControl` resta non montato (F7); la mappa del gruppo è `HistoryMapView` di 2a.
- UX2A-2 (Markdown nel riepilogo delle note), UX2A-3 (test instabile), UX2A-4 (percentuale per "N volte a settimana"),
  UX2A-7 (accessibilità di `HeatGrid`): restano nel tracker.
