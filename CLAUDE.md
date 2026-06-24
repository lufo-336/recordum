# Recordum — Contesto di Progetto per Claude Code

> *Record your existence.* — Editor di abitudini open-source, offline-first, multipiattaforma. Questo file riassume gli invarianti e lo stato. **I documenti sorgente in §10 sono autoritativi** quando entrano in conflitto con questo riassunto.

---

## 1. Identità

Editor di abitudini con tipi multipli (action, counter, resistance, journal, state) componibili in alberi gerarchici. Rifiuta la riduzione *fatto / non fatto*: deve servire chi traccia salute cronica, allenamento, scrittura, astinenza, ecc.

- **Autore**: ITS Web Solutions Architect · Torino
- **Product spec**: v2.0 (aprile 2026)
- **DB schema**: v2.11 (giugno 2026) — delta `note_category` v2.11 (`notes.category` + `notes.completion_id`) > `counter_reclass` v2.10 (data-only: resistance-serie → counter) > `note_history` v2.9 > `abitudine_calendar` v2.8 > `backup_log` v2.7 > `streak_broken_at` v2.6 > `active_timer` v2.5 > PinScope v2.4 > provenance plugin v2.3 > v2.2 > v2.1.
- **Namespace**: `Recordum`

---

## 2. Principi (non negoziabili)

1. **Offline-first.** Dati sul dispositivo. Sync cloud opzionale via provider terzi — mai server proprietario.
2. **Nessun account obbligatorio.** Account solo per sync opzionale.
3. **Privacy by default.** AI disabilitata al primo avvio. Dati non lasciano il dispositivo senza consenso informato.
4. **Complessità per composizione.** Esigenze complesse = gruppi di habit semplici, mai opzioni gonfiate sulla singola habit.
5. **Profondità senza intimidazione.** Uso base in 30s; advanced disponibile ma mai d'intralcio (progressive disclosure).
6. **Open source.** Niente paywall sulle feature core.
7. **English-first.** Tutte le stringhe utente in `.resx` dalla prima riga. Inglese primario, italiano secondario.

---

## 3. Stack

