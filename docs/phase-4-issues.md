# Recordum — Fase 4 issue tracker

> **Scopo**: lista vivente dei bug e delle rifiniture aperte in Fase 4. Sostituisce le sezioni "Bloccanti / Rifinitura" che erano in `CLAUDE.md` §7. CLAUDE.md mantiene solo il pointer.
>
> **Stati**: `reported` (utente lo segnala, non ancora investigato) → `verified` (riprodotto da claude / utente conferma stato attuale) → `triaged` (root cause ipotizzata + scope chiaro) → `in-progress` (fix in lavorazione su un branch) → `fixed` (commit fatto, build verde) → `verified-fixed` (utente conferma sul build).
>
> Quando un issue raggiunge `verified-fixed`, spostalo in fondo nella sezione "Risolti" con il commit SHA del fix.

---

## Come segnalare un nuovo bug

Quando ne incontri uno, copia il template qui sotto in una nuova voce `B<N>` (bloccante) o `R<N>` (rifinitura) sopra. Più dettagli = meno round-trip di domande.

```markdown
### B<N>. <Titolo breve, < 60 caratteri>
- **Stato**: `reported`
- **Quando**: 2026-MM-DD
- **Steps**:
  1. <ogni passaggio per riprodurre, dall'avvio dell'app>
  2. ...
- **Atteso**: <cosa dovrebbe succedere>
- **Osservato**: <cosa succede davvero — sintomo visibile>
- **Screenshot**: <path o "incolla in chat"> (opzionale ma molto utile per bug visuali)
- **Ambiente**:
  - Theme: <System | Light | Dark>
  - WebView2 Runtime installato: <yes/no, vedi Help → Diagnostic info>
  - Schema version: <da Diagnostic info>
  - Branch + SHA: <da Diagnostic info>
- **Note**: <qualsiasi info collaterale: prima funzionava? è successo dopo un'azione specifica? log/stack trace allegata?>
```

**Diagnostic info** (Help → Diagnostic info, dialog modale): copia-incolla l'output e hai l'intera sezione "Ambiente" già compilata.

**Log di crash**: se l'app cracha, `%LocalAppData%\Recordum\logs\recordum.log` ha lo stack trace appeso. Incollalo nella sezione "Note".

---

## Stato al 2026-05-29 (giornata estesa)

**Risolti/verificati questa sessione** (dettaglio in fondo + commit):
- **B1** "Save note" → era **theming chrome (R1)** + WebView2 falso negativo, non un bug di Save. Bottoni invisibili (chiaro-su-chiaro) risolti con chrome flat; editor markdown ripristinato.
- **WebView2 falso negativo** → probe spostato sul Dispatcher UI + logging. `verified-fixed` (editor ricco visibile).
- **R1** chrome flat Button/TabItem/ComboBox → `verified-fixed` (utente).
- **B4** sposta-nel-cestino/archivia non aggiornava la lista → `verified-fixed` (utente).
- **B5** "+"/registrazione + refresh Home su ogni completamento → `verified` (utente: "il riquadro si accende").
- **B6a** instanziazione template scartava metriche/scala/goal/segmenti → fixed (test verdi); scala 1-5 ora registra (`verified` utente).
- **B6c** UI sezioni editor (Scheduling/Streak/Metrics/Scales/Timer/Goal/Notifications/Tags/AI/ResetTime) costruita → `verified` funzionale (utente). Spec `docs/superpowers/specs/2026-05-29-editor-sections-ui-design.md`, piano `docs/superpowers/plans/2026-05-29-editor-sections-ui.md`.

**Risolti nel blocco "limatura" (2026-05-29 sera)** — `verified` da fare dall'utente al prossimo giro:
- **Theming Pass 3** (commit `4c6bac5`): CheckBox flat (implicito), toggle "Advanced" a pillola (stile keyed `Recordum.PillToggle`), DatePicker/Calendar tematizzati (template flat per CalendarDayButton/CalendarButton). Chiude **B7** (calendario chiaro-su-chiaro). NB minore: il navigatore mese del Calendar non persiste l'highlight selezione (usa IsPressed; `CalendarButton.IsSelected` non è trigger valido).
- **B9 i18n** (commit `3b42d2f`): 25 stringhe del'editor + picker localizzate (EN+IT). Restano in inglese solo i **nomi display dei template seed** (dati DB, non stringhe — localizzazione separata da decidere).
- **RB6c-1 day-picker** + **RB6c-2 chip Tag selezionate** (commit `50ef22d`): row di toggle giorni per schedule `SpecificDays` (convenzione `["Mon".."Sun"]` da `ScheduleEvaluator`); chip Tag ora mostrano stato selezionato (accent).
- Fix test culture-independent picker (commit `c0c515b`). Suite: WPF 335, Data 296 verdi.

