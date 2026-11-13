# Onda "Giornata" — la Home ha un giorno, le parole decise, la schermata che non si ripete

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** il giro "apro, segno, chiudo" e il recupero della sera costano poco: la Home ha un **giorno corrente** che si
sceglie in testa (ieri costa quanto oggi), la testa sta su una riga col conto della giornata, le righe hanno **tre
versioni a scelta** in Impostazioni › Aspetto, la tastiera registra, le parole sono quelle decise dall'autore con una
guardia che le tiene, e la schermata dell'abitudine smette di ripetere la stessa voce (B1–B6).

**Architecture:** il giorno della Home è **stato di `HomeViewModel`** (`DaysBack`, 0 = oggi), dentro la finestra di 14
giorni che `GetHomeTreeAsync` carica già: nessuna query nuova, nessuna migrazione. Le righe si ricostruiscono dal
`_lastFetched` col giorno come parametro (è ciò che fa già ogni evento di registrazione, senza la lettura dal database).
Il log rapido riceve il giorno (`IQuickActionHandler.ExecuteAsync(id, day, ct)`) e timbra con **un solo** helper condiviso
col quick-add della schermata (oggi = adesso; un giorno passato = `NoonOf` del confine dell'abitudine). Le tre versioni
delle righe sono una preferenza (`HomeRowsPreference`, chiave `home.rows` in `AppSetting`, senza seed) che la vista traduce
in misure con una tabella sola (`HomeRowsMetrics`) ereditata lungo l'albero visuale: la stessa riga si disegna nelle tre
versioni anche nell'anteprima delle Impostazioni.

**Tech Stack:** .NET 10, Avalonia 12.1.2, CommunityToolkit.Mvvm, xunit v3 + Avalonia.Headless (UI), xunit (Presentation,
Data).

**Spec:** `docs/superpowers/specs/2026-11-10-v1-ux-review-and-path-design.md` — §2 A1–A4, B1–B6, C; **§6 punti 2, 3, 4 e
"righe della Home a scelta"** (le decisioni dell'autore: dove il §2 e il §6 divergono vince il §6). Mockup che si provano:
`docs/design/2026-11-10-giornata-mockups/home-giorno.html` (giorno e testa), `docs/design/2026-11-10-telefono-mockups/oggi-striscia.html`
(le tre righe e la pagina Aspetto). Direzione visiva: `docs/superpowers/specs/2026-10-28-ux-direction-design.md` (§4.4, §5).
Modello di forma: il piano 3a.

## Global Constraints

- **Branch**: `giornata`, da `main` **dopo** il merge di 3a (lo esegue l'autore). Cambiare branch in una chiamata separata
  (hook `guard-main.js`). Mai `git stash` (c'è uno stash dell'autore).
- **Livelli** (ROADMAP "Chi esegue"): ogni task dichiara il suo. Review di ogni task in un agente **diverso** da chi ha
  scritto (Sonnet). **Fermati e sali di livello** dopo due tentativi falliti sullo stesso test, o se il task chiede di
  toccare un file fuori dal suo elenco: si scrive nel report, non si improvvisa.
- **Il giorno**: si chiede a `DayBoundary` (da `IResetTimeResolver`), **una volta** in cima all'operazione; una retroattiva
  si timbra a `NoonOf(giorno)` con `IsRetroactive = true`. Mai `DateOnly.FromDateTime(clock.UtcNow…)`, mai `DateTime.UtcNow`
  (`DayDerivationGuardTests`, `DayBoundaryParityTests` restano verdi). Il giorno della Home è quello dell'**app**; il timbro
  di una registrazione usa il confine dell'**abitudine**.
- **Nessuna migrazione, schema invariato.** `home.rows` è una chiave `AppSetting` senza seed (come `theme.mode`): si scrive
  al primo cambio.
- **Parole**: `docs/GLOSSARY.md` (Task 1) vale per ogni stringa nuova di quest'onda. Le stringhe nuove qui sotto sono già
  conformi: non si riscrivono.
- Ogni stringa in `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs` (**solo via shell**). Test con
  etichette: `TestCulture.Pin()`; chi controlla una traduzione fissa **entrambe** le culture.
- Avalonia: `InitializeComponent()`, `x:DataType` ovunque, niente `ContentControl` con `ContentTemplate` statico,
  `[AvaloniaFact]`, dopo un'azione `RunJobs()` + `ForceRenderTimerTick()`; un PNG vuole un'asserzione strutturale. Scala
  12/13/14/16/20, `Brush.*`, icone Lucide, nessun glifo nuovo come testo. Colore dell'abitudine solo per il comando che
  registra; inchiostro (`Button.primary`) per **una** azione per finestra.
- `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`: la guardia sta anche dentro il metodo.
- WPF è congelata: deve **compilare**, non riceve funzioni. I cambi di firma condivisi (Task 4 e 5) li elenca il
  compilatore; in WPF si fa il minimo.
- Build con `--no-incremental`; mai `-p:UseAppHost=false`; `TreatWarningsAsErrors`. **Un test di regressione si prova
  rosso** contro il codice di prima. Filtri `dotnet test` per nome di classe, controllando il totale eseguito.
- Aggiornato un `.md` → commit e push subito. Dopo ogni push che tocca codice si guarda la CI.

## Review Focus

1. **Il giorno cambia con la Home aperta su ieri** (app lasciata aperta la notte): la Home torna su oggi da sola, e un log
   rapido premuto subito dopo va sul giorno nuovo, non su "ieri di ieri". Test nel Task 6.
2. **Un'abitudine col suo inizio di giornata** (reset 05:00, app 00:00) registrata dalla Home su un giorno passato: la
   registrazione cade in **quel** giorno dell'abitudine (mezzogiorno del suo confine) e la cella marcata è quella che si
   riempie. Test nel Task 4.
3. **Ricaduta su un giorno passato ai bordi**: prima dell'inizio del periodo aperto (errore mostrato, niente scritto), lo
   stesso giorno dell'inizio ma con mezzogiorno prima dell'inizio (la fine cade a fine giornata, mai prima dell'inizio),
   nessun periodo aperto (nessun effetto). Test nel Task 5.
4. **La finestra si stringe mentre il giorno scelto esce dalle colonne visibili**: il giorno scelto diventa il più vecchio
   visibile, titolo e fascia lo seguono; non si resta a scrivere su un giorno che non si vede. Test nei Task 6 e 7.
5. **`home.rows` con un valore che non esiste** (file di un'altra versione, scritto a mano): vale la predefinita, nessuna
   eccezione; e un cambio dalle Impostazioni con la Home aperta si vede subito, senza riavvio. Test nel Task 9.
6. **Un diario che ha già entità `Note`**: tiene la colonna Note, che le mostra e **non** ne crea di nuove; un diario senza
   non ha la colonna, neanche dopo un ripristino che gliele porta (riappare). Test nel Task 13.

---

## Parte 1 — Le parole

### Task 1: Glossario, guardia, parole decise

**Livello:** Sonnet. **Fermati e sali se** una parola vietata è costruita in codice (non in `.resx`) e toglierla cambia
una firma.

**Files:**
- Create: `docs/GLOSSARY.md`; `tests/Recordum.Presentation.Tests/Resources/GlossaryGuardTests.cs`
- Modify: `Strings.resx`, `Strings.it.resx` (solo valori; nessuna chiave rinominata)

- [ ] **Step 1: la guardia, rossa.** `GlossaryGuardTests` legge le due `.resx` come XML (stesso modo di
  `OfficialTextsTests`) e fallisce elencando `chiave: valore` per ogni violazione:
  - `Italian_values_avoid_forbidden_words`: nessun valore italiano contiene (senza distinguere maiuscole) `streak`,
    `resistenza`, `resistere`, `journal`, `pulit`, `ho ceduto`, `gesto primario`; nessun valore di una chiave che contiene
    `Streak` contiene `serie`; nessun valore di una chiave che contiene `Abstinence` o `Resistance` contiene `tentativ`.
  - `English_values_avoid_forbidden_words`: nessun valore inglese di una chiave `Abstinence`/`Resistance`/`HabitType`
    contiene `resistance`, `clean`, `gave in`, `attempt`.
  - `Every_key_exists_in_both_languages` (se non c'è già un test equivalente: cercarlo prima).
  Rossa oggi su almeno: `Home.HabitType.Resistance`, `Home.HabitType.Journal`, `Detail.Streak.Current/Longest/Empty`,
  `Editor.Section.Streak`, `Editor.Field.StreakMode`, `HabitBuilder.Schedule.Header/StreakMode`,
  `HabitBuilder.AbstinenceBlock.Tip`, `Abstinence.CleanFor`, `Abstinence.CleanDays`, `Today.Resistance.Attempts`,
  `Editor.Registration.JournalGesture`, `Head.Abstinence.Lapse`.
- [ ] **Step 2: i valori**, con le regole del §6.4 (non si inventa oltre):
  streak → "di fila" ("{0} giorni di fila", "Record: {0}", riga di Configura "Giorni di fila"; inglese resta "streak") ·
  tipo → "astinenza" / "abstinence" · tipo diario → "diario" · `Head.Abstinence.Lapse` → "Segna una ricaduta…" / "Log a
  relapse…" · "Libero da" / "Free for", "{0} giorni liberi" / "{0} free days" · `Today.Resistance.Attempts` → "{0} ricadute
  in 90 g" / "{0} relapses in 90 d" · `Editor.Registration.JournalGesture` → "Si registra con" / "Recorded with" · più
  registrazioni nello stesso giorno → "registrazioni" (mai "nomi delle serie") · "serie" resta solo all'unità `sets`.
- [ ] **Step 3: `docs/GLOSSARY.md`**, una trentina di righe: `inglese | italiano | cosa vuol dire | non si scrive`. Dentro
  i termini del §6.4 e quelli che le `.resx` usano per: i cinque tipi, gruppo, voce, nota, registrazione, giorno,
  inizio della giornata, obiettivo, programma, modello, colonna, Cronologia, Dettaglio, Configura, archivio, cestino,
  backup, ripristino, passphrase. In testa: "la guardia è `GlossaryGuardTests`: una parola vietata nuova si aggiunge lì".
  I termini **non** decisi dall'autore si marcano "proposta" e si elencano nel report.
- [ ] **Step 4:** guardia verde; suite Presentation e UI verdi (i test che confrontano le etichette cambiate si
  aggiornano); commit `feat(strings): le parole decise, glossario e guardia sulle .resx`.

### Task 2: Le parole che chiedono codice

**Livello:** Sonnet.

**Files:** dove vivono oggi (cercarli dalle chiavi): il formato "valore + unità" (UX3A-1/W2V-12, tracker riga ~2446), i
titoli delle tessere del creatore (`HabitBuilder` — UX3A-Q2), `NoteRowViewModel.CategoryLabel`, `NotesColumnView.axaml`.

- [ ] **Step 1: test rossi** (Presentation, culture fissate `en-US` e `it-IT`):
  - `Unit_next_to_a_value_uses_the_symbol_when_there_is_one` ("2 h", "20 min") e
    `..._otherwise_the_translated_name` ("12 pagine" / "12 pages", da `OfficialTexts.UnitName`); mai "2 hours".
  - `Builder_tiles_have_no_plus_in_the_title` ("Quantità", non "+ Quantità"; il "+" è l'icona `Icon.Plus` della tessera).
  - `A_note_row_has_no_category_label` (via "Nota veloce" dalle note: `CategoryLabel` sparisce dalla riga).
  - UI: il pulsante dentro Note che apre le versioni dice "Versioni" / "Versions" (`Notes.History`).
- [ ] **Step 2: implementa** in un punto solo per regola (un formatter per valore + unità, usato da tutti i chiamanti che
  il compilatore elenca).
- [ ] **Step 3:** verdi, commit `fix(presentation): unita' col simbolo, tessere senza "+", note senza etichetta di categoria`.

---

## Parte 2 — La Home

### Task 3: La fixture "manifesto" e i PNG di partenza

**Livello:** Haiku (fixture e PNG). **Fermati e sali se** serve toccare codice fuori da `tests/`.

**Files:**
- Create: `tests/Recordum.UI.Tests/Fixtures/ManifestHome.cs`; `tests/Recordum.UI.Tests/GiornataScreenshotTests.cs`

**Interfaces — Produces:** `static IReadOnlyList<HomeTreeRow> ManifestHome.Rows(DateOnly today)` — le nove abitudini della
tabella di `docs/DOGFOODING.md` (un gruppo "Routine del mattino" con tre azioni, più le altre, come nel mockup), 14 giorni
di dati, **oggi a metà** (4 fatte su 9), **ieri lasciato a metà**, colori **diversi** della palette (mai lo stesso cremisi),
`ValuesLast14Days` per contatori e stato, un'astinenza in corso da 23 giorni, `HabitDay`/`AppDay` valorizzati.
`ManifestHome.Home(...)` costruisce un `HomeViewModel` con `FakeHabitTreeQueryService` e l'orologio fisso sul
2026-11-10 12:00 locale.

- [ ] **Step 1:** fixture + `Home_full(theme, width)` per `light`/`dark` × 900/360: PNG `home-manifesto-{theme}-{width}.png`
  con asserzioni strutturali (9 righe di abitudine + 1 di gruppo; l'intestazione dei giorni visibile; la colonna di oggi
  dell'intestazione sopra l'ultima cella della prima striscia, ±1 px).
- [ ] **Step 2:** guardare i quattro PNG e scrivere nel report cosa si vede (è la prima Home piena mai resa: finding A4).
  Commit `test(ui): fixture manifesto e PNG della Home piena`.

### Task 4: Il timbro di una registrazione, in un punto solo; il log rapido riceve il giorno

**Livello:** Sonnet (tocca `DayBoundary` e una firma condivisa: **mai** Haiku).

**Files:**
- Create: `src/Recordum.Presentation/Services/CompletionStamp.cs`
- Modify: `ViewModels/Quick/IQuickActionHandler.cs` e i quattro handler; `ViewModels/HabitScreen/QuickAdd/QuickAddViewModelBase.cs`;
  `HomeRowViewModel.cs` (solo la chiamata)
- Test: `tests/Recordum.Presentation.Tests/ViewModels/Quick/` (esistenti + nuovi)

**Interfaces — Produces:**
- `static (DateTime Timestamp, long Ms, bool IsRetroactive) CompletionStamp.For(DayBoundary boundary, IClock clock, DateOnly day)`
  — `day == boundary.Today(clock)` → adesso, non retroattiva; altrimenti `boundary.NoonOf(day)`, retroattiva.
  `QuickAddViewModelBase.ResolveCompletionTimestamp` diventa una riga che lo chiama (comportamento identico).
- `ValueTask<bool> IQuickActionHandler.ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)` —
  `day == null` = oggi (i chiamanti di prima). Azione e contatore senza campi: confine da
  `IResetTimeResolver.GetDayBoundaryAsync(habitNodeId)`, timbro da `CompletionStamp.For`. Contatore con campi, stato,
  diario: `false` (si apre la schermata) come oggi. Astinenza: Task 5.

- [ ] **Step 1: test rossi:**
  - `Stamp_today_is_now_and_not_retroactive`, `Stamp_past_day_is_noon_of_that_day_and_retroactive` (confine 05:00,
    fuso Europe/Rome, un giorno col cambio d'ora).
  - `Action_on_a_past_day_records_at_noon_of_the_habit_boundary` (**Review Focus 2**: reset dell'abitudine 05:00, app
    00:00, `day` = ieri → `CompletedAt` = mezzogiorno di ieri del confine dell'abitudine, `IsRetroactive`).
  - `Counter_without_fields_on_a_past_day_records_one`; `Counter_with_fields_on_a_past_day_opens_the_screen` (false).
  - `Null_day_behaves_as_before` (il "parametro muto": un caso in cui `day` passato e `null` danno timbri **diversi**).
- [ ] **Step 2: implementa**; `HomeRowViewModel.QuickActionAsync` passa `null` per ora (il giorno arriva nel Task 6).
- [ ] **Step 3:** verdi, WPF compila, commit `feat(presentation): il log rapido riceve il giorno; un solo timbro per oggi e per i giorni passati`.

### Task 5: La ricaduta con una data

**Livello:** Sonnet. **Fermati e sali (Opus) se** per farla serve cambiare come `GetStateAsync` o gli streak leggono i
periodi.

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/IAbstinenceService.cs`; `src/Recordum.Data/Services/AbstinenceService.cs`;
  `Quick/AbstinenceQuickActionHandler.cs`; `HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs`;
  `Today/ResistanceTodayContent.cs`; `src/Recordum.UI/Views/HabitScreen/Head/AbstinenceCommandView.axaml(.cs)`;
  `Records/AbstinencePeriodListView.axaml`
- Test: `tests/Recordum.Data.Tests/` (classe dei test di `AbstinenceService`), Presentation e UI

**Interfaces — Produces:**
- `Task IAbstinenceService.LapseAsync(Guid habitNodeId, DateOnly? day = null, CancellationToken ct = default)` — `null` o
  oggi: chiude adesso (come prima). Un giorno passato `D` (confine dell'abitudine): la fine è `NoonOf(D)`; se non è dopo
  l'inizio del periodo aperto, è l'ultimo millisecondo di `D`; se anche quello non è dopo l'inizio (il periodo è cominciato
  dopo `D`), lancia `AbstinenceLapseBeforeStartException` (nuova, in `Recordum.Core/Errors`) e non scrive. Un giorno futuro:
  `ArgumentOutOfRangeException`.
- `AbstinenceQuickAddViewModel`: `LapseCommand` usa `CompletionDay` (il giorno scelto nella schermata); l'eccezione diventa
  `ErrorMessage = Strings.Abstinence_Error_BeforeStart`; `event EventHandler? LapsePromptRequested` +
  `void RequestLapsePrompt()` (la vista apre il flyout di conferma).
- `AbstinenceQuickActionHandler.ExecuteAsync(id, day, ct)`: se non in astinenza **e** `day` è null/oggi → `StartAsync`,
  `true`; in ogni altro caso `false` (si apre la schermata: dalla Home **non si chiude più un periodo con un clic**).

Stringhe nuove: `Head.Abstinence.LapseOn` "The relapse goes on {0}." / "La ricaduta va su {0}." (nel flyout, solo se il
giorno non è oggi) · `Abstinence.Error.BeforeStart` "On that day the abstinence had not started yet." / "Quel giorno
l'astinenza non era ancora cominciata."

- [ ] **Step 1: test rossi** (Data): `Lapse_on_a_past_day_ends_at_noon_of_that_day`; `..._same_day_as_start_ends_at_the_end_of_that_day`
  (inizio alle 15:00 di D: fine > inizio); `..._before_the_start_throws_and_writes_nothing`; `..._future_day_throws`;
  `..._without_open_period_does_nothing`; `Lapse_without_day_ends_now` (il default risponde **diverso** dal giorno passato).
  Presentation: `Home_quick_action_never_lapses` (in astinenza → false, nessuna chiamata a `LapseAsync`);
  `Home_quick_action_starts_only_today`. UI (B5): `Relapse_text_is_muted_not_red` (`RelapsedOnDisplay` e la barra del
  periodo chiuso usano `Brush.TextMuted`/`Brush.Border`, non `Brush.Danger`); il pulsante dice "Segna una ricaduta…" ed è
  `Button.record` senza `danger`; la data della ricaduta ha il formato del resto dell'app ("16 ott").
- [ ] **Step 2: implementa.** Il flyout mostra `Head.Abstinence.LapseOn` col giorno lungo quando `CompletionDay` non è oggi.
- [ ] **Step 3:** verdi (Data con `--filter "Category!=WindowsOnly"`), commit `feat(data): la ricaduta con una data; dalla Home non si chiude un periodo con un clic; ricaduta in testo attenuato (B5)`.

### Task 6: Il giorno corrente in `HomeViewModel`

**Livello:** Sonnet.

**Files:**
- Modify: `HomeViewModel.cs`, `HomeRowViewModel.cs`, `StripCell.cs`, `GroupProgress.cs`
- Create: `src/Recordum.Presentation/ViewModels/Home/DayHeaderCell.cs`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/Home/HomeDayTests.cs` (nuovo); `HomeHeaderTests`, `GroupProgressTests`,
  `HomeRowStripCellsTests` (aggiornati)

**Interfaces — Produces** (`HomeViewModel`):
- `int DaysBack` (osservabile, 0 = oggi) · `int MaxDaysBack` (lo imposta la vista: colonne visibili − 1; predefinito 13;
  se scende sotto `DaysBack`, `DaysBack` diventa `MaxDaysBack`) · `DateOnly SelectedDay` · `bool IsOnToday`.
- `string Title` ("Oggi" / il giorno lungo con l'iniziale maiuscola, "Lunedì 9 novembre"; in Archivio e Cestino il nome
  della scheda) · `string DateLabel` (oggi: la data lunga; passato: "ieri" / "{0} giorni fa") · `string DayCountText`
  ("4 di 9": le abitudini non archiviate fatte in `SelectedDay` sul totale; riusa `Home.Group.Progress`) ·
  `string? PastDayBanner` (null su oggi).
- `PreviousDayCommand`, `NextDayCommand` (con `CanExecute` **e** guardia interna), `SelectDayCommand(int daysBack)`,
  `BackToTodayCommand`. Attivi solo in `HomeTab.Active`; cambiare scheda riporta a oggi.
- `ImmutableArray<DayHeaderCell> DayHeader` al posto di `StripHeader` —
  `record struct DayHeaderCell(string ShortName, string Initial, string FullName, int DaysBack, bool IsToday, bool IsSelected)`
  (`ShortName` = "ddd", per oggi `Home.StripHeader.Today`; `Initial` = la prima lettera di "ddd", minuscola nella cultura).
- `StripCell(string Glyph, bool On, bool IsToday, bool IsSelected = false)`.
- `HomeRowViewModel(..., int daysBack = 0)`: `IsDoneOnDay` (per `daysBack == 0` è `row.IsCompletedToday`; altrimenti la
  cella di quel giorno ha un conto o un valore), la cella scelta marcata `IsSelected`, la cifra chiave **del giorno** per
  `CounterTotal` e `StateValue` (dal valore di `ValuesLast14Days`; l'etichetta dello stato solo per oggi) e per
  `DoneToday`/`JournalToday`; streak, orologio dell'astinenza e "voci in settimana" restano di adesso.
  `QuickActionAsync` passa `daysBack == 0 ? null : SelectedDay`.
- `GroupProgress.Count(byParent, groupId, Func<HomeTreeRow, bool> isDone)`; il filtro "fatte" e il conto dei gruppi usano
  il giorno scelto.

Stringhe nuove: `Home.Day.Previous` "Previous day" / "Giorno prima" · `Home.Day.Next` "Next day" / "Giorno dopo" ·
`Home.Day.Yesterday` "yesterday" / "ieri" · `Home.Day.DaysAgo` "{0} days ago" / "{0} giorni fa" · `Home.PastDay.Banner`
"You are logging {0}. What you record goes on that day." / "Stai segnando {0}. Quello che registri va su quel giorno." ·
`Home.PastDay.BackToToday` "Back to today" / "Torna a oggi".

**Come:** il cambio di giorno **non rilegge il database**: ricostruisce le righe da `_lastFetched` conservando la selezione
(lo stesso giro di `ReloadPreservingSelectionAsync`, senza `LoadAsync`) e rifà intestazione e conti. Il ritorno a oggi al
cambio di giorno sta sul battito di `HomeClockTicker` (che `Rebuild` svuota: si registra di nuovo lì): se
`appDay.Today(clock)` è cambiato → `DaysBack = 0` e `LoadAsync`.

- [ ] **Step 1: test rossi** (`HomeDayTests`; tre abitudini e un gruppo costruiti con i fake dei test Presentation, non
  con `ManifestHome`, che vive nei test UI):
  - `Starts_on_today`; `Previous_day_moves_title_date_count_and_banner` ("Lunedì 9 novembre", "ieri", "1 di 3", fascia con
    "lunedì 9 novembre"); `Back_to_today_clears_the_banner`.
  - `Cannot_go_past_the_window_or_into_the_future` (comandi e guardia interna via `ExecuteAsync`).
  - `Rows_mark_the_selected_day_and_use_its_values` (cella `IsSelected`, `IsDoneOnDay`, cifra chiave del contatore = il
    valore di ieri).
  - `Done_filter_and_group_progress_follow_the_day`.
  - `Quick_action_on_a_past_day_passes_that_day` e `..._on_today_passes_null` (handler finto che registra l'argomento).
  - `Changing_day_does_not_refetch` (il fake conta le chiamate a `GetHomeTreeAsync`) e `..._keeps_the_selection`.
  - `Switching_tab_returns_to_today`; in Archivio e Cestino `PreviousDayCommand.CanExecute` è falso.
  - `Day_rollover_returns_to_today_and_reloads` (**Review Focus 1**: orologio finto oltre il confine, un battito).
  - `Shrinking_the_window_clamps_the_selected_day` (**Review Focus 4**: `DaysBack = 6`, `MaxDaysBack = 3` → 3).
- [ ] **Step 2: implementa.** `StripHeader`, `TodayLabel` e `ShowStripHeader` si sostituiscono; il compilatore elenca i
  chiamanti (WPF compresa: lì `DayHeader` si adatta col minimo, mostrando `Short`).
- [ ] **Step 3:** verdi, commit `feat(presentation): la Home ha un giorno corrente (A1)`.

**Com'è stato fatto (2026-11-12)** — ciò che i task dopo trovano diverso da qui sopra:
- `DayHeaderCell.ShortName`, non `Short` (CA1720 vieta un identificatore che è un nome di tipo).
- `ShowDayHeader` al posto di `ShowStripHeader`: falso anche fuori dalle Attive. In `HomeView.axaml` l'intestazione è ancora
  la `HeatmapStrip` di prima, alimentata da `DayHeader` col convertitore `HomeView.HeaderCells`: il Task 7 toglie entrambi.
- La riga riceve il giorno da registrare in `HomeRowViewModel.RecordDay` (`init`, null = oggi), non lo ricalcola.
- **Il giorno della Home è dell'app, la striscia è dell'abitudine**: `GroupProgress.CellBack(row, daysBack, clock)` dice di
  quante celle indietro sta il giorno scelto nella striscia di *quella* riga (Review Focus 2: reset 05:00, app 00:00, alle
  02:00 "ieri" della Home è l'ultima cella dell'abitudine, quella che il log rapido riempie). Cella scelta, `IsDoneOnDay`,
  filtro "fatte" e conti passano tutti da lì; `GroupProgress.HasEntryOn` / `IsDoneOn` / `CountOn` / `ValueOn` leggono il giorno.
- `GroupProgress.Count(byParent, Guid? groupId, isDone = null)`: `isDone` facoltativo (la Panoramica resta su oggi),
  `groupId` null conta dalla radice (è il "4 di 9" della testa).
- Il ritorno a oggi al cambio di giorno sta nel **caricamento** (`ResolveAppDayAsync`), non solo nel battito: un evento che
  ricarica dopo mezzanotte, prima del battito, non sposta "ieri" di un giorno. Il battito si limita a ricaricare.
- La Home ora batte sempre (una callback per il cambio di giorno), anche senza righe di astinenza.

### Task 7: La testa su una riga, l'intestazione che si clicca, la fascia

**Livello:** Sonnet.

**Files:**
- Modify: `src/Recordum.UI/Views/HomeView.axaml(.cs)`, `Controls/HeatmapStrip.axaml.cs` (via il ramo `IsHeader`; la cella
  scelta), `Views/Rows/RowCardStyles.axaml`, `Themes/Icons.axaml` (mancano `Icon.ChevronLeft` e `Icon.Ellipsis`: i
  tracciati Lucide, come le altre)
- Create: `src/Recordum.UI/Views/Home/DayHeaderView.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/HomeViewTests.cs`, `HomeLayoutTests.cs`, `GiornataScreenshotTests.cs`

**Cosa** (mockup `home-giorno.html`, "Proposta"):
- **Una riga**: `Title` (20/600) · `DateLabel` · `DayCountText` · ‹ › (`Button.ghost`, icone `Icon.ChevronLeft/Right`,
  tooltip `Home.Day.Previous/Next`) │ lente (apre la casella di ricerca al posto di data e conto; Esc o casella vuota senza fuoco
  la richiude; Ctrl+F la apre) · "fatte" · ordinamento · ⋯ (`Icon.Ellipsis`; `MenuFlyout`: Attive, Archivio, Cestino, con la spunta
  sulla scheda attiva) · "+ Nuova". I segmenti Attive/Archiviate/Cestino spariscono; Ctrl+1/2/3 restano.
- In Archivio e Cestino: niente ‹ ›, niente conto, niente intestazione dei giorni; accanto al titolo `Button.ghost`
  "Torna alle abitudini" (`Home.BackToActive`, "Back to habits").
- **Sotto i 520 px** la testa va su due righe (titolo, data, conto, ‹ › sopra; gli strumenti sotto, a destra); sotto i 320
  la data si nasconde, come oggi.
- **La fascia** (`PastDayBanner` non null): sotto la testa, fondo `Brush.WarningSurface`, testo `Brush.Warning`, a destra
  "Torna a oggi" sottolineato (`Button.ghost`). Nessun colore nuovo.
- **`DayHeaderView`**: un `ItemsControl` orizzontale su `DayHeader`, un `Button` per giorno largo quanto una colonna della
  striscia, `Command = SelectDayCommand`, `ToolTip` e `AutomationProperties.Name` = `FullName`; il giorno scelto è la
  pastiglia d'inchiostro (`Brush.Ink` / `Brush.OnInk`), oggi non scelto in `Brush.Text` 600. Mostra gli ultimi N giorni
  come le strisce (`HeatmapStrip.VisibleDays`, ereditato). La vista imposta `MaxDaysBack = N − 1`.
- **La colonna del giorno marcata su ogni riga**, anche oggi: la cella `IsSelected` ha il fondo del colore dell'abitudine
  al 26% in chiaro e 36% in scuro (i valori del mockup), raggio 5.

- [ ] **Step 1: test rossi:** `Head_is_one_row_at_900` (titolo e "+ Nuova" sulla stessa riga; nessun `HomeTabs`);
  `Head_wraps_under_520`; `Clicking_a_header_day_selects_it` (clic sul terzultimo → `DaysBack == 2`, titolo cambiato);
  `Header_days_sit_over_their_columns` (±1 px, per ogni colonna visibile); `Selected_day_is_marked_on_every_row`
  (anche con `DaysBack == 0`); `Banner_shows_only_on_a_past_day_and_goes_back`; `Narrowing_the_view_clamps_the_day`
  (**Review Focus 4**: da 900 a 360 con `DaysBack = 6`); `More_menu_switches_tabs_and_archive_has_no_day_controls`;
  `Search_opens_from_the_lens_and_from_ctrl_f`.
- [ ] **Step 2: implementa.** I PNG del Task 3 si rifanno; in più `home-manifesto-ieri-{theme}.png` (un giorno passato,
  con la fascia).
- [ ] **Step 3:** verdi, guardare i PNG, commit `feat(ui): la testa della Home su una riga, i giorni si cliccano, fascia sul giorno passato (A1, A3)`.

**Com'è stato fatto (2026-11-12)** — ciò che i task dopo trovano diverso da qui sopra:
- **Chi ha scritto**: test, stringhe e disegno in sessione; l'implementazione (`HomeView`, `DayHeaderView`, `HeatmapStrip`)
  l'ha scritta Antigravity con `gemini-4-hydrogen-high` da un brief coi percorsi (140 s, compila al primo colpo, 40 test
  su 41 verdi al primo giro); tre ritocchi in sessione dopo aver guardato i PNG.
- **La testa va a capo quando non ci sta, non sotto una soglia fissa**: `HomeHeadPanel` (in `HomeView.axaml.cs`) tiene
  titolo e strumenti sulla stessa riga finché ci stanno e altrimenti manda gli strumenti sotto, a destra (è il `flex-wrap`
  del mockup). Sotto i 520 px va sempre a capo; con un titolo lungo ("Mercoledì 30 settembre") può andarci anche poco
  sopra. Nel blocco del titolo cede prima la data, poi il titolo (puntini); ‹ › e conto non si tagliano mai.
- Sotto i 400 px "fatte" resta la sola icona (col tooltip); sotto i 340 circa gli strumenti vanno a capo fra loro
  (`WrapPanel`): a 280 px "+ Nuova" sta su una terza riga.
- **La ricerca è stato della vista** (`_searchOpen`): Esc la chiude **e toglie il filtro** (una ricerca che non si vede
  non deve filtrare); con del testo dentro resta aperta anche senza fuoco, e resta a vista anche se non trova niente.
- **Il menu ⋯** usa le parole delle schede di prima (`Home.Tab.*`: "Attive / Archiviate / Cestino"), non "Archivio": il
  titolo della scheda (Task 6) dice già "Archiviate". La scheda attiva ha il segno del `MenuItem` a scelta singola
  (`ToggleType="Radio"`: un punto, non una spunta). Stringhe nuove oltre a `Home.BackToActive`: `Home.Search`
  "Search" / "Cerca" e `Home.More` "Archive and trash" / "Archivio e cestino" (nomi accessibili di lente e ⋯).
- `MaxDaysBack` lo imposta `HomeView.ApplyWidthRules` (colonne dell'intestazione − 1) **solo se c'è almeno una striscia**:
  una ricerca senza risultati non riporta a oggi.
- La cella del giorno scelto è un `Border` di nome `HeatmapStrip.SelectedCellName` attorno al `TextBlock`: il colore
  dell'abitudine mescolato al pannello (26% chiaro, 36% scuro), opaco. Gli stili del giorno stanno in `DayHeaderView`;
  `RowCardStyles.axaml` non è stato toccato.
- `DayHeaderView` ricrea i suoi pulsanti a ogni cambio di giorno: il fuoco da tastiera su un giorno si perde dopo Invio.
  Lo riprende il Task 11 (← →).

### Task 8: Dalla Home si apre la schermata sul giorno scelto

**Livello:** Sonnet.

**Files:** `MainWindowViewModel.cs`; `AbstinenceCommandView.axaml.cs`; test in `MainWindowViewModelTests.cs` e
`tests/Recordum.UI.Tests/HabitScreen/`

- [x] **Step 1: test rossi:** `Opening_a_habit_from_a_past_day_selects_that_day` (selezione della riga **e** log rapido
  che torna `false`: dopo `OpenSubjectAsync`, `Screen.SelectedDay == Home.SelectedDay`); `Opening_from_today_leaves_the_screen_on_today`
  (non chiama `SelectDayAsync`: un diario con una modifica aperta non riceve la domanda "cambiare giorno?");
  `Quick_action_on_an_abstinence_asks_for_the_relapse_prompt` (in astinenza: schermata aperta sul giorno,
  `LapsePromptRequested` alzato una volta); UI: il flyout si apre e mostra "La ricaduta va su lunedì 9 novembre."
- [x] **Step 2: implementa** nei tre punti che aprono un soggetto dalla Home (`SelectedRow`, `HabitActivationRequested`,
  il terzo `OpenSubjectAsync`): dopo l'apertura, se `!Home.IsOnToday`, `await screen.SelectDayAsync(Home.SelectedDay)`.
- [x] **Step 3:** verdi, commit `feat(presentation): dalla Home la schermata si apre sul giorno scelto; l'astinenza propone la ricaduta con quella data`.

**Com'è stato fatto (2026-11-13)** — ciò che i task dopo trovano diverso da qui sopra:
- **Il giorno si dà solo quando è l'utente ad aprire.** La Home rifà le righe a ogni registrazione e a ogni cambio di
  giorno, e ri-seleziona la stessa abitudine con una riga nuova: `SelectedRow` cambia senza che nessuno abbia aperto
  niente. `MainWindowViewModel` tiene l'id dell'ultima selezione (`_selectedSubject`) e su una ri-selezione non tocca la
  schermata, altrimenti il giorno scelto *dentro* la schermata tornava indietro a ogni registrazione
  (`Rebuilding_the_Home_does_not_move_an_open_screen`). Ne segue: cambiare giorno nella Home **non** sposta una
  schermata già aperta; la sposta il clic sulla riga o il log rapido.
- **Due punti, non tre**: `SelectedRow` e `HabitActivationRequested`, tutti e due da `LandOnHomeDayAsync(row, recording)`.
  Il giorno è `row.RecordDay` (quello che la riga dà al log rapido, null su oggi), preso dalla riga al momento del gesto.
  Un **gruppo** resta su oggi come la sua Panoramica (Task 6), quindi `GroupCreated` e un figlio aperto dalla Panoramica
  (`SubjectActivationRequested`) non sono toccati.
- **"Ha chiesto di registrare" o "ha selezionato"**: `HomeViewModel.RecordRequestedFor` vale solo mentre la Home alza la
  selezione che segue un log rapido che ha risposto `false`; l'host lo legge lì. La ricaduta si propone solo in quel caso
  (anche su oggi), mai su una semplice selezione.
- **La domanda aspetta la vista**: una schermata appena aperta non ha ancora la sua `AbstinenceCommandView`, e l'evento
  cadeva nel vuoto (provato: senza l'attesa il test d'insieme è rosso). `RequestLapsePrompt` segna la richiesta,
  `TakeLapsePrompt()` la consuma una volta; la vista la prende all'evento, al cambio di `DataContext` e a `OnLoaded`.
  Senza un periodo aperto `RequestLapsePrompt` non fa niente.
- **Riaprire una scheda che sta caricando aspetta il caricamento** (`SubjectTabViewModel.Loading`): un doppio clic sul
  log rapido non dà il giorno a una schermata a metà.
- Test: otto in `MainWindowViewModelTests`, tre in `AbstinenceQuickAddViewModelTests`, uno in `WorkspaceViewModelTests`,
  `HeadCommandTests.A_requested_relapse_prompt_opens_the_flyout_on_the_chosen_day` e, da capo a fondo col contenitore vero,
  `ShellIntegrationTests.Quick_action_on_an_abstinence_from_a_past_day_opens_the_relapse_prompt_on_that_day`. Sei prove del
  rosso. Scritto in sessione, senza Antigravity (task piccolo: sei file, un'ottantina di righe).
- Resta aperto **GIO-6** (tracker): tornata la Home a oggi, una schermata aperta da un giorno passato resta su quel giorno.

### Task 9: Le tre righe — preferenza, misure, striscia a segni

**Livello:** Sonnet.

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/Home/HomeRows.cs`; `src/Recordum.Presentation/Services/HomeRowsPreference.cs`;
  `src/Recordum.UI/Controls/HomeRowsMetrics.cs`
- Modify: `HomeViewModel.cs`; `src/Recordum.UI/Bootstrap/AvaloniaShellServices.cs`; `App.axaml.cs` (inizializzazione accanto
  a quella del tema); `HeatmapStrip.axaml.cs`; `Views/Rows/HabitRow.axaml(.cs)`, `GroupRow.axaml(.cs)`; `HomeView.axaml.cs`;
  `Views/Home/DayHeaderView.axaml.cs`
- Test: `tests/Recordum.Presentation.Tests/Services/HomeRowsPreferenceTests.cs`; `tests/Recordum.UI.Tests/HomeRowsTests.cs`;
  `CompositionTests.cs`; `GiornataScreenshotTests.cs`

**Interfaces — Produces:**
- `enum HomeRows { Compact, Marks, Comfortable }`.
- `sealed class HomeRowsPreference(IAppSettingService settings, HomeRows fallback)`: `HomeRows Current` (prima di
  `InitializeAsync` vale `fallback`), `ValueTask InitializeAsync(ct)` (`GetEnumAsync<HomeRows>("home.rows")`; null o
  illeggibile → `fallback`, mai un'eccezione), `ValueTask SetAsync(HomeRows value, ct)` (scrive, aggiorna, alza `Changed`
  solo se cambia), `event EventHandler? Changed`. Singleton; in `AvaloniaShellServices` con `fallback: HomeRows.Comfortable`
  (la testa Android userà `Compact`). **WPF non la registra**: è congelata, e `HomeViewModel` senza preferenza vale
  `Comfortable`.
- `HomeViewModel(..., HomeRowsPreference? rows = null)`: `HomeRows RowsStyle` (osservabile, segue `Changed`; si stacca in
  `Dispose`).
- `HomeRowsMetrics` (UI): `static readonly AttachedProperty<HomeRows> RowsProperty` (**ereditata**), `Get/SetRows`;
  `static HomeRowsMetrics For(HomeRows)`; `static int VisibleDays(HomeRows, double width)`. **La tabella è una sola**, ed è
  qui che si bilancia:

  | | altezza riga | comando | colonna | spazio | intestazione | segni |
  |---|---|---|---|---|---|---|
  | `Comfortable` | 36 | 28 | 26 | 0 | `Short` ("mer", "oggi") | no |
  | `Compact` | 30 | 24 | 26 | 0 | `Initial` ("m") | no |
  | `Marks` | 30 | 24 | 14 | 3 | `Initial` | sì |

  Giorni visibili: ≥ 640 px → 14; ≥ 400 → 7; ≥ 340 → 5; sotto → 4. `Marks` non scende mai sotto 7.
  `Comfortable` è la Home di oggi: **nessun pixel cambia** (i PNG del Task 7 restano uguali).
- `HeatmapStrip`: legge `HomeRowsMetrics` dalla proprietà ereditata e si ridisegna quando cambia. Con `Marks` disegna
  quadratini 14×14, raggio 4: vuoto = `Brush.Border`; pieno = il colore dell'abitudine a tre
  intensità (frazione ≤ 1/3 → 35%, ≤ 2/3 → 65%, oltre → 100%; i tipi sì/no sempre 100%); la cella `IsSelected` ha un
  contorno d'inchiostro di 2 px staccato di 1. Le cinque forme per tipo (`Kind`) restano per chi le usa già: la Home non le
  usa più.
- `HabitRow`/`GroupRow`: altezza e lato del comando dalla tabella (code-behind sulla proprietà ereditata; via il
  `StaticResource Layout.RowHeight` dalle due righe).

- [x] **Step 1: test rossi:**
  - Presentation: `Defaults_to_the_fallback`; `Reads_the_saved_value`; `Unknown_value_falls_back_without_throwing`
    (**Review Focus 5**: "Gigantic" nel fake); `Set_persists_and_raises_changed_once`; `Home_follows_the_preference_live`
    (`SetAsync(Marks)` con la Home costruita → `RowsStyle == Marks`, una notifica).
  - UI: `Comfortable_keeps_today_measures` (riga 36, colonna 26); `Compact_rows_are_30_and_header_shows_initials`;
    `Marks_draws_squares_and_no_text` (7 `Border` 14×14 per striscia, nessun `TextBlock`; la cella scelta col contorno);
    `Header_sits_over_columns_in_every_style` (±1 px, i tre stili × 900 e 360); `Visible_days_table` (theory sulla tabella);
    `Changing_style_redraws_open_home` (**Review Focus 5**); `CompositionTests`: `HomeRowsPreference` risolta e passata alla Home.
- [x] **Step 2: implementa.**
- [x] **Step 3:** verdi; PNG `home-manifesto-{compact|marks|comfortable}-{theme}-{900|360}.png` (12), guardati tutti; commit
  `feat(ui): le righe della Home in tre versioni (Compatta, Segni, Larga)`.

**Com'è stato fatto (2026-11-13)** — ciò che i task dopo trovano diverso da qui sopra:
- **Chi ha scritto**: preferenza, `RowsStyle`, registrazione, avvio e tutti i test in sessione; la parte UI
  (`HomeRowsMetrics`, `HeatmapStrip`, righe, `HomeView`, `DayHeaderView`) Antigravity con `gemini-4-hydrogen-high` da un
  brief coi percorsi (157 s, diff fedele, due `using Avalonia;` mancanti). Un ritocco dopo i PNG: la striscia non ritaglia
  più (`ClipToBounds = false`), altrimenti del contorno di Segni si vedevano solo i lati.
- **La tabella dei giorni visibili vale anche per Larga**: a 360 px la Home mostra ora 5 giorni, non 4 (≥ 340 → 5).
  `Narrowing_the_view_clamps_the_day` è stato aggiornato (360 px → `MaxDaysBack` 4).
- `HomeRowsMetrics` è un `record` (`RowHeight, Command, Column, Gap, HeaderInitials, Marks`): `For(valore sconosciuto)` vale
  Larga. La proprietà ereditata `Rows` la imposta `HomeView` su di sé dal `RowsStyle` del ViewModel; righe, strisce e
  intestazione reagiscono al suo cambio. Via `HeatmapStrip.ValueSlotWidth` (la colonna è `metrics.Column`); il margine
  destro dell'intestazione lo calcola `HomeView` (`Command + 15`: 43 in Larga, com'era).
- Il comando di registrazione ha `x:Name="QuickLog"` ed è largo `Command`, alto `Command − 2` (28×26 in Larga, com'era).
- **Segni**: quadratini 14×14 raggio 4 a 3 di distanza, vuoto `Brush.Border`, pieno il colore mescolato **opaco** al
  pannello (35 / 65 / 100 % dalla frazione di `StripFractions`, i sì/no sempre 100 %); il giorno scelto ha un `Border`
  `SelectedDay` d'inchiostro, 2 px, staccato di 1. Intestazione con le iniziali, pastiglia alta 16 (classe `narrow` su
  `DayHeaderView`). Il valore di oggi accanto ai quadratini è la cifra chiave che la riga ha già.
- `HomeViewModel.Dispose` stacca la Home dalla preferenza (singleton, la Home è transient). La preferenza si inizializza in
  `App.StartAsync` dopo il tema. Un valore numerico come "7" passa `Enum.TryParse`: la preferenza lo scarta con
  `Enum.IsDefined`.
- Test: 9 in `HomeRowsPreferenceTests` (Presentation), 27 in `HomeRowsTests`, `CompositionTests.The_home_rows_preference_is_one_and_reaches_the_home`,
  12 PNG in `GiornataScreenshotTests.Home_in_three_versions`. Prove del rosso: valore "7" senza `IsDefined`, `Dispose` che
  non si stacca, `HomeView` che non segue `RowsStyle`, `Marks` senza il minimo di 7 giorni.
- Resta aperto **GIO-3** (in tutte e tre le versioni a 900 px l'intestazione ha 14 giorni e le righe non d'astinenza 7) e
  si apre **GIO-7** (in scuro i quadratini vuoti di Segni si leggono poco).

### Task 10: Impostazioni › Aspetto

**Livello:** Sonnet.

**Files:**
- Modify: `SettingsViewModel.cs`; `src/Recordum.UI/Views/Settings/SettingsWindow.axaml(.cs)`;
  `Services/AvaloniaSettingsWindowLauncher.cs`
- Create: `src/Recordum.UI/Views/Settings/AppearancePage.axaml(.cs)`
- Test: `SettingsViewModelTests.cs`; `tests/Recordum.UI.Tests/Settings/AppearancePageTests.cs`

**Interfaces — Produces:** `SettingsViewModel(IThemeService, IAppFontService, HomeRowsPreference? rows = null)` con
`HomeRows HomeRows` (osservabile; letta in `InitializeAsync`; il cambio chiama `SetAsync`, non durante l'inizializzazione).

**Cosa** (mockup `oggi-striscia.html`, primo telefono): la finestra ha **due pagine**, "Aspetto" e "Dati e backup", scelte
da un segmento in testa (le categorie a sinistra arrivano con 3b: qui il minimo). `ShowAsync(null)` apre Aspetto,
`ShowAsync(SettingsCategoryId.Backup)` apre Dati e backup. La pagina Aspetto: **Tema** (Sistema · Chiaro · Scuro, segmenti
su `ThemeMode`) e **Righe della Home**: tre scelte a radio, ognuna con nome, una riga di spiegazione e **una riga vera
d'esempio** (`RowPreview` con `HomeRowsMetrics.SetRows` su di sé: "Lettura", sette giorni, oggi 35) disegnata in quella
versione; sotto, "Vale per questo dispositivo.". Il carattere dell'app resta a 3b.

Stringhe nuove: `Settings.Appearance.Theme` "Theme" / "Tema" (se non c'è già) · `Settings.Appearance.HomeRows` "Home rows" /
"Righe della Home" · `.HomeRows.Compact` "Compact" / "Compatta" · `.Compact.Hint` "The last days' values, low rows" / "I
valori degli ultimi giorni, righe basse" · `.HomeRows.Marks` "Marks" / "Segni" · `.Marks.Hint` "The week as squares, with
today's value" / "La settimana a quadratini, il valore di oggi" · `.HomeRows.Comfortable` "Comfortable" / "Larga" ·
`.Comfortable.Hint` "Day names, tall rows" / "I nomi dei giorni, righe alte" · `.HomeRows.DeviceNote` "Applies to this
device." / "Vale per questo dispositivo."

- [ ] **Step 1: test rossi:** `Home_rows_loads_from_the_preference`; `Changing_home_rows_saves_once_and_not_while_loading`;
  UI: `Appearance_page_shows_three_choices_each_with_its_own_row` (tre `RowPreview` con tre altezze/disegni diversi);
  `Picking_marks_updates_the_preference`; `Settings_open_on_appearance_and_backup_deep_link_still_works`;
  `Theme_segments_change_the_theme`.
- [ ] **Step 2: implementa.** PNG `settings-appearance-{light,dark}.png`.
- [ ] **Step 3:** verdi, commit `feat(ui): Impostazioni › Aspetto con il tema e le righe della Home`.

### Task 11: La tastiera

**Livello:** Sonnet.

**Files:** `src/Recordum.UI/Views/HomeView.axaml.cs` (`OnKeyDownTunnel`, `DispatchRowKey`), `MainWindow.axaml`;
`HomeViewModel.cs` (`SelectNext/Previous`); test `tests/Recordum.UI.Tests/HomeKeyboardTests.cs`

**La mappa** (A2; vale con il fuoco nella Home o da nessuna parte, mai rubando un tasto a `TextBox`, `Button`, `ComboBox`,
`MenuItem` a fuoco): ↑ ↓ riga prima/dopo (senza selezione: ↓ la prima, ↑ l'ultima; la riga entra in vista) · **Spazio** =
log rapido della riga scelta sul giorno della Home · **Invio** = apre e fissa la scheda (`HabitActivationRequested`) ·
← → giorno prima/dopo · **Ctrl+N** = nuova abitudine (`OpenNewHabitCommand`) · **Ctrl+,** = Impostazioni (`ShowSettingsCommand`).
Restano Ctrl+1/2/3, Ctrl+W, Ctrl+F, F2, Canc, Ctrl+E, Ctrl+R, Esc.

- [ ] **Step 1: test rossi** (uno per tasto, più): `Space_in_the_search_box_types_a_space`; `Arrows_do_nothing_while_renaming`;
  `Right_arrow_on_today_does_nothing`; `Space_on_a_group_does_nothing`; `Left_arrow_in_archive_does_nothing`;
  `Keys_are_ignored_when_focus_is_in_a_workspace_tab`.
- [ ] **Step 2: implementa.** I tooltip di ‹ ›, "+ Nuova" e del comando di riga dicono la scorciatoia.
- [ ] **Step 3:** verdi, commit `feat(ui): la tastiera registra dalla Home (A2)`.

### Task 8b: Dalla Home una spunta si toglie, con conferma

Deciso dall'autore il 2026-11-12 (cambia la "Decisione mia" 3). **Livello:** Sonnet (cancella dati: **mai** Haiku).

**Com'è oggi:** `ActionQuickActionHandler` registra sempre: un secondo clic su un'azione già fatta scrive una seconda
registrazione. Nella schermata `ActionQuickAddViewModel.ToggleAsync` alterna già, senza chiedere.

**Files:**
- Modify: `ViewModels/Quick/ActionQuickActionHandler.cs`; `Strings.resx` + `Strings.it.resx` + accessor
- Test: `tests/Recordum.Presentation.Tests/ViewModels/Quick/ActionQuickActionHandlerTests.cs`; uno nei test UI della Home

**Interfaces — Produces:**
- `ActionQuickActionHandler(ICompletionService, IClock, IResetTimeResolver, IConfirmationDialog)`. Il giorno è `day` o
  l'oggi del confine dell'abitudine, risolto una volta. Se quel giorno non ha registrazioni: registra, come oggi. Se ne
  ha: `ConfirmAsync(Home.Uncheck.Title, Home.Uncheck.Message col giorno)`; "sì" cancella **la più recente di quel giorno**
  (la stessa finestra di `ToggleAsync`: `RangeOf(day)` meno 1 ms — la finestra si scrive in un punto solo, usato da
  entrambi); "no" non scrive niente. In tutti e tre i casi torna `true`: la schermata non si apre.
- Vale solo per l'azione. Il contatore senza campi continua ad aggiungere uno: non è una spunta.

Stringhe nuove: `Home.Uncheck.Title` "Remove the check?" / "Togliere la spunta?" · `Home.Uncheck.Message` "The record
of {0} is deleted." / "La registrazione di {0} viene cancellata." (`{0}`: "oggi"/"today" o il giorno lungo, "lunedì 9
novembre").

**Trappole:** `IConfirmationDialog` è registrato in UI e in WPF (l'handler è condiviso: WPF riceve lo stesso
comportamento, col minimo); un test della registrazione DI se manca. La conferma è una finestra: nei test UI serve il
finto che risponde, come in `RecordsViewTests`.

- [ ] **Step 1: test rossi:** `Action_not_done_records_without_asking` (la conferma non viene mai chiesta);
  `Action_already_done_asks_and_removes_the_latest_of_that_day` (due registrazioni nel giorno: resta la più vecchia);
  `Declining_writes_nothing_and_does_not_open_the_screen`; `Removal_on_a_past_day_leaves_today_alone`;
  `Removal_uses_the_habit_boundary` (reset 05:00: una registrazione alle 02:00 appartiene al giorno prima). UI: clic su
  una riga già fatta → conferma chiesta una volta, "sì" → la riga torna non fatta.
- [ ] **Step 2: implementa.**
- [ ] **Step 3:** verdi, commit `feat(presentation): dalla Home una spunta si toglie, con conferma`.

---

## Parte 3 — La schermata dell'abitudine

### Task 12: L'elenco non ripete il giorno scelto (B1)

**Livello:** Sonnet.

**Files:** `HabitRecordsViewModel.cs`, `AbstinencePeriodListViewModel.cs`, `HabitScreenViewModel.cs` (`SelectDayAsync`,
il caricamento del periodo); test in `tests/Recordum.Presentation.Tests/ViewModels/HabitScreen/`

**Interfaces — Produces:** `DateOnly? HabitRecordsViewModel.ExcludedDay` (le registrazioni di quel giorno, col confine
dell'abitudine, non entrano nell'elenco del periodo); per l'astinenza l'elenco dei periodi salta quelli che il blocco del
giorno mostra già. Lo schermo lo imposta a `SelectedDay` e lo aggiorna a ogni `SelectDayAsync`.

- [ ] **Step 1: test rossi:** `Period_list_skips_the_selected_day` (tre giorni di registrazioni, scelto quello di mezzo:
  l'elenco ha gli altri due); `Selecting_another_day_brings_the_previous_one_back`; `Empty_message_does_not_lie` (un periodo
  con registrazioni solo nel giorno scelto **non** dice "niente registrato": l'elenco resta vuoto senza quel testo);
  `Abstinence_current_period_is_shown_once` (oggi scelto: "In corso" solo nel blocco del giorno); `With_notes_filter_still_works`.
- [ ] **Step 2: implementa.** **Step 3:** verdi, PNG `habit-all-columns-light.png` riguardato (una voce una volta), commit
  `fix(presentation): l'elenco della Cronologia non ripete il giorno scelto (B1)`.

### Task 13: Voce nei diari, nota sulle altre (B2)

**Livello:** Sonnet. **Fermati e sali se** per rendere il Markdown delle note serve un controllo nuovo: si riusa ciò che
l'elenco del periodo usa già per le voci del diario (`Controls/DiaryEditor` in sola lettura), e se cinquanta note rese
fanno scattare `HomePerformanceTests` o rendono lenta la colonna, ci si ferma con la misura in mano.

**Files:** `HabitScreenViewModel.cs` (`IsColumnApplicable`, `ShowNote`, i pulsanti delle colonne), `Notes/*ViewModel.cs`,
`src/Recordum.UI/Views/HabitScreen/Notes/NotesColumnView.axaml`, `History/DayBlockView.axaml`; test Presentation e UI

**Regole** (§6.3, nessuna migrazione):
- Su un **diario** la colonna Note non c'è (né il suo pulsante), **salvo** che il diario abbia già entità `Note`: allora
  c'è, le mostra e **non** offre "+ Nuova nota" (`bool Notes.IsReadOnlyLegacy`). Il conto si legge quando si carica il
  soggetto.
- Sulle **altre** abitudini e sui gruppi la colonna resta com'è, senza l'etichetta di categoria (Task 2).
- Le righe dell'elenco delle note e la nota nel blocco del giorno sono **rese**; il Markdown grezzo solo mentre si scrive.

- [ ] **Step 1: test rossi:** `A_journal_has_no_notes_column`; `A_journal_with_existing_notes_keeps_it_read_only`
  (**Review Focus 6**); `..._and_it_comes_back_when_notes_appear_after_reload`; `Other_types_keep_the_notes_column`;
  `Open_columns_survive_switching_between_a_journal_and_a_habit` (Note aperta su un'azione, passo a un diario e torno: è
  ancora aperta); UI: `Note_rows_render_markdown` (una nota `**lenta**`: la riga usa lo stesso controllo delle voci
  rese, e nessun `TextBlock` della riga contiene gli asterischi) e `Day_block_note_renders_markdown_until_edited`.
- [ ] **Step 2: implementa.** **Step 3:** verdi, PNG di un diario e di un'azione con note, commit
  `feat(presentation): voce nei diari, nota sulle altre abitudini; note rese (B2)`.

### Task 14: Lo stato col numero, "+ a mano" che sembra acceso (B3, B6)

**Livello:** Sonnet.

**Files:** `src/Recordum.UI/Views/HabitScreen/Head/StateCommandView.axaml`, `SessionCommandView.axaml`,
`ColumnButtonStyles.axaml`; `QuickAdd/MeasuredQuickAddViewModel.cs` (`ScalePoint`); test UI

- [ ] **Step 1: test rossi:** `State_steps_show_their_number` (dentro ogni gradino il valore intero, 13/500);
  `The_recorded_step_is_filled` (il gradino registrato nel giorno scelto ha il fondo pieno del colore dell'abitudine e il
  numero nel colore "su colore"; gli altri hanno contorno; `ScalePoint.IsCurrent` aggiornato dopo `RecordStepCommand` e a
  ogni cambio di giorno); `No_step_is_filled_on_a_day_without_a_value`; `Manual_button_is_an_outline_secondary` (B6:
  l'aspetto del `Button` senza classe, cioè il secondario a contorno della direzione visiva §4.4: fondo trasparente,
  bordo `Brush.Border`, testo `Brush.Text`; da premuto resta leggibile).
- [ ] **Step 2: implementa.** **Step 3:** verdi, PNG `habit-state-{light,dark}.png` e `habit-session-light.png` rifatti e
  guardati, commit `fix(ui): lo stato col numero e il gradino scelto pieno (B3); "+ a mano" a contorno (B6)`.

### Task 15: Schermate, inventario, documenti, review

**Livello:** Haiku per PNG e documenti; **Opus** per la review dell'intero branch.

- [ ] **Step 1: PNG.** Tutti quelli dei task, in chiaro e scuro, guardati uno per uno; nel report ciò che non torna.
  In più `home-manifesto-comfortable-light-900.png` accanto alla variante "numeri in inchiostro" (finding B9: solo
  un'immagine per la decisione dell'autore, nessun codice di prodotto).
- [ ] **Step 2: inventario.** Niente di ciò che la Home faceva si è perso: Attive/Archivio/Cestino, ricerca, "fatte", i tre
  ordinamenti, "+ Nuova" (abitudine e gruppo), rinomina, fissa, archivia, elimina, ripristina, trascinamento, selezione
  multipla, "reset 5:00", cifre chiave per tipo, cornici dei gruppi. Una voce persa è un difetto del task che l'ha tolta.
- [ ] **Step 3: documenti.** Tracker: sezione "Onda Giornata"; A1–A4, B1–B6, UX3A-Q1…Q4, W2V-20 `fixed` col test che lo
  prova. ROADMAP ("Dove siamo", passo 2 ☑), MILESTONES, `CLAUDE.md` §5 solo se nasce una regola (il timbro unico). Commit e
  push.
- [ ] **Step 4: review dell'intero branch** (Opus) con la spec e i due mockup in mano; un'onda di correzioni (le banali
  tornano a chi ha scritto); **CI verde** su Linux e Windows. Il merge in `main` dopo che l'autore ha guardato i PNG.

## Decisioni mie (da controllare leggendo)

1. **Il cambio di giorno non rilegge il database**: la Home ha già 14 giorni per riga. Più indietro di così dalla Home non
   si va (si apre l'abitudine); su una finestra stretta si va indietro quanto le colonne che si vedono.
   **Confermata dall'autore il 2026-11-12**: per la vista d'insieme ci sono i grafici e la schermata dell'abitudine; un
   lasso di tempo navigabile a scelta è un'idea per dopo, non prioritaria (vedi "Fuori dall'onda").
2. **"Fatta" in un giorno passato** = quel giorno ha un conto o un valore. Per oggi resta la risposta della query.
3. **La spunta dalla Home registra, non toglie** (com'è oggi). Il mockup la faceva alternare: togliere una registrazione
   con un clic su ieri mi sembra un rischio per i dati; resta "Annulla l'ultima" nella schermata. Se l'autore la vuole
   alternata, è un task a parte.
   **Cambiata dall'autore il 2026-11-12**: il clic su una spunta già fatta chiede conferma e, confermato, la toglie.
   È il Task 8b.
4. **Dalla Home l'astinenza non chiude più un periodo con un clic**: apre la schermata sul giorno e propone "Segna una
   ricaduta…". Oggi un clic sulla riga chiudeva il periodo senza chiedere. **Confermata dall'autore il 2026-11-12.**
5. **Una ricaduta su un giorno passato chiude il periodo e basta**: "Inizia" resta un gesto a sé, da adesso. I giorni fra
   la ricaduta e il nuovo inizio non sono "liberi". **Confermata dall'autore il 2026-11-12.**
6. **In Archivio e Cestino** il titolo dice dove si è e c'è "Torna alle abitudini"; niente giorno, niente conto.
7. **"x di y" della giornata** conta tutte le abitudini non archiviate, come i gruppi, anche quelle non in programma quel
   giorno.
8. **Le misure delle tre righe** sono una tabella sola in `HomeRowsMetrics`: Larga è la Home di oggi senza un pixel
   cambiato; Compatta abbassa la riga a 30 e usa le iniziali; Segni usa quadratini da 14. Sono la prima stesura da
   bilanciare a schermo; le misure da dito (38 e 48) arrivano con la testa Android.
9. **Segni disegna quadratini uguali per tutti i tipi** (come nel mockup), non le cinque forme per tipo della vecchia
   striscia.
10. **Impostazioni con due pagine e un segmento in testa**: il minimo perché "Aspetto" esista; le categorie a sinistra
    restano a 3b. La finestra si apre su Aspetto.
11. **La preferenza non è registrata in WPF** (congelata): lì la Home resta Larga.
12. **La guardia delle parole ha l'elenco nel test**, non lo legge dal glossario: meno codice; il glossario dice dove
    aggiungere.
13. **L'elenco delle scorciatoie** in Impostazioni › Informazioni aspetta 3b (la pagina non esiste); intanto le dicono i
    tooltip.

## Fuori dall'onda

- B7 (la durata come orologio accanto a un'ora), B8 (mappa dell'anno), B9 (valori nel colore: solo l'immagine di confronto).
- La cornice da telefono, le misure da dito, il foglio dal basso: testa Android (ROADMAP passo 4).
- Impostazioni a categorie, Generale, Informazioni con le scorciatoie, carattere dell'app: 3b.
- `home.rows` fuori dall'"unisci" (vale per dispositivo): va scritto nella spec dell'"unisci".
- La palette dei comandi: 1.1.
- Un lasso di tempo navigabile a scelta nella Home (oltre i 14 giorni delle righe): idea dell'autore del 2026-11-12, non
  prioritaria.