| Componente | Tecnologia |
|---|---|
| Linguaggio | C# / .NET 10 |
| UI Windows (Fase 3) | WPF + CommunityToolkit.Mvvm |
| Persistenza | EF Core + SQLite + SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`) |
| Editor Markdown | WebView2 + Toast UI Editor (vendored) |
| Backend web (Fase 4) | ASP.NET Core + React/Svelte PWA |
| Export PDF | QuestPDF |
| Mobile (Fase 7) | .NET MAUI |
| Health (Fase 7) | CoreMotion (iOS) / Health Connect (Android) |
| Statistica (Fase 9) | Math.NET Numerics |

---

## 4. Architettura — 5 progetti `src/`

| Progetto | Responsabilità |
|---|---|
| **Recordum.Core** | Domain models, abstractions (`IClock`, `IPluginContext`). C# puro, zero dipendenze UI/EF. |
| **Recordum.Data** | `RecordumDbContext`, Fluent configurations, migrazioni. Referenzia Core. |
| **Recordum.Plugins.Contracts** | Interfacce host, manifest record, permission/extension-point/event ID. Referenzia Core. |
| **Recordum.Plugins** | `PluginManager`, `InProcessPluginHost`, `AlwaysAllowPermissionBroker`, `ExtensionPointRegistry`, `EventBus`, `ApiRouter`. In Fase 3 host in-process per first-party fidati. WebView2 host in Fase 7. |
| **Recordum.WPF** | App + Views + ViewModels + `Bootstrap/` (ServiceRegistrar, ApplicationInitializer, RecordumOptions). |

Core/Data/Plugins.Contracts/Plugins riutilizzati senza modifiche in Fase 5 (web) e Fase 7 (MAUI).

### Convenzioni di startup

- `App.OnStartup` costruisce il `ServiceProvider`, awaits `ApplicationInitializer.InitializeAsync()` che applica le migrazioni EF Core e tocca il `PluginManager`; on failure mostra dialog fatale localizzato e shutdown.
- SQLite **encrypted via SQLCipher**. Chiave master sempre da `IDbKeyProvider` iniettato — mai hardcoded. Fase 3 ships solo lo skeleton; in Milestone 3.2 arrivano `DpapiKeyProvider` (default) e `PassphraseKeyProvider` (opt-in Argon2id). Transizione DPAPI→passphrase è one-shot decrypt+re-encrypt; passphrase persa = dati persi (no recovery server-side).
- **Mai** `DateTime.UtcNow`/`Now` diretti. Tutto via `IClock`/`TimeProvider`.
- `TimestampInterceptor` popola automaticamente `CreatedAt`, `UpdatedAt`, `UpdatedAtMs`, e `CreatedByPluginId` (sulle 4 entità con provenance v2.3) leggendo `IPluginContext.PluginId`.
- Global query filter soft-delete su `HabitNode`: `HasQueryFilter(n => n.DeletedAt == null)`. Per Trash/diagnostica usare `IgnoreQueryFilters()`.

---

## 5. Modello dominio (essenziale)

Tutto è un `HabitNode` — sia habit sia gruppi — con albero ricorsivo via `parent_id`. Tipo nodo:

- **Habit**: ha `habit_type` ∈ {`action`, `counter`, `resistance`, `journal`, `state`}, completion, config, streak, goal, notifiche.
- **Group**: aggrega figli, può avere un proprio Goal, non è completabile direttamente.

Modalità **session** = `timer_mode='session'` su counter, con segmenti nominati (Work/Rest…). Non è un sesto tipo.

**Cascata reset_time** (boundary giornaliero della streak): habit.reset_time → ancestor groups.reset_time → `AppSetting.daily_reset_time` (default `00:00`). Risoluzione stateless via `IResetTimeResolver.GetEffectiveResetTime(habitNodeId)` (Milestone 3.3+). `reset_time` non è versionato in `HabitConfigHistory`.

**HabitConfig versioning**: ogni modifica a `HabitConfig` snapshot in `HabitConfigHistory` (mirror di tutte le colonne configurabili **eccetto** `id`, `habit_node_id`, `created_at`, `updated_at`, `updated_at_ms`, `quick_complete`, `ai_data_sharing`, `reset_time`).

**Scale**: `MetricDefinition.labels_json` (override locale) vince su `ScaleTemplate.labels_json` (template condiviso) — specificity stile CSS. Il valore memorizzato è sempre l'integer.

Dettagli su scheduling, streak modes, recovery, goals, notifications: vedi `Recordum_ProductSpec_v2.docx`.

---

## 6. Schema — invarianti

> **Sorgente autoritativa**: `Recordum_DatabaseSchema_v2_2.docx` per dettagli colonna, `docs/superpowers/specs/2026-05-13-plugin-system-design.md` §2.4 per il delta v2.3 provenance.

**Tipi SQLite**: boolean = INTEGER 0/1; date/timestamp human-readable = TEXT ISO 8601; sync key high-precision = INTEGER Unix ms; enum = TEXT + CHECK; JSON blob = TEXT (`schedule_days`, `labels_json`, `layout_json`, `payload_json`, `filter_json`).

**Convenzione timestamp**:
- `created_at` / `updated_at` / `valid_from` / `valid_until` / `generated_at` / `last_sync_at` → TEXT ISO 8601.
- `updated_at_ms` / `completed_at_ms` → INTEGER Unix ms per last-write-wins sync. **`Note` ha solo `updated_at_ms`, NON `updated_at`** (intenzionale).

**Provenance v2.3**: `created_by_plugin_id TEXT NULL` (FK soft, no constraint) su `habit_node`, `habit_template`, `export_template`, `chart`. NULL = creato da utente/core. Child events (Completion, Note, Goal) NON hanno la colonna — ereditano dal parent.

**PinScope v2.4**: `habit_node.pin_scope TEXT NOT NULL DEFAULT 'None'` con check `IN ('None','Local','Global')` + indice. Sostituisce il vecchio bool `is_pinned`. Migration `PinScopeV24` converte `is_pinned=1` → `'Local'`. AppSetting `schema_version` bumpato a `2.4`.

**ActiveTimer v2.5**: tabella `active_timer` con UNIQUE su `habit_node_id` (un solo timer attivo per habit), CASCADE on delete dal nodo, CHECK `timer_mode IN ('Stopwatch','Countdown','Primary','Session')`. Colonne `started_at_ms INTEGER`, `paused_at_ms INTEGER NULL`, `accumulated_paused_ms INTEGER DEFAULT 0`, `segment_index INTEGER NULL` (solo Session). Migration `ActiveTimerV25` bumpa `schema_version` a `2.5`. La tabella è il pivot del **timer recovery** post-crash: `TimerStatusBarViewModel.LoadAsync` carica `IActiveTimerService.ListActiveAsync` allo startup e ricostruisce le righe della status bar.

**StreakBrokenAt v2.6**: `habit_config.streak_broken_at TEXT NULL` per supportare `StreakRecovery.Manual`. **Non versionata** in `HabitConfigHistory` (è uno stato puntuale, non un setting); aggiunta a `HabitConfigHistoryColumns.Excluded`. Migration `StreakBrokenAtV26` bumpa `schema_version` a `2.6`.

**BackupLog v2.7**: tabella `backup_log` (`id` GUID PK, `created_at` TEXT, `kind` TEXT con CHECK `IN ('Manual','Auto','PreRestore')`, `path` TEXT, `size_bytes` INTEGER, `schema_version` TEXT, `app_version` TEXT, `notes` TEXT NULL) + indice `ix_backup_log_created_at` discendente. **Singola migration additiva** `BackupLogV27`: crea la tabella + bumpa `schema_version` a `2.7` + inserisce 5 nuovi AppSetting (`backup.auto.enabled`=true, `backup.auto.frequency_days`=7, `backup.auto.retention_count`=8, `backup.auto.last_run_utc`="", `backup.auto.path`=""). Conta totale AppSetting: 18.

**NoteCategory v2.11**: `notes.category TEXT NOT NULL DEFAULT 'Veloce'` (maxLength 16, CHECK `ck_note_category` `category IN ('Diario','Veloce')`) + `notes.completion_id TEXT NULL` (FK soft a una completion, indice `ix_notes_completion_id`). Migration additiva `NoteCategoryV211` bumpa `schema_version` a `2.11`. **Nota debito**: il seed `AppSettingSeed.schema_version` è fermo a `2.9` (bumpato solo via `UpdateData` nelle migration successive, non nel costante `HasData`/ModelSnapshot) — disallineamento **senza impatto runtime** (il restore-guard CR2 legge lo `schema_version` reale dal DB vivo); riallineamento tracciato come **CR8** (richiede `dotnet ef migrations add` per rigenerare lo snapshot).

**Soft delete**: solo su `HabitNode`. Cascade in transazione singola ai discendenti. Restore ripristina solo i discendenti chiusi nella stessa operazione. Purge permanente è job background per nodi >30 giorni (ordine: file → tabelle figlie → HabitNode).

**Naming**: tabelle e colonne snake_case (via `EFCore.NamingConventions`). Classi/proprietà C# PascalCase. Tutte le cascade rules e tutti gli indici **dichiarati esplicitamente** in Fluent API — mai default EF Core.

---

## 7. Roadmap

> **Roadmap rivista 2026-06-19** (brainstorm con l'autore): vedi `docs/superpowers/specs/2026-06-19-roadmap-revision-design.md`. Mobile anticipato sopra il web; Web/AI rimandati; sync di terzi confermato.

| Fase | Scope | Stato |
|---|---|---|
| 0–2 | Console + WPF prototyping | ✓ formativo (non importare codice in Fase 3) |
| 3 | WPF production: schema + UI + plugin foundation | ✓ **completata 2026-05-29** |
| **4** | **Stabilizzazione → Desktop v1.0: chiusura findings (priorità CR1–CR4 sicurezza/perdita-dati), chart funzionanti, export+plugin verificati, esplorazione dati di base, perf, UX gradevole. AI fuori.** | **→ corrente** |
| 5 | **Mobile (MAUI), standalone offline** — riusa Core/Data/Plugins invariati; dati locali; ponte dati via export/import `.recbak` | Pianificata |
| 6 | **Sync cloud via provider terzi** — "bring your own cloud" (Dropbox/Drive/OneDrive/iCloud), zero costi server; LWW già supportato (`updated_at_ms`) | Pianificata |
| Future | Plugin runtime WebView2 + marketplace + widget + accessibilità completa → Web/PWA → AI insight+correlazioni → (molto futuro, richiede risorse) sync con server proprietario | Rimandate, senza numero fisso finché la Fase 6 non è fatta |

**Principi roadmap**: **v1.0 desktop funzionante prima di espandere la piattaforma** (freeze fasi deciso 2026-06-16). Mobile prima del web. Schema finale dalla Fase 3 — nessuna migrazione invasiva successiva (mobile/sync riusano lo schema). Ogni fase = prodotto funzionante. Nessuna UI placeholder.

### Milestone Fase 3

| # | Obiettivo | Stato |
|---|---|---|
| **3.1** | Scaffold soluzione + schema completo + InitialSchema migration + plugin foundation (contracts + in-process host + manifest schema + plugin-api-ts skeleton + docs/plugin-api.md) | ✓ **completata 2026-05-14** |
| 3.2 | Core Data Layer: CRUD HabitNode/Config/Completion, soft delete + restore, HabitConfigHistory snapshot. SQLCipher via `IDbKeyProvider` (DPAPI baseline + opt-in passphrase Argon2id). Perf budget home <500ms | ✓ **completata 2026-05-19** |
| 3.3 | Home Screen: tab nav, albero habit, sort/filter, hide-completed, context menu | ✓ **completata 2026-05-20** |
| 3.4 | Habit Editor: progressive disclosure 3 livelli | ✓ **completata 2026-05-21** |
| 3.5 | Completion Recording: 5 tipi + session mode + multi-serie + timer recovery + retroactive | ✓ **completata 2026-05-21** |
| 3.6 | Journal Editor + Notes (WebView2 + Toast UI) | ✓ **completata 2026-05-22** |
| 3.7 | Habit Detail Screen: heatmap, streak history, chart, distribuzione oraria, goal progress | ✓ **completata 2026-05-22** |
| 3.8 | Streaks Engine: tutte le combinazioni schedule_type × streak_mode × streak_recovery × pause + HabitConfigHistory | ✓ **completata 2026-05-25** |
| **3.9** | Custom Charts (LiveCharts2). **Chart Engine come core plugin pilota** consumer di `chart.renderer` | ✓ **completata 2026-05-27** |
| **3.10a** | Export System (PDF/HTML/Markdown/JSON/CSV). **Export Engine come secondo core plugin** consumer di `export.format` | ✓ **completata 2026-05-28** |
| **3.10b** | Backup/restore `.recbak` (Argon2id + AES-256-GCM) + auto-backup scheduler + backup history | ✓ **completata 2026-05-29** |

### Fase 4 — Stabilizzazione → Desktop v1.0 (corrente)

**Branch**: `fase-4-polish` (creato 2026-05-29 da `milestone-3.10b-backup-restore`).

**Decisione (freeze 2026-06-16)**: nessuna nuova fase finché il desktop non è una **v1.0 funzionante e gradevole**. Asticella v1.0 = *core solido + UX gradevole + esplorazione dati funzionante* (vedi spec `docs/superpowers/specs/2026-06-19-roadmap-revision-design.md`). **Niente nuove feature.**

**Scope v1.0** (exit criteria): chiudere i findings del tracker, **priorità CR1–CR4** (perdita-dati/sicurezza backup-restore); **far funzionare i chart** (oggi rotti); **verificare+fixare export e plugin**; esplorazione dati di base (heatmap/statistiche); perf (<500ms home); UX/theming "gradevole". Aree core verificate a schermo dall'autore; `/security-review` superata sui findings di sicurezza. **Fuori scope**: AI/correlazioni, audit accessibilità completo (→ 1.x).

**Bug + rifinitura aperti** — tracker triagato in [`docs/phase-4-issues.md`](docs/phase-4-issues.md). Stati: `reported / verified / triaged / in-progress / fixed / verified-fixed`. Code review 2026-06-19: findings **CR1–CR24** (4 alti su sicurezza/perdita-dati). **Stato al 2026-06-24: 22/24 `fixed`** (compile-verified su Mac; i test WPF/Data girano sulla VM) — **CR6 `wontfix`** (reclass resistance, interpretazione dubbia + zero dati first-party), **CR8 `triaged`→VM** (riallineare seed `schema_version` a 2.11 via `dotnet ef`). Backlog del code review praticamente chiuso.

**Progresso 2026-05-29**: Theming Pass 1→3 (BaseStyles implicit style + chrome flat Button/TabItem/ComboBox + CheckBox flat + DatePicker/Calendar tematizzati); crash tab Dettaglio risolto; WebView2 probe sul Dispatcher UI (editor markdown ripristinato); refresh Home dopo mutazioni riga (B4) e dopo ogni completamento (B5); instanziazione template materializza metriche/scala/goal/segmenti (B6a); **costruita l'intera UI di personalizzazione dell'editor** — 10 section view + host Expander (B6c); i18n editor+picker (B9); day-picker schedule + chip Tag selezionate. Suite WPF 335 / Data 296 verdi. **Aperti**: B6b template custom (richiede design), rifiniture minori (picker scala, scala State, "+" State), nav-model a colonne toggle (spec parcheggiato). Tracker autoritativo `docs/phase-4-issues.md`; spec/piani in `docs/superpowers/`.

**Progresso 2026-06-23** (sessione da macOS — vincoli build/test e checklist di verifica VM in [`docs/dev-environment-sync.md`](docs/dev-environment-sync.md) §"Log sessione 2026-06-23"): **CR1–CR4 chiusi** (commit `24c0e60`) — restore atomico + rollback, guard `schema_version` reale (major.minor), bound Argon2id + cap anti zip-bomb, shred dei temp in chiaro. **Appearance pass** (commit `3497021`): editor diario tematizzato (segue tema **e** font a caldo via tripwire) + diario in serif; input minimal **underline** esteso a `PasswordBox` e preview note; **font UI Palatino** con **switch utente in Impostazioni → Aspetto** (nuovo `AppFontService`, mirror di `ThemeService`; AppSetting `ui.font`, default Serif). Tutto **compila** su Mac (`-p:EnableWindowsTargeting=true`) ma WPF/SQLCipher **non girano** lì → suite test + restore-corrotto + verifica visiva **da fare sulla VM**. Dettaglio: `docs/phase-4-issues.md` §"Stato al 2026-06-23".

**Progresso 2026-06-23 (continuazione, da macOS)** — chiusi altri findings del code review. **Data layer** (commit `fe6cb9d`, `37a2fd5`): CR5 (State valore-0 nel calendario, Data+WPF), CR7 (materializzazione template a singola transazione), CR9 ("Goal" hardcoded → eredita nome habit), CR10 (validazione Move condivisa), CR11 (length streak astinenza ancorato a createdDate; toccava anche l'export). **WPF** (compile-only su Mac, run+verifica VM): CR13 (durate visibili in Cronologia), CR20 (`IClock` in BackupDialog), CR22 (year-heatmap a 54 settimane → 31 dic non sparisce). **WONTFIX**: CR6 (riclass resistance — interpretazione dubbia, zero dati first-party). **CR8** rimandato alla VM (regen ModelSnapshot via `dotnet ef`). **Scoperto e sistemato**: il progetto `Recordum.WPF.Tests` non compilava dalla `3497021` (costruttore `SettingsViewModel` con nuovo `IAppFontService`, 2 test non aggiornati) → `FakeAppFontService` + 4 siti. Test Data verdi su `osx-arm64` (suite 339; 17 fail = solo DPAPI/SQLCipher Windows-only); WPF compila. Dettaglio: `docs/phase-4-issues.md` §"Continuazione 2026-06-23". **UX** (anteprime browser approvate, resa da verificare sulla VM): restyle dei **menù tasto-destro** (`ContextMenu`/`MenuItem` in `BaseStyles.xaml`) — da default Aero a pannello flottante arrotondato + ombra, niente colonna-icone, hover accento 14%, submenu con chevron (`docs/context-menu-preview.html`); **Slider** della scala State (era thumb Aero → traccia flat + fill accento + thumb circolare, `docs/slider-preview.html`); **simbolo del programma** nella title bar (era icona Lucide "activity" a linea → **R serif Palatino** nel colore testo, `MainWindow.xaml`, `docs/brand-mark-preview.html`); **dropdown ComboBox** allineato alla famiglia popup dei menù (ombra + raggio 8 + hover accento, `docs/combobox-preview.html`); **scorrimento colonne workspace** ripristinato come **ibrido fill-then-scroll** (regressione da `f08fba9` star-split; ora `HabitScreen.xaml` avvolge la griglia in uno ScrollViewer orizzontale con `Grid.Width=ViewportWidth` + `Grid.MinWidth=OpenColumnsMinWidth` = colonne×320 → riempie se c'è spazio, scrolla se sono troppe, mai sotto 320, `docs/column-scroll-preview.html`); **ToolTip** allineato alla famiglia popup (era rettangolo squadrato Aero → raggio 6 + ombra morbida, `docs/tooltip-preview.html`); **divisori colonne** (`GridSplitter`) da barra grigia 4px a **sash** (hairline a riposo, barra accento su hover/drag, stile `HabitScreen.ColumnSplitter`, `docs/column-splitter-preview.html`); **empty-state** (`EmptyState.xaml` Home + welcome `WorkspaceView`) da singola riga grigia a **icona line + titolo** centrati, icona per-tab (Sprout/Moon/Sparkles/BookOpen, `docs/empty-state-preview.html`); **scrollbar** (già flat dalla Fase C) con **stato hover/drag** (scurisce+allarga) e **min-size** del thumb (`docs/scrollbar-preview.html`); **TabStrip workspace** (`WorkspaceView.xaml`) con angoli tab arrotondati + chiusura come icona X Lucide (era "✕"), underline accento e pin/preview invariati (`docs/tabstrip-preview.html`).

**Color Picker v2** (feature, brainstorm→spec→plan→subagent-driven, commit `505cdc9`→`e953e89`): overhaul di `ColorSwatchPicker` (ereditato da editor/builder/note) — polish famiglia flottante + hex "#", **anello di selezione** (F1), **ramp tonale** auto (`ShadeRamp.For`, F2), **preferiti salvabili** (`IFavoriteColorStore`, AppSetting `ui.favorite_colors` cap 12, F4), **contagocce schermo** (`IScreenColorPicker` Win32 isolato, VM-only, F3). Spec/piano in `docs/superpowers/`, mockup `docs/color-picker-preview.html`. **Nota build**: una nuova stringa = 3 edit (`Strings.resx`+`Strings.it.resx`+proprietà manuale in `Strings.Designer.cs`); nessun ResX code-gen.

**Progresso 2026-06-24 (da macOS)** — chiusura del backlog del code review (CR rimasti) + una rifinitura UX. **Robustezza/integrità** (commit `90c92c4`/`cc85c4d`/`d0e1e93`): CR14 (generation guard in `HabitEditorViewModel.LoadAsync` — cambi rapidi di habit non interlacciano più i load), CR15 (gli 8 handler `async void` in MainWindow/HabitScreen ora in try/catch+trace — niente crash su errore transitorio), CR16 (drag-drop tollera nodo/parent sparito da reload concorrente). **Guard/perf** (commit `885942e`/`68f3c89`/`6e760d1`): CR17 (zona-drop dalla card via `FindRowCard`), CR18 (generation guard su `HomeViewModel` reload), CR19 (`CancellationTokenSource` per-reload in `HabitRecordsViewModel`; VM ora `IDisposable`), CR24 (`LoadMarkersAsync` parallelizzato con `Task.WhenAll`), CR23 (nuovo `DebouncedColorRecorder` — il MRU colori cattura solo il colore assestato, non ogni tasto). **Convenzioni/reuse** (commit `04522ea`): CR21 (errori QuickAdd localizzati via `SetUnexpectedError` + nuova `SeriesQuickAddViewModelBase` che deduplica `ToggleChrono`). **Cosmetico** (commit `60ead30`): CR12 (`CurrentLength` richiede `BreakReason is null` → una ricaduta in giornata non lascia il numero stale). **UX**: CTA **"Crea la prima abitudine"** nell'empty-state Attive (`EmptyState.xaml` → `OpenNewHabitCommand`; nascosta su Archiviate/Cestino) + corretta la copy datata `Home.Empty.Active` (diceva ancora "milestone 3.4"). Ogni fix WPF ha un test (compile-only su Mac, run VM). Tutto compila (entrambi i progetti); **da fare sulla VM**: suite test completa Data+WPF + verifiche visive accumulate + CR8. Dettaglio: `docs/phase-4-issues.md`, handoff VM in `docs/dev-environment-sync.md`.

### Storico stati milestone

Spostato in [`docs/MILESTONES.md`](docs/MILESTONES.md). Quel file è il companion "a scalare" — consultalo quando devi ricostruire decisioni passate, deviazioni dai plan, schema delta dettagliati, o known quirks per milestone (3.1 → 3.10b + avanzamento Fase 4 giorno per giorno). Le memorie utente `recordum-milestone-3-x-spec` restano l'altra fonte sintetica.

---

## 8. Convenzioni di codice

- **MVVM** in `Recordum.WPF` con `CommunityToolkit.Mvvm` (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`).
- **DI** ovunque via `Microsoft.Extensions.DependencyInjection`. `IClock`, `IPluginContext` iniettati. Mai accesso statico al tempo.
- **Stringhe utente** in `.resx` (English-first, italiano secondario). Niente hardcoded.
- **Cascade rules + indici** sempre in Fluent API esplicita, mai default EF.
- **Provenance**: nuove entità top-level user-facing (HabitNode-like, template-like, chart-like) → aggiungere `created_by_plugin_id TEXT NULL`. Mai su entità child (ereditano dal parent).
- **Plugin context**: service Core che crea entità per conto di un plugin deve farlo dentro uno scope con `IPluginContext.PluginId` valorizzato. Codice user-driven o core lascia `PluginId = null`. First-party features (Chart Engine, export…) registrano capability via `IExtensionPoint` — niente shortcut che bypassano il routing.
- **Soft delete**: usa il filtro globale; per Trash/diagnostica `IgnoreQueryFilters()`.
- **Test**: per Fase 3 servono test su soft-delete cascade/restore, snapshot HabitConfigHistory, `StreakEngine` (tutte le combinazioni schedule × streak × recovery + pause + cambi config + reset_time cascade), export su dataset grandi.
- **Sicurezza pre-merge**: prima di mergiare un branch `fase-*` → `main`, eseguire `/security-review` e chiudere i findings ad alta priorità (l'app spedisce pubblicamente, con DB cifrato). Backlog sicurezza corrente: CR1–CR4 in `docs/phase-4-issues.md`.
- **Guardrail hook** (`.claude/settings.json` + `.claude/hooks/*.js`, team-wide): commit/push su `main` bloccato; edit a mano dei file EF generati (`*.Designer.cs`/`*ModelSnapshot.cs`) bloccato; delete di dati utente (`%LocalAppData%\Recordum`/`.recbak`) chiede conferma; promemoria a fine turno se il working tree è sporco. Gestibili con `/hooks`.
- **Ambiente Claude Code multi-macchina** (VM Windows primaria + Mac secondario): cosa viaggia via git vs cosa è machine-local (plugin, memorie, `settings.local.json`), report stato VM e checklist di allineamento in [`docs/dev-environment-sync.md`](docs/dev-environment-sync.md). Invariante: il plugin **`superpowers`** (pin v6.0.3) va installato per-macchina — tutto il flusso `docs/superpowers/*` ne dipende.

### Migrazioni

```bash
dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF
dotnet ef database update    --project src/Recordum.Data --startup-project src/Recordum.WPF
```

Verifica il SQL generato contro la documentazione schema **prima** di eseguire l'update.

### Build

Il RID è vincolato a **`win-x64`** (`SelfContained=false`) in `Directory.Build.props`: ogni build emette solo i nativi Windows invece di SkiaSharp/sqlcipher/QuestPDF per ~8 RID (cartella di lavoro ~530 MB invece di ~2.6 GB). Fase 5 (mobile) / sync useranno i propri progetti che ri-risolvono i RID che servono. Nota: `NU1903` (CVE-2025-6965 su `e_sqlite3 ≤ 2.1.11`, **nessun fix upstream**) è declassato a warning visibile — rimuovere il declass quando SQLitePCLRaw spedirà SQLite ≥ 3.50.2. Vedi memoria `recordum-build-rid-and-cve`.

---

## 9. Linguaggio visivo (riferimento UI)

> Dettaglio completo in `Recordum_Prototype_DesignBrief.md`.

- **Tono**: serio ma caldo. Riferimenti: Linear, Notion, Things 3, Readwise. **Non** Material/iOS candy.
- **Typography-forward**, whitespace generoso. Sans clean (Inter o system stack).
- **Colore = identificatore**, mai decorazione. 4px accent bar a sinistra di una riga habit; heatmap ramp dominante. Niente altrove.
- **Light + dark theme rifiniti**.
- **Icone Lucide** o equivalenti line-icon. **Mai emoji nella chrome UI** (permesse solo in contenuti scritti dall'utente).
- **Heatmap stile GitHub**, ramp a 5 step.
- **Motion restrained**: nessun bounce/parallax/jiggle.
- **No gamification, no frasi motivazionali, no controlli desktop reinventati.**

**Progressive disclosure Habit Editor**: Level 1 (sempre visibile) = Nome, type selector, color, icon. Level 2 (sezioni collassabili, default collassate) = Scheduling, Streak, Metrics, Timer, Scales, Data source, Goal, Notifications, AI sharing. Level 3 (toggle "Advanced" dentro sezione) = opzioni rare (`streak_tolerance_minutes`, `timer_auto_prompt`, `rest_timer_seconds`).

---

## 10. Documenti sorgente

| File | Uso |
|---|---|
| `Recordum_ProductSpec_v2.docx` | Comportamento prodotto, tipi habit, scheduling, streak, note, goal, export, casi d'uso. |
| `Recordum_DatabaseSchema_v2_1.docx` | Schema tabella-per-tabella autoritativo per i dettagli colonna. |
| `Recordum_DatabaseSchema_v2_2.docx` | Emendamenti a v2.1: cascade rules, indici, reset cascade, AppSetting registry, timestamp conventions. **Vince su v2.1.** |
| `docs/superpowers/specs/2026-05-13-plugin-system-design.md` | Spec plugin system. Autoritativo per: scope Fase 3, delta schema v2.3 provenance (§2.4), AI filter (§4.6), SQLCipher (§11), backup/restore plugin state (§7.9), supply chain (§3.5), perf budget concurrent (§6.6). |
| `Recordum_Roadmap_v1.docx` | Struttura fasi e milestone. Fase 3 milestone aggiornate in §7 di questo file + spec plugin §8. |
| `Recordum_Prototype_DesignBrief.md` | Brief UX/visuale. |

### Precedenza in caso di conflitto

1. Spec plugin §2.4 + §11 (provenance, encryption)
2. DatabaseSchema v2.2
3. DatabaseSchema v2.1
4. ProductSpec v2
5. Roadmap v1
6. Questo CLAUDE.md (riassunto)

---

## 11. Decisioni di design

In caso di dubbio:
- **Privacy > convenienza.**
- **Composizione > opzioni in più.**
- **Profondità nascosta > schermata semplificata che nasconde feature.**
- **Esplicito > implicito** (specialmente cascade rules e EF mapping).
- **English-first** nelle stringhe utente; italiano OK nei commenti/conversazioni interne.

Recordum spedisce. Tutto il codice da Fase 3 in poi è codice di produzione.