**Ancora aperte**:
- **B6b** template personalizzato — **feature, richiede design** (brainstorming) prima di implementare. NON fatto di proposito.
- **RB6c-4** picker scala: il dropdown scala c'è, ma rifinitura labels/anteprima da fare.
- **R-scaleUI** layout scala State sgradevole (Cronologia).
- **R-statePlus** "+" su State non fa nulla se l'habit è già selezionata.
- **Theming**: `ToggleButton` generico resta Foreground-only (solo l'Advanced ha il pill keyed) — ok per ora.
- **Nav model** spec `2026-05-29-navigation-model-design.md` parcheggiato (colonne toggle) — dopo le basi.

---

## Stato al 2026-06-01

**Brainstorm "Recordum problemi 2" → spec + esecuzione**:
- **Spec** `docs/superpowers/specs/2026-06-01-abitudine-column-calendar-design.md` (commit `2519edb`): 4 pilastri — colonna Abitudine + `CalendarNavigator` riusabile, note unificate (`Note.TargetDate`), timer Session ibrido, modello a colonne (Abitudine rimpiazza Cronologia nei tab attuali; refactor colonne-chip rimandato).
- **B10** (Configura non personalizza) → `fixed` `3f4dce6` (vedi Risolti).
- **Piano 1 — fondamenta dati schema v2.8** (`docs/superpowers/plans/2026-06-01-schema-v28-data-foundation.md`, commit `3555f45`): **eseguito** subagent-driven, 5 task TDD. Schema **v2.8** additivo: `note.target_date` (+indice), `habit_config.note_marker_color` (excluded) + `session_repeat_count` (versioned+mirror), `segment_definition.duration_seconds`; migration `AbitudineCalendarV28`. Query `INoteService` note-per-giorno/mese. Suite **Data 301 / WPF 337** verdi.
- **Piano 2a — `CalendarNavigator` + tab Abitudine** (`docs/superpowers/plans/2026-06-01-calendar-navigator-abitudine-tab.md`, commit `c2b0370`): **eseguito** subagent-driven, 4 task TDD. Widget calendario riusabile (`CalendarNavigatorViewModel` + controllo, marker provider delegate) che rimpiazza la striscia heatmap nella prima tab, ora **Abitudine**; marcatori completato=colore habit (`GetHeatmapAsync`) / nota=`NoteMarkerColor` (`ListNoteDaysInMonthAsync`); giorno selezionato → `QuickAdd.CompletionDate`. Suite **WPF 345 / Data 301** verdi. **Verifica visiva utente: da fare.**
- **Piano 2b — note-del-giorno unificate** (`docs/superpowers/plans/2026-06-01-day-notes-abitudine.md`, commit `633d750`): **eseguito** (Task 1-2 subagent-driven, Task 3 inline per sovraccarico API). `DayNoteViewModel` (editor breve legato al giorno selezionato, scrive/aggiorna `Note.TargetDate`) nella tab Abitudine; `HabitScreenViewModel` si sottoscrive agli eventi nota → `Calendar.JumpAsync` (refresh pallini-nota); badge data nella timeline della colonna Note (`NoteRowViewModel.DateBadge`, "d MMM" o "Generale"). Suite **WPF 351 / Data 301** verdi. **Verifica visiva utente: da fare.**
- **Prossimo**: Piano 3 (timer Session ibrido — motore auto-avanzamento + ripeti ciclo, UI Timer in Configura, + picker colore-nota `NoteMarkerColor` in Configura). Poi i bug minori residui del report.

**Follow-up aperti dalle code-review del Piano 2a** (non bloccanti):
- **R-onAccent**: introdurre token `Brush.OnAccent` (foreground su accento) nei temi Light/Dark e usarlo al posto di `Foreground="White"` hardcoded (cella selezionata calendario + backfill `PillToggle`/`CalendarDayButton` in BaseStyles) — contrasto dark insufficiente. Pass di theming.
- **R-heatmapDead**: dopo lo swap, `HeatmapViewModel` è ancora costruito e ricaricato in `HabitScreenViewModel` ma non più mostrato nella prima tab (la year-heatmap del Dettaglio è un controllo separato). Rimuovere field/load/sottoscrizione + il `DataTemplate` morto (cleanup dedicato, tocca più superficie).
- **R-fakeWebView2-dup**: `FakeWebView2Availability` duplicata in `HabitScreenViewModelFactory` e `MainWindowViewModelFactory` — estrarre in un fixture condiviso.

---

## Stato al 2026-06-02

**Verifica visiva Piano 2a+2b (utente)** — feedback raccolto sulla tab Abitudine:
- ✓ Note del giorno: funzionano (calendario riflette i pallini, editor del giorno ok).
- **A — range anni navigatore troppo stretto**: dropdown solo 2021..2028 (`SelectableYears = Range(today-5, 8)`). I pulsanti ‹ › sono illimitati. → in Piano cell-markers Task 5.
- **B — DatePicker QuickAdd ridondante**: il `<DatePicker>` nei 5 template QuickAdd ha popup ancora chiaro-su-chiaro **ed** è ridondante (il calendario principale già pilota `CompletionDate`). → rimpiazzare con label read-only "Registrando per:", Piano cell-markers Task 9.
- **C — pallino completato si sovrappone al numero**: triage → brainstorming "marcatori cella calendario".

**Brainstorm "marcatori cella calendario" → spec + piano** (companion visivo):
- **Spec** `docs/superpowers/specs/2026-06-02-calendar-cell-markers-design.md` (commit `35926a8`): cella-giorno ridisegnata — **sfondo tinto** type-aware (heat, intensità relativa) = fatto a colpo d'occhio + quanto; **numero conteggio** type-aware (risolve "3 serie = 1 completamento": resistance conta `SeriesIndex` distinti); **angolino nota** colore per-abitudine (`note_marker_color` già in v2.8) + conteggio se >1; **striscia dettaglio** del giorno selezionato. Niente migrazione (solo due query nuove). Nessuna modalità configurabile (una regola automatica per tipo). **Idea "figura geometrica = goal" parcheggiata** (il modello non ha goal per-giorno: `GoalPeriodType ∈ {Fixed,Rolling,Open}`) — §9 della spec. Note correttive: "session" non è un `HabitType` (counter in `timer_mode='session'`, trattato come counter per ora); tint State relativo (assoluto su scaleMax = refinement).
- **Piano** `docs/superpowers/plans/2026-06-02-calendar-cell-markers.md`: 10 task TDD. Data (magnitudine type-aware, conteggio note) → host (calcola TintStep/Number/NoteCount) → CalendarNavigator muto (render) → converter tint → XAML cella → striscia dettaglio → picker colore-nota in Configura. Quick-fix A (Task 5) e B (Task 9) inclusi.
- **Scope shift**: il picker colore-nota (`NoteMarkerColor`) era in Piano 3; si sposta qui (riguarda il marcatore calendario). **Piano 3 resta solo il timer Session ibrido.**
- **ESEGUITO** subagent-driven (sessione 2026-06-02, branch `fase-4-polish`). 10 task + cleanup; per ogni task implementer → spec-review → code-quality-review. Commit `4a7e6a8`..`b22f09a` (14 feat/refactor + cleanup resx morta). Schema invariato (v2.8, due sole query nuove: `ICompletionService.GetDailyMagnitudeAsync` type-aware + `INoteService.ListNoteCountsInMonthAsync`). `NumberFor` riusato da cella calendario **e** striscia riepilogo (coerenti). `HabitColorTintConverter` (multi-binding hex+step→alpha). Nuova section editor `NotesSectionViewModel` (config-backed) per `NoteMarkerColor`. Quick-fix A (anni 15 indietro) + B (DatePicker QuickAdd → label `Abitudine.QuickAdd.RecordingFor`, vecchia chiave `HabitScreen.QuickAdd.Date` rimossa). Suite **Data 307 / WPF 362** verdi, build 0/0. Review finale olistica: **READY TO MERGE**. **Verifica visiva utente: da fare** (skill `run`).
  - **Catch della review**: la formula resistance "somma per-completamento dei SeriesIndex distinti" del piano è **corretta** per dominio (spec §riga 55) — un reviewer l'aveva flaggata per una parafrasi imprecisa del prompt, confermata sulla spec invece di propagare una falsa fix che avrebbe introdotto un bug.
- **Follow-up aperti (polish-debt, non bloccanti, dalla review finale)**:
  - **R-daySummaryUnits**: la striscia riepilogo mostra numeri nudi (es. State "✓ Done · 4"); la spec §5.5 voleva unità/scala ("Umore 4/5", "100 pagine"). Riusare nome metrica/scala primaria. Per State il "4" senza scala è opaco.
  - **R-noteCountContrast**: l'overlay conteggio-note in cella usa `Foreground="#1c1d22"` hardcoded sopra un colore-nota ora scelto dall'utente → rischio contrasto su colori scuri. Tematizzare (lega a `R-onAccent`).
  - **R-summaryGlyphs**: "✓"/"—" nella striscia sono glyph testuali (spec-acknowledged) ma in tensione col brief "no emoji nella chrome". Valutare icone Lucide.
  - `ListNoteDaysInMonthAsync` ora senza chiamanti di produzione (intenzionalmente tenuta in `INoteService` per usi futuri, spec §3.2); `CompletedColorHex` nome lievemente stale (è la sorgente del tint) — cosmetico.
  - **R-tintRamp** (verifica visiva 2026-06-02): l'effetto heat "è un po' meccanico" (utente). La ramp alpha fissa `{0x38,0x66,0x9e,0xd8}` su 4 step rende i salti netti/duri. Da rivedere (curva più morbida / più step / mescolanza con la surface invece di alpha puro). Non bloccante.
  - **Fix visivi post-verifica (commit `696d450`, verified-fixed utente)**: (1) il tint appariva come "pillola" attorno al numero — il `ControlTemplate` flat del Button centra il contenuto di default → `HorizontalContentAlignment/VerticalContentAlignment="Stretch"` sul Button cella; (2) il tint restava grigio default — `CalendarDayCellViewModel.TintColorHex` era `{get;set;}` non-observable → in `MultiBinding` il child-binding teneva il valore stale letto all'init; reso `[ObservableProperty]`. **Lezione**: ogni sorgente di un `MultiBinding` settata dopo l'init del binding DEVE essere observable, altrimenti resta al default.

**Brainstorm "creazione abitudini funzionale" → decomposizione 3 spec + Spec 1 eseguita** (companion visivo):
- **Decisione di fondo**: creazione = **costruttore block-first** (tela vuota → assembli mattoni → tipo dedotto → Lancia atomico, bozza fino al Lancia). I template restano l'on-ramp type-first (istanziano subito). Aggiunta utente: **template personalizzati**. Decomposto in **Spec 1** (builder non-sessione + draft/launch + template custom), **Spec 2** (modo sessione + runtime timer — assorbe il vecchio "Piano 3"), **Spec 3** (builder come "Configura" su abitudini esistenti con dati + unificazione draft↔live).
- **Spec** `docs/superpowers/specs/2026-06-02-modular-habit-builder-design.md` + **Piano** `docs/superpowers/plans/2026-06-02-modular-habit-builder.md` (14 task TDD). **ESEGUITO** subagent-driven (commit `d94ed8f`..`2af4529`). Grammatica pura `HabitCompositionRules` (mattoni→tipo + guardrail) in Core; `IComposedHabitService.CreateComposedHabitAsync` (Data, transazionale node+config+metriche); `HabitBuilderViewModel`/`HabitBuilderView` (composizione grammar-driven + anteprima); picker "Costruisci da zero" → builder (niente più nodo Action orfano); salva-come-template + sezione "I tuoi template" (rinomina/elimina). Suite **Core 59 / Data 309 / WPF 370** verdi, build 0/0. Review finale: **READY** dopo fix i18n. **Verifica visiva utente: da fare**.
  - **Catch della review (falso positivo evitato)**: in Spec 2 cell-markers la formula resistance del piano era corretta — confermata sulla spec invece di propagare la "fix". Stesso atteggiamento qui.
  - **Fix i18n post-review (commit `2af4529`)**: la view builder hardcodava ~20 stringhe italiane (violava English-first) e `DefaultName` italiano finiva nel `PayloadJson` persistito → localizzato tutto via resx (EN+IT+Designer), test reso culture-independent.
- **Follow-up aperti Spec 1 (deferiti, in questo tracker)**:
  - **R-builderFieldPickers**: i campi **Quantità/Scala** non hanno picker unità/scala nel builder → `CanLaunch` resta false e l'utente non capisce perché il Lancia è disabilitato (vicolo cieco silenzioso). Aggiungere i picker (riusare `IReferenceDataQueryService`) o nascondere quei mattoni / mostrare un hint "serve unità/scala".
  - **R-builderMode**: `RecordingMode` (cronometro/countdown) è nella grammatica ma **non esposto** nella view e **non persistito** (`BuildConfig`→null). Cablare `RecordingMode`→`HabitConfig.TimerMode` + controllo Mode quando si fa la Spec 2.
  - **R-templateFidelity**: il payload del template custom scrive `series_count`/`mode` che il reader (`HabitTemplateService.InstantiateAsync`/`TemplatePayload`) **ignora**, e non scrive unità/scala → serie-default e unità/scala persi al re-instantiate (nodo+metriche Name/Type round-trippano, `habit_type` sì). Allineare writer↔reader estendendo `TemplatePayload` (con i picker di sopra).
  - **R-builderSeriesGuard** (già fixato): CheckBox serie `IsEnabled={Binding CanEnableSeries}` + i mattoni invalidanti bloccati da `CanAddField` → composizione invalida non raggiungibile (verificato in review).

---

## Aperti — bloccanti

### B1. Save note non funzionante
- **Stato**: `reported` (2026-05-29 utente)
- **Sintomo**: dopo modifica di una nota nella tab Notes (o nel Journal QuickAdd), il bottone Save non persiste o resta disabilitato.
- **Ipotesi root cause**:
  - (a) Toast UI WYSIWYG non emette `'change'` → `MarkdownProperty` (TwoWay) non si aggiorna → `EditorMarkdown` resta uguale a `BodyMd` → `IsDirty` = false → Save disabilitato.
  - (b) Save abilitato ma `INoteService.UpdateAsync` non commita (improbabile, coperto da test).
  - (c) WebView2 non disponibile → fallback `TextBox`; il binding `TextBox.Text → Markdown` DP via `SetCurrentValue` propaga a source TwoWay? Da verificare.
- **Info da raccogliere prima del fix**:
  - Il bottone Save è cliccabile (Enabled) quando digiti, o resta grigio?
  - Hai WebView2 Runtime installato? Banner "WebView2 not available" visibile sopra HabitScreen?
  - Riavviando l'app la nota persiste o sparisce?

### B2. Costumizzazione note "non funziona particolarmente bene"
- **Stato**: `reported` (2026-05-29 utente)
- **Sintomo**: vago — formattazione markdown nell'editor non si comporta come atteso.
- **Info da raccogliere**: screenshot di un caso specifico (es. "scrivo `**bold**`, vedo `**bold**` invece di **bold**"). Questa è la differenza fra mode WYSIWYG e mode Markdown source.
- **Possibili sotto-bucket**:
  - Toolbar Toast UI ridotta non ha `table`/`task`/`image`/`source-view` toggle.
  - Tema dell'editor: Toast UI usa il suo CSS, non rispetta il dark/light di Recordum.
  - Preview/edit toggle nel WYSIWYG.

### B3. "Tante altre cose non funzionano"
- **Stato**: `reported` (2026-05-29 utente)
- **Sintomo**: generico. Da triagiare dettaglio per dettaglio (2026-05-30+).
- **Plan**: per ogni nuovo bug specifico, aprire una nuova voce B<N> qui sotto con sintomo + ipotesi + info da raccogliere.

---

## Aperti — rifinitura

### R1. Theming Pass 2 — chrome flat per Button/TabItem/ComboBox
- **Stato**: `triaged` (depends on B0 verified-fixed sotto)
- **Scope**: BaseStyles Pass 1 (commit `6a15cea`) cabla solo Foreground; il `ControlTemplate` Aero di Button/TabItem/ComboBox usa un gradiente chiaro hardcoded. In dark mode il testo è leggibile ma lo sfondo bottone resta light-gradient = ugly.
- **Approccio**: sostituire i 3 ControlTemplate con flat (Border + ContentPresenter) + Background = `Brush.SurfaceElevated` + hover state.

### R2. WebView2 fallback UX
- **Stato**: `triaged`
- **Scope**: se WebView2 Runtime non è installato, l'editor cade su `TextBox` senza preview markdown. Aggiungere banner di guida con link diretto al Runtime download.

### R3. Toast UI toolbar — opzioni mancanti
- **Stato**: `reported`
- **Scope**: vedi B2. Decidere se aggiungere `table`/`task`/`image`/`source-view` toggle alla toolbar.

### R4. BackupSchedulerHost — start delay configurabile
- **Stato**: `triaged`
- **Scope**: il fix di M3.10b è "wait one tick = 60min prima del primo run". Per polish: rendere il delay configurabile (es. 5min default per esperienza utente più reattiva).

### R5. Accessibility / keyboard navigation
- **Stato**: `reported`
- **Scope**: focus order, tab traversal, AutomationPeers, contrasto colori WCAG.

### R6. CLAUDE.md ridotto — strategia di consultazione dei file companion
- **Stato**: `fixed` (questo commit) — `CLAUDE.md` rimosso le sezioni storiche, ora pointer a `docs/MILESTONES.md`. Tracker bug in questo file.

---

## Risolti

### B10. Configura — le sezioni type-specific non compaiono al cambio tipo
- **Stato**: `fixed` (2026-06-01, su `fase-4-polish`)
- **Sintomo (report "Recordum problemi 2")**: creata un'abitudine vuota e aperta Configura, "nulla si inserisce affinché l'abitudine possa essere personalizzata" — niente unità/quantità/cronometro.
- **Root cause**: un'abitudine vuota nasce `Action`; le sezioni Metrics/Scales/Timer sono type-gated (Counter/Resistance/State). La visibilità (`IsVisible = IsApplicable(...)`) era calcolata **solo in `HabitEditorViewModel.LoadAsync`**. `OnSelectedNodeTypeChanged`/`OnSelectedHabitTypeChanged` persistevano il tipo ma **non ricalcolavano** la visibilità → cambiando il tipo a Counter le sezioni non comparivano mai senza riaprire l'editor.
- **Fix**: estratto `RefreshSectionVisibility()`, chiamato in `LoadAsync` + nei due handler di cambio tipo. Test rosso→verde `SectionVisibilityReactivityTests` (Action→Counter rivela Metrics/Timer; Habit→Group nasconde le sezioni habit-only). Suite WPF 337 verdi.
- **NB**: il persist delle sezioni era già corretto (`ApplyAsync`→`IHabitConfigService.UpsertAsync`); il difetto era solo la reattività della visibilità. Restano separati: creazione habit vuota senza prompt + apertura automatica della Cronologia (vedi B3/nuove voci).

### B0. Testo nero su nero in tema scuro — Pass 1
- **Stato**: `fixed` (2026-05-29, commit `6a15cea` su `fase-4-polish`)
- **Verifica utente**: in attesa (Pass 2 chrome flat è R1).
- **Root cause**: `App.xaml` non aveva implicit `Style` per i control type principali → Foreground default = nero anche in dark.
- **Fix**: `Resources/BaseStyles.xaml` con ~20 implicit `Style` cablati ai brush di tema via `DynamicResource`.

### B-prev. Crash tab Dettaglio al click (e poi al launch)
- **Stato**: `verified-fixed` (2026-05-29, commit `abb9e4c` su `fase-4-polish`)
- **Verifica utente**: ✓ confermato — "Dettagli ora va".
- **Root cause**: `CommandParameter="Days30"` string passato a `IAsyncRelayCommand<TrendRange>`. `RelayCommand<T>` throws `InvalidCastException` durante CanExecute check al template materialization.
- **Fix**: `CommandParameter="{x:Static vmd:TrendRange.Days30}"` (et al.) in `HabitScreen.xaml`.
