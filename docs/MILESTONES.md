# Recordum — Storico stati milestone

> **Quando consultare.** Riferimento storico per le milestone Fase 3 e l'avanzamento Fase 4 (in corso). `CLAUDE.md` resta il file principale auto-caricato; questo file è il companion "a scalare" per ricostruire decisioni passate, deviazioni dai plan, schema delta dettagliati, e known quirks per milestone.
>
> Ogni sezione "Stato Milestone X" è il digest scritto a chiusura, autoritativo per la struttura del codice consegnato. Per il dettaglio task-by-task vedi `docs/superpowers/plans/2026-MM-DD-milestone-X.Y-*.md`. Per gli script mentali aggiornati (commit messages incluse le deviazioni) vedi `git log`. Per i quirks più rilevanti vedi le memorie utente `recordum-milestone-X-Y-spec`.

---

## Fase 3 — milestone

### Stato Milestone 3.1

49 task del plan `docs/superpowers/archive/plans/2026-05-14-milestone-3.1-foundation.md` completati. 95 test verdi (Core 18, Data 32, Plugins 41, WPF 4 smoke). Build pulito, zero warning. Stack consegnato: 5 progetti src + 4 test, 25 entity + Fluent configs + indexes + cascade rules + soft-delete filter + TimestampInterceptor, InitialSchema migration + seed (14 UoM, 7 ScaleTemplate, 8 HabitTemplate, 4 ExportTemplate, 13 AppSetting con `schema_version=2.3`), plugin contracts (9 extension points, 12 events, manifest record), plugin host (broker/registry/event bus/api router/in-process host/manager), WPF bootstrap (ServiceRegistrar + ApplicationInitializer + .resx), `tools/plugin-api-ts/` (manifest.schema.json + index.d.ts), `docs/plugin-api.md`.

### Stato Milestone 3.2

36 task del plan `docs/superpowers/archive/plans/2026-05-18-milestone-3.2-core-data-layer.md` completati. 167 test verdi (Core 24, Plugins 41, Data 97, WPF 5 smoke incl. E2E encrypted bootstrap). Build pulito, zero warning. Data layer + SQLCipher consegnati: 14 service aggregate + query + 1 key transition (Recordum.Data.Services / .Queries), `HabitConfigHistoryInterceptor` registrato prima del `TimestampInterceptor`, `IHabitTreeQueryService.GetHomeTreeAsync` con perf budget p95 < 300 ms su dataset seedato (200 habit × 50k completion). SQLCipher attivo via `SQLitePCLRaw.bundle_e_sqlcipher`; `DpapiKeyProvider` di default + `PassphraseKeyProvider` (Argon2id 64 MiB / 4 iter / p=2) opt-in; `IKeyTransitionService` headless DPAPI↔passphrase con backup-and-rollback. Nuovo indice composito `(habit_node_id, completed_at_ms)` su `completions` (migration `20260519131117_CompletionCompletedAtMsIndex`). SQLCipher overhead empirico ~4× plain su workload SELECT 50k (assoluto < 30 ms/call): target 30% del plan irrealistico per AES-CBC su read-heavy; soglia di regressione fissata a 5×. `RecordumOptions` ora implementa `IDbStoragePaths` e ospita `DatabasePath` + `KeyFilePath` + `SaltFilePath`. Nessuna UI Settings per la transition: arriva in 3.3+.

### Stato Milestone 3.3

33 task del plan `docs/superpowers/archive/plans/2026-05-19-milestone-3.3-home-screen.md` completati. 243 test verdi (Core 30, Plugins 41, Data 126 incl. perf budget, WPF 46 incl. E2E `MainWindowViewModel` smoke). Build pulito, zero warning. Home Screen consegnato: 4 ViewModel (`HomeViewModel`, `HomeRowViewModel`, `SettingsViewModel`, `MainWindowViewModel`) con commands tab-aware e filtri client-side (type/tag/hide-completed/search/sort + pin layering Global > Local > None); 6 View XAML (`MainWindow`, `HomeView`, `HabitRowTemplate`, `GroupRowTemplate`, `HeatmapStrip`, `EmptyState`, `SettingsPopup`); 3 Behavior (`InlineRenameBehavior`, `TreeDragDropBehavior` con 3-band drop zone + `CyclicHabitNodeMoveException` guard, `ThemeService` registry watcher singleton); 7 converter (`HexToBrush`, `HabitTypeLabel`, `BoolToVisible`, `BoolToHidden`, `EnumToBool`, `StateToVisible`, `ActiveTabVisible`). Schema delta v2.4 (`PinScope` enum + migration). `HomeTreeRow` esteso a `PinScope`/`ChildCount`/`IsArchived`/`CompletionsLast7Days[7]`/`TagIds`. `HabitTreeQueryService.GetTrashTreeAsync` separato. Theming System/Light/Dark con switch live via `Resources/MergedDictionaries[1]` swap. Localization en + it tramite `Strings.resx` + designer auto-generato. Scorciatoie tastiera: Ctrl+1/2/3 tab, Ctrl+F focus search, F2 rename, Del soft-delete, Ctrl+E toggle archive, Ctrl+R restore. **Known limits**: drag-drop persiste solo `ParentId` (no `SortOrder` reorder in 3.3); heatmap intensity step binario (proporzionale a `TargetValue` → M3.7).

### Stato Milestone 3.4

30 task del plan `docs/superpowers/archive/plans/2026-05-20-milestone-3.4-habit-editor.md` completati. 341 test verdi (Core 30, Plugins 41, Data 139, WPF 131 incl. E2E template→edit smoke). Build pulito, zero warning. Habit Editor consegnato: pane laterale destro `EditorPane` aperto su `Home.SelectedRow` (`MainWindowViewModel` routing); `HabitEditorViewModel` ospita L1 header (Name/Description/Color/Icon + Node/Habit type combos) + 11 sezioni VM (`Scheduling`, `Streak`, `Timer`, `Metrics`, `Scales`, `Goal`, `Notifications`, `Tags`, `AiSharing`, `DataSource`, `ResetTime`) — ciascuna estende `SectionViewModelBase` con autosave per-campo (`ApplyAsync` → Saving→Saved|Error). Visibilità per tipo via `IsApplicable(NodeType, HabitType?)`, coperta dal matrix test (spec §6). Service extensions: `IHabitTemplateService.InstantiateAsync` (clona seed in nuovo habit + config), `ITagService.SetTagsAsync` (replace idempotente), `IHabitConfigService.ListSegmentsAsync/UpdateSegmentAsync`, `IGoalService.GetActiveAsync/UpsertActiveAsync`. Create flow: `HabitTemplatePicker` modal (8 seed templates + Blank habit/group) lanciato da `+ New` button su Home via `IHabitTemplatePickerLauncher` (DI-injectable per test). Footer pane aggrega status (Error > Saving > Saved > Idle). Pane width persistito in `AppSetting "ui.editor_pane_width"` (default 480, clamp 320..900) — push back via `GridSplitter.DragCompleted` → `MainWindowViewModel.CommitEditorPaneWidthAsync`. Tutte le stringhe utente in `Strings.resx` + `Strings.it.resx` (en/it). **Known limits**: `DataSourceSection` backed by `AppSetting` placeholder (revisit a M3.6 dopo decisione schema); reorder segmenti/metrics non implementato (drag-drop intra-section deferred).

### Stato Milestone 3.5

40 task del plan `docs/superpowers/archive/plans/2026-05-21-milestone-3.5-completion-recording.md` completati. 452 test verdi (Core 31, Plugins 41, Data 167, WPF 213 incl. E2E smoke quick-action + timer recovery). Build pulito, zero warning. Completion Recording consegnato.

**Schema delta v2.5**: `active_timer` (UNIQUE habit, CASCADE, CHECK mode) + migration `ActiveTimerV25` (bump schema_version 2.4→2.5). `Recordum.Data.csproj` ora referenzia `Recordum.Plugins.Contracts` per i payload `CompletionRecorded/Updated/DeletedPayload` (collocati in `Plugins.Contracts/Events/` invece che `Plugins/Events/` — deviazione dal plan: `Data` deve dipendere solo dal contratto, non dall'host runtime).

**Core/Data**:
- `ActiveTimer` model + `IActiveTimerService` (Start/Pause/Resume/Stop/NextSegment/Get/List). Pause idempotente, accumulated_paused subtratto in Stop. StartAsync con TimerMode.Session inizializza `SegmentIndex=0`. NextSegment vincolato a Session.
- `ICompletionService` fleshed-out: `ListByHabitAsync(habitNodeId, fromUtc, toUtc, take)`, `GetHeatmapAsync(habitNodeId, fromDate, toDate) → HeatmapCell[]`. Tutte le 3 ops mutanti (Record/Update/Delete) pubblicano via `IEventBus`. Update emette solo se la riga esisteva.
- `HabitConfigInterceptor` registration order invariato.

**WPF — Layout 5 colonne**: `MainWindow.xaml` ora `Home (*) | splitter | HabitScreen (user-width) | splitter | EditorPane (user-width)`. Code-behind toggla `GridLength` 0 / saved-width in base a `Home.SelectedRow` ed `Editor.HasNode`. Width persistite in AppSetting `ui.habit_screen_width` (default 520, clamp 360..800) e `ui.editor_pane_width`. **Rewire row-click**: `Home.SelectedRow` → `HabitScreen.LoadAsync` (NON più Editor). Editor si apre solo via `HabitScreen.OpenConfigure` (button "Configure" nell'header).

**WPF — HabitScreen**: VM host `HabitScreenViewModel` composta da `HeatmapViewModel` + `QuickAddViewModelBase` (polimorfica via DataTemplateSelector) + `TimerControlViewModel` + `CompletionListViewModel`. `IDisposable` per cleanup IEventBus subscriptions.

- **Heatmap**: 91 celle (7 rows × 13 cols) ending today, gap-filled con 0. 5-step ramp `0, 1, 2-3, 4-7, ≥8` → brushes `Brush.Heatmap.Level0..4` (Light+Dark). `ClickCell` emette `DateClicked` → `QuickAdd.CompletionDate` per backdate inline.
- **5 Quick-add VM**: Action (one-click), Counter (value + primary metric by SortOrder + notes), Resistance (multi-series `SeriesRowViewModel` + cells per metric, `SeriesIndex` 0..N), Journal (notes-only validate non-empty), State (`LabelsJson` → ScaleLabels combo, integer index salvato come ValueInteger). Tutte estendono `QuickAddViewModelBase` con `ResolveCompletionTimestamp()` (today = clock.UtcNow, retro = midnight UTC + IsRetroactive=true).
- **TimerControl**: Start/Pause/Resume/Stop/NextSegment. `SetContextAsync` carica HabitConfig + segments + active timer per pre-seed di IsActive/IsPaused/SegmentIndex (timer recovery). NextSegment auto-stop dopo l'ultimo. Visible solo se `HabitConfig.HasIntegratedTimer`.
- **CompletionList**: top 20 desc, inline edit (`BeginEdit/SaveEdit/CancelEdit`) e Delete con event emit. `IEventBus` sub re-fetch heatmap+list automatico su qualsiasi evento per il `Node.Id` corrente.

**WPF — TimerStatusBar**: barra always-visible al bottom di MainWindow. `TimerStatusBarViewModel` (Transient) + `ActiveTimerRowViewModel` per riga (Elapsed mm:ss / hh:mm:ss, Pause/Resume/Stop, clamp ≥0). `TimerRecoveryService.RecoverAsync` chiama `LoadAsync` allo startup → recupera timer post-crash. 1Hz `DispatcherTimer` in code-behind del view per refresh display.

**WPF — Home quick-action icon**: `IQuickActionHandler` + 5 implementazioni (Action/Counter recording inline; Resistance/Journal/State no-op che lasciano il click apertura della screen). `QuickActionHandlerSelector` lookup by HabitType. `HomeRowViewModel.QuickActionCommand` (CanExecute=!IsGroup && selector attached) + event `QuickActionExecuted`. Icona `+` 26×26 in `HabitRowTemplate.xaml` nuova colonna.

**Known quirks** — vedi memory `recordum-milestone-3-5-spec`.

### Stato Milestone 3.6

30 task del plan `docs/superpowers/archive/plans/2026-05-22-milestone-3.6-journal-editor.md` completati. ~491 test verdi (Core 31, Plugins 45, Data 175, WPF 240 incl. E2E Note CRUD smoke). Build pulito, zero warning nuovi. Journal Editor + Notes consegnato. **Schema delta: zero** — `note` e `attachment` esistono già da `InitialSchema` (M3.1); `schema_version` resta `2.5`.

**Plugin contracts**: `WellKnownEvents.NoteDeleted` + 3 record `NoteCreated/Updated/DeletedPayload` (collocati in `Plugins.Contracts/Events/`, stesso pattern dei `Completion*Payload` di M3.5).

**Core/Data**:
- `INoteService` flesh-out: `CreateAsync(Note, ct)` + overload `CreateAsync(Note, IReadOnlyList<Attachment>?, ct)` (preserva il supporto attachments di M3.2 — deviazione dal plan), `UpdateAsync` / `DeleteAsync` / `GetAsync` / `ListByHabitAsync`. `NoteService` `sealed partial` con `LoggerMessage` source-gen. Tutte le ops mutanti pubblicano via `IEventBus`; Update/Delete usano existence-check per evitare emit su id mancante.
- `ListByHabitAsync` ordina per `UpdatedAtMs DESC` + filtro per `HabitNodeId`.
- Cascade purge: `OnDelete(Restrict)` su FK Note→HabitNode (intenzionale, da M3.1) — il PurgeService di M3.2 rimuove le note esplicitamente dentro la transazione (deviazione dal plan, che asseriva cascade FK).

**WPF — Editor control**:
- `Microsoft.Web.WebView2` 1.0.* NuGet + vendored Toast UI Editor 3.2.2 MIT bundle (JS 534KB + CSS 165KB + `LICENSE-toast-ui.md`) sotto `Resources/EditorAssets/` (`CopyToOutputDirectory=PreserveNewest`).
- `IWebView2Availability` singleton + `WebView2Availability` impl: `ProbeAsync()` non-fatale (try/catch su `CoreWebView2Environment.CreateAsync()`); chiamato dopo migrations in `ApplicationInitializer`. Win10 senza Runtime → `IsAvailable=false`, fallback TextBox.
- `editor.html` + `editor.js`: init Toast UI in modalità wysiwyg con toolbar ridotta, JS bridge `window.chrome.webview.postMessage` per messaggi tipizzati `ready`/`change`/`setContent`/`setReadOnly`. Guardia `suppressNext` contro ping-pong.
- `MarkdownEditorControl` UserControl (xaml + xaml.cs): 4 DPs (`Markdown` TwoWay, `IsReadOnly`, `Placeholder`, `Availability`). `TryInitAsync` lazy: se `Availability.IsAvailable=true` → `EnsureCoreWebView2Async` + `SetVirtualHostNameToFolderMapping("recordum.local", AppContext.BaseDirectory + "Resources/EditorAssets", Allow)` + `Navigate("https://recordum.local/editor.html")`. Se false → `ActivateFallback()` mostra TextBox bound al DP. `Xunit.StaFact` aggiunto per `[WpfFact]` (DependencyObject ctor richiede STA).

**WPF — Notes tab**:
- `NoteRowViewModel`: `Summary` (prima riga non-blank, 40-char truncation + `…`, fallback `Strings.Notes_Untitled`), `UpdatedAtDisplay` (`yyyy-MM-dd HH:mm` UTC), `Refresh()` per re-emit dopo mutazione esterna.
- `NotesViewModel` (`sealed partial` + IDisposable): `ObservableCollection<NoteRowViewModel> Notes`, `[ObservableProperty]` per `SelectedNote`/`EditorMarkdown`/`IsConfirmingDelete`. Commands: `NewCommand` (inserisce in cima + auto-select), `SaveCommand` (CanExecute=`IsDirty`, persiste e refresh row), `RequestDelete/Confirm/CancelDelete` (idx-clamp next-row select). `OnSelectedNoteChanged` carica `BodyMd` nell'editor — eventuali edit non salvati vengono silenziosamente scartati (per spec §3.3, esplicit-Save è l'unico path). `IEventBus.Subscribe` su `NoteCreated/Updated/Deleted` — eventi cross-VM provocano `LoadAsync` con preservazione della selezione.
- `NotesTabView` (xaml + xaml.cs): master-detail 3 colonne (`ListBox 220px | GridSplitter | MarkdownEditorControl + DockPanel Save/Delete row`). `DataTemplate` mappato in `HabitScreen.Resources`.

**WPF — HabitScreen integration**:
- `HabitScreen.xaml` rifattorizzato: header + banner WebView2-missing + `TabControl` (Completion / Notes). Heatmap/QuickAdd/TimerControl/CompletionList ora vivono dentro il Completion `TabItem`. Notes `TabItem` punta a `Binding Notes` via DataTemplate.
- Journal QuickAdd: `TextBox` rimpiazzato da `MarkdownEditorControl` (`Height=280`, `Availability` risolto via `RelativeSource AncestorType=UserControl` → HabitScreen.DataContext).
- `HabitScreenViewModel`: 2 nuovi ctor param (`INoteService`, `IWebView2Availability`) + 2 proprietà (`Notes`, `Availability`). `LoadAsync` include `Notes.LoadAsync` nel `Task.WhenAll`; `Dispose` chain a `Notes.Dispose`.

**WPF — i18n (en + it)**: 13 nuove chiavi resx: `HabitScreen.Tab.Completion/Notes`, `Notes.New/Save/Delete/Confirm/Cancel/ConfirmDelete/Empty/EmptyHint/Untitled`, `Editor.WebView2.NotAvailable.Title/Body`. Designer field con underscore (`HabitScreen_Tab_Completion` etc.).

**Known quirks** — vedi memory `recordum-milestone-3-6-spec`.

### Stato Milestone 3.7

27 task del plan `docs/superpowers/archive/plans/2026-05-22-milestone-3.7-habit-detail-screen.md` completati. 540 test verdi (Core 38, Plugins 45, Data 191, WPF 266 incl. E2E `HabitScreenDetailSmokeTests`). Build pulito, 2 warning pre-esistenti, zero nuovi. **Schema delta: zero** — `schema_version` resta `2.5`.

**Core/Data**:
- `IClock.LocalTimeZone` aggiunta per testabilità bucketing locale (`SystemClock` ritorna `TimeZoneInfo.Local`, `FakeClock` default `TimeZoneInfo.Utc`).
- `IIntensityRampResolver` (Core) + `IntensityRampResolver`: ramp proportionale 25/50/75/100 sul per-day target di un `Goal`; Open passa-through, Rolling divide per `RollingDays`, Fixed normalizza sull'intero periodo. Fallback `1/3/7/8` se goal assente o target ≤ 0.
- `IStreakAggregator` (Core) + `NaiveDailyStreakAggregator` (Data): sequenze giornaliere da `ICompletionService.GetHeatmapAsync` (count-only, no schedule awareness). Sarà sostituito in M3.8 da `SchedulingAwareStreakAggregator` con stessa interfaccia.
- `ICompletionService.GetDailySumAsync(habit, metric, from, to)` + `DailyValue` DTO: somma per-giorno di `(ValueDecimal ?? ValueInteger ?? ValueSeconds)` filtrata per metric, base per heatmap proportional di counter/resistance/state.
- `ITrendQueryService` + `TrendQueryService`: bucketing Daily/Weekly. Action/Journal/no-primary → count; State → media `ValueInteger`; Counter/Resistance → sum `ValueDecimal??Integer??Seconds`. Weekly bucket via `((int)DayOfWeek+6)%7` (Mon=0).
- `IHourlyDistributionQueryService` + `HourlyDistributionQueryService`: 24-bucket per-ora locale via `IClock.LocalTimeZone`. Esclude `IsRetroactive=true`.

**WPF — Detail tab**: 6 ViewModel sotto `Recordum.WPF.ViewModels.HabitScreen.Detail/`:
- `DetailViewModel` orchestrator (IDisposable, sub IEventBus `Completion*`/`Note*`). Routes per `NodeType`: Group → solo `GoalProgressCard`; Habit → tutte le sezioni.
- `GoalProgressCardViewModel`: Open/Rolling/Fixed period; metric-aware Actual (sum su MetricDefinitionId) o fallback count.
- `YearHeatmapViewModel`: 53×7 = 371 celle, anchor = Monday di today-week meno `52*7` giorni. `HeatmapCellViewModel.IsOutOfRange` per pad cells (giorni futuri della settimana corrente). Usa `GetDailySumAsync` per counter/resistance/state con primary metric, `GetHeatmapAsync` altrimenti.
- `StreakSummaryViewModel`: current/longest/runs90. Current ≠ 0 solo se ultimo run termina oggi o ieri.
- `StreakTimelineViewModel`: window 90gg.
- `TrendChartViewModel`: 30d/90d/1y range, `TrendChartKind.Line` (Counter/Resistance/State) o `Bar` (Action/Journal). `RangeChanged` event → `DetailViewModel.OnTrendRangeChanged` → `HourlyHistogramViewModel.SetRangeAsync` (range condiviso).
- `HourlyHistogramViewModel`: 24-bucket, range condiviso con Trend.

**WPF — Controls** (4 nuovi sotto `Recordum.WPF.Controls/`, pure WPF Canvas/Shape):
- `YearHeatmapControl` (UniformGrid 53×7 + `IntensityStepToBrush`).
- `StreakTimelineControl` (Canvas rect bars con tooltip date range).
- `TrendChartControl` (Polyline+Polygon area per Line, Rectangle bars per Bar; empty-state localizzato).
- `HourlyHistogramControl` (Canvas 24 rect).

**HeatmapViewModel (Completion tab)** ora consuma `IIntensityRampResolver` + `IGoalService` + `IHabitConfigService`. Quando esiste un goal applica la rampa proporzionale; senza goal mantiene il fallback step (M3.3 known-limit ripagato). `HeatmapCellViewModel` accetta `int? overrideIntensityStep` e flag `IsOutOfRange` (default false).

**Wiring**:
- `HabitScreenViewModel` ctor +5 dipendenze (`IGoalService`, `IIntensityRampResolver`, `IStreakAggregator`, `ITrendQueryService`, `IHourlyDistributionQueryService`). Espone `Detail` property, `LoadAsync` include `Detail.LoadAsync` nel `Task.WhenAll`, `Dispose` chain a `Detail.Dispose`.
- `HabitScreen.xaml`: 3a tab "Detail" tra Completion e Notes con `DataTemplate` inline (ScrollViewer + 5 Border sections); `IsHabit` converter nasconde le sezioni habit-only per i gruppi.
- `ServiceRegistrar`: +4 entry (`IIntensityRampResolver` singleton, `IStreakAggregator`/`ITrendQueryService`/`IHourlyDistributionQueryService` scoped).

**i18n**: 21 nuove chiavi (`HabitScreen.Tab.Detail`, `Detail.GoalCard.*`, `Detail.Streak.*`, `Detail.Trend.Range.*`, `Detail.Trend.Empty`, `Detail.Hourly.*`, `Detail.BasicStreakNotice`) in `Strings.resx` + `Strings.it.resx` + designer manuale.

**Known quirks** — vedi memory `recordum-milestone-3-7-spec`.

### Stato Milestone 3.8

26 task del plan `docs/superpowers/archive/plans/2026-05-24-milestone-3.8-streaks-engine.md` completati. 613 test verdi (Core 40, Plugins 45, Data 261, WPF 267 incl. E2E `Detail_with_scheduling_aware_aggregator_reports_streak_length` + perf trait `OneYearHistory_completes_within_budget`). Build pulito, zero warning nuovi.

**Schema delta v2.6**: `habit_config.streak_broken_at TEXT NULL` + migration `StreakBrokenAtV26` (bump `schema_version` 2.5 → 2.6). Esclusa da `HabitConfigHistory` (stato puntuale, non setting).

**Engine consegnato**:
- `SchedulingAwareStreakAggregator` sostituisce `NaiveDailyStreakAggregator` (firma `IStreakAggregator` invariata, M3.7 ViewModels intoccati).
- Per ogni giorno: pause-check → `IScheduleEvaluator` (6 ScheduleType) → `IStreakModeEvaluator` (Daily/Weekly/Occurrences/Both, keyed) → `IStreakRecoveryPolicy` (None/GraceDay/Manual/Retroactive, keyed).
- `IResetTimeResolver` introdotto (paga il debito di M3.3): cammina parent chain → AppSetting `daily_reset_time` → default `00:00`. Cap profondità 32.
- `HabitConfigTimeline` interno: per-day lookup di `HabitConfig` (current + history) — la run sopravvive a cambi di config.
- `StreakRun` esteso non-breaking con `IsCurrent`, `RecoveriesUsed`, `BreakReason?` (`Missed`/`Manual`/`Closed`).
- `IStreakService.MarkBrokenAsync` / `ClearBrokenAsync` headless (UI rinviata).
- Banner `Detail_BasicStreakNotice` rimosso (engine ora completo).

**Recovery semantics**:
- `None`: prima miss chiude run.
- `GraceDay`: 1 miss/finestra 7gg rolling. Recoveries con "pendingAbsorbs" buffer (committate solo se la run sopravvive con un Counts day successivo — deviazione interna documentata vs. plan).
- `Manual`: short-circuit prima della policy; run chiude solo se `StreakBrokenAt` settato e `≤ giorno corrente`.
- `Retroactive`: switch dataset (`dailyAll` vs `dailyLive`) per-day in base alla policy attiva del giorno; policy stessa ritorna sempre `false` (effetto via dataset selection).

**Pause handling**: una pausa estende `runEnd` ma non incrementa `length` né consuma recovery (anche se sovrappone una miss).

**Mode semantics**:
- Daily: ogni giorno scheduled deve avere completion.
- Weekly: target settimanale (ISO Mon-Sun) — verdict `Counts/Misses` solo a fine settimana.
- Occurrences: gap non rompe; solo Manual/Closed chiudono la run.
- Both = Daily AND Weekly (Misses se uno dei due).

**Known limits**:
- L'engine bucketizza per `CompletedAt` calendar date; il bucketing local-by-reset (consumo di `IResetTimeResolver`) arriva in milestone successiva quando "today" sarà rivisitato.
- `StreakPause` UI rinviata (service esiste da M3.5).
- `MarkBroken` UI rinviata (service esiste).

### Stato Milestone 3.9

24 task del plan `docs/superpowers/archive/plans/2026-05-25-milestone-3.9-custom-charts.md` completati (21–22 risolti come "no-migration" documentato — vedi sotto). 665 test verdi (Core 40, Plugins 61, **ChartEngine 18 nuovo progetto**, Data 266, WPF 280 incl. E2E `ChartsWorkspaceE2ETests`). Build pulito, zero warning. **Schema delta: zero** — `chart`/`chart_series` esistono da `InitialSchema`; `schema_version` resta `2.6`. Nessuna migration.

**Nuovo assembly `Recordum.Plugins.ChartEngine`** (`net10.0-windows`, UseWPF, unica sede del codice LiveCharts2 `LiveChartsCore.SkiaSharpView.WPF` 2.0.4): è il **primo consumer reale del plugin system** sotto plugin id `io.recordum.chart-engine`. `NoWarn=NU1701` per OpenTK/SkiaSharp.Views.WPF (TFM .NET Framework, funzionali su .NET 10).

**Contract (`Plugins.Contracts/Charts` + `ExtensionPoints`)**: `ChartKind` enum (Line/Bar/Area/StackedBar/Scatter/Pie) + `ChartKindParser` (token round-trip, fallback Line); `ChartRenderModel` + `ChartSeriesModel`/`ChartPoint`(struct)/`ChartAxisModel`/`ChartThemeModel` (UI-free); `IChartRenderer : IExtensionPoint` con `CreateView` che ritorna `object` (l'host fa il cast a `FrameworkElement` — Contracts resta UI-free); `ChartConfig` (de)serializza `Chart.ConfigJson` (range/from/to/bucket/show_legend/axis labels, default 90d/daily).

**Data (`Recordum.Data/Services/Queries`)**: `IChartQueryService.BuildModelAsync(chartId, isDark, ct)` — **vive in `Recordum.Data`, NON in Core** (deviazione documentata: ritorna un tipo `Plugins.Contracts`, e Core ha zero dipendenze non-Core). Risolve range, granularità (daily/weekly), per-serie via `ICompletionService.GetDailySumAsync` (metric esplicita) o `ITrendQueryService.GetTrendAsync` (count/no-metric); Pie collassa ogni serie a un singolo totale. X cartesiana = `DateOnly.ToOADate()`, `Label` porta la data leggibile. `IChartService.ListAsync` per la gallery.

**ChartEngine**: `ChartEngineRenderer` (`PluginId="io.recordum.chart-engine"`, `PointId=chart.renderer`). `MapSeries` (metodo pubblico d'istanza puro, testabile senza control — `#pragma CA1822`) mappa `ChartRenderModel`→`ISeries[]` LiveCharts2 (`LineSeries`/`ColumnSeries`/`StackedColumnSeries`/`ScatterSeries`/`PieSeries<double>`). `CreateView` costruisce `CartesianChart`/`PieChart` temati; il **date-labeler OADate è applicato solo quando `XAxis.IsTime == true`** (gli assi non-temporali come l'ora 0–23 restano numerici). `ChartEngineManifest` + `ChartEngineActivator` (`EnableAsync` via `PluginManager` + `registry.Register`) — **idempotente** (short-circuit se il renderer è già registrato, perché `ApplicationInitializer.InitializeAsync` può rieseguire). Attivato in `ApplicationInitializer` dopo le migration; `IChartQueryService` scoped + `IChartEngineActivator` singleton in `ServiceRegistrar`.

**WPF — Charts workspace**: nav top-level Home | Charts in `MainWindow` (`AppWorkspace` enum + `ShowHome`/`ShowChartsCommand`, Ctrl+4; Ctrl+1/2/3 rientrano in Home via `PreviewKeyDown` senza consumare l'evento). `ChartsWorkspaceViewModel` (lista + builder + render): **risolve il renderer dall'`IExtensionPointRegistry`** (`GetExtensions(chart.renderer).OfType<IChartRenderer>().FirstOrDefault(CanRender)`), MAI reference diretta all'engine. `ChartBuilderViewModel` (create/edit, `Series` di `ChartSeriesRowViewModel`, `Saved` event) + `ChartListItemViewModel`. `ChartsView.xaml` master-detail (lista | chart renderizzato / builder overlay via `BoolToVisible`/`InvBoolToVisible`). 21 chiavi resx en/it (`Workspace.*`, `Charts.*`).

**Migrazione 4 detail chart M3.7 (Fase E)** — DP/API pubblici invariati, ViewModels e test M3.7 intoccati:
- `TrendChartControl` + `HourlyHistogramControl` → **migrati** a LiveCharts2 via builder (`Builders/TrendChartBuilder`, `Builders/HourlyChartBuilder`) che riusano `ChartEngineRenderer.CreateView`. Canvas/Polyline/Rectangle rimpiazzati da `ContentControl` host + `Rebuild()` su `DataContextChanged`/`Loaded`. Empty-state preservato.
- `YearHeatmapControl` + `StreakTimelineControl` → **mantenuti su Canvas bespoke** (decisione utente, guardrail spec §7): LiveCharts2 `HeatSeries` usa un gradiente continuo + legend/assi, non la ramp GitHub discreta a 5 step richiesta dal DesignBrief ("colore = identificatore, niente axis clutter"); la timeline bespoke dà barre date-range precise con tooltip. Nessun builder dead-code creato.
- `ThemeProbe` (helper interno `Controls/`): rileva dark/light dalla luminanza di `Brush.Background` — i control LiveCharts2 non consumano i `DynamicResource` brush; rivalutato a ogni `Rebuild`.

**Known quirks** — vedi memory `recordum-milestone-3-9-spec`.

### Stato Milestone 3.10a

22 task del plan `docs/superpowers/archive/plans/2026-05-27-milestone-3.10a-export-system.md` completati. 697 test verdi (Core 40, Plugins 64, **ExportEngine 17 nuovo progetto**, ChartEngine 18, Data 272, WPF 286 incl. E2E launch tests). Build pulito, zero warning nuovi. **Schema delta: zero** — `export_job`/`export_template` esistono da `InitialSchema`; `schema_version` resta `2.6`. Nessuna migration.

**Nuovo assembly `Recordum.Plugins.ExportEngine`** (`net10.0`, NO WPF — riutilizzabile in Fase 5 web): **secondo consumer reale** del plugin system sotto plugin id `io.recordum.export-engine`, registra 5 formatter sotto `export.format`. NoWarn=NU1701 per SkiaSharp native assets.

**Contract (`Plugins.Contracts/Export` + `ExtensionPoints`)**: `ExportSubject` enum (Core: SingleHabit/Library); `ExportLayout` (sezioni metadata/summary/heatmap/completions/notes + range; `Parse(json)` con default fallback su null/empty/malformato; `ToJson()` round-trip); `ExportModel` + `ExportHabitModel`/`ExportSummary`/`ExportCompletionRow`/`ExportNoteRow`/`ExportHeatmapCell` (struct) UI-free; `IExportFormatter : IExtensionPoint` con `Format`, `CanFormat`, `Write(model) → byte[]` (host owns IO — Contracts resta IO-free).

**Data (`Recordum.Data/Services/Queries`)**: `IExportQueryService.BuildAsync(ExportRequest, ct)` — **vive in `Recordum.Data`** (deviazione documentata, stessa rationale di `IChartQueryService` M3.9: ritorna tipo `Plugins.Contracts`, Core ha zero dipendenze non-Core). `ExportRequest(Subject, HabitNodeId?, From, To, Layout)`. Single habit assemblea metadata + summary (current/longest streak via `IStreakAggregator`, total completions, goal actual/target) + completions filtered by From/To + notes (UpdatedAtMs desc) + heatmap. Library branch enumera tutti `HabitNode` con `NodeType == Habit` (global soft-delete filter esclude trashed). **Heatmap sempre il 371-cell year ending today** (anchor = Monday del corrente -52*7gg), indipendente da From/To — From/To filtrano solo completions/notes/summary. Solo celle con `Step > 0` emesse (sparse). Ramp via `IIntensityRampResolver`. `IExportJobService.{RecordAsync,ListAsync}` (history newest-first).

**Note di shape**: `Completion` non ha `ValueDecimal/Integer/Seconds` diretti (deviazione dal plan): la value scalare per `ExportCompletionRow` viene risolta da `c.Values.FirstOrDefault()` con coalesce `ValueDecimal ?? (decimal?)ValueInteger ?? (decimal?)ValueSeconds`.

**Engine — 5 formatter**: `ExportFormatterBase` (PluginId const = `io.recordum.export-engine`); `JsonExportFormatter` (snake_case JsonNamingPolicy, indented); `CsvExportFormatter` (RFC 4180 quoting, `\r\n`, InvariantCulture); `MarkdownExportFormatter` (H1 + summary list + pipe table + Notes section; `ReplaceLineEndings(" ")` su note body); `HtmlExportFormatter` (self-contained, `WebUtility.HtmlEncode` ovunque + heatmap PNG embed via base64 data URI); `PdfExportFormatter` (QuestPDF Fluent, A4, Community license, heatmap image `.FitWidth()`, page numbers). `HeatmapImageRenderer.RenderPng(cells)` headless SkiaSharp 53×7 con 5-step GitHub light ramp (Pad6 / Cell11 / Gap2 → PNG ~750×100).

**Manifest + Activator**: `ExportEngineManifest` (`ExtensionPoints=["export.format"]`, `MinRecordumVersion=3.10.0`); `ExportEngineActivator` (`EnableAsync` via `IPluginManager` + register 5 formatters via `IExtensionPointRegistry`). **Idempotent** (short-circuit se il PluginId è già registrato — `ApplicationInitializer.InitializeAsync` può rieseguire). Setta `QuestPDF.Settings.License = Community` prima del register.

**WPF — Export dialog**: `ExportDialogViewModel` (sealed partial, ObservableObject) con AvailableFormats/section toggles/Templates preset/SelectedTemplate→applies LayoutJson. `ExportCommand` risolve formatter dall'`IExtensionPointRegistry` (`GetExtensions(export.format).OfType<IExportFormatter>().FirstOrDefault(CanFormat(selectedFormat))`), MAI ref diretta all'engine. `ISaveFileDialog` astratta (Prompt + WriteAsync) — `WpfSaveFileDialog` implementa Microsoft.Win32 `SaveFileDialog`. `IExportDialogLauncher` (`ShowForHabitAsync` + `ShowForLibraryAsync`) — `WpfExportDialogLauncher` risolve VM via ServiceProvider e mostra `ExportDialog` modal. Launch points: header `HabitScreen` "Export" button (`HabitScreenViewModel.ExportCommand` → event `ExportRequested` → `MainWindowViewModel.OnHabitScreenExportRequested`); top nav "Export library" button (`MainWindowViewModel.ExportLibraryCommand`).

**i18n**: 12 nuove chiavi resx en/it (`Export.Title/Button/Library/Format/Template/Sections/Section.*/Run/Cancel`) + designer underscore props.

**Known quirks** — vedi memory `recordum-milestone-3-10a-spec`.

### Stato Milestone 3.10b

25 task del plan `docs/superpowers/archive/plans/2026-05-28-milestone-3.10b-backup-restore.md` completati. **740 test verdi** (Core 41, Plugins 66, ChartEngine 18, ExportEngine 17, Data 292, WPF 306 incl. E2E `BackupRoundTripE2ETests`). Build pulito, zero warning nuovi. **Schema delta v2.7**: tabella `backup_log` + 5 AppSetting `backup.auto.*` + bump `schema_version` 2.6→2.7. Migration `BackupLogV27` additiva.

**Architettura `.recbak`**: header `RCBK` (4B magic + 1B versione + 3B reserved + 1B KDF id + 2B Argon2 mem MiB + 1B iter + 1B parallel + 16B salt + 12B nonce) + AES-256-GCM ciphertext + 16B tag. Inner payload = ZIP (`manifest.json` JSON snake_case + `db/recordum.db` plaintext dump via `sqlcipher_export` + `attachments/*`). KDF: Argon2id 64 MiB / 4 iter / p=2. Auto-backup usa passphrase fissa wrappata DPAPI in `RecordumOptions.AutoBackupKeyPath` (Windows-only); backup manuali usano passphrase utente. Wrong-passphrase = `RestoreException("invalid_passphrase")` indistinguibile da ciphertext tamper.

**Core (`Recordum.Core/Backup/`)**: 15 record/interfaces — `BackupKind`, `BackupOptions`, `BackupResult`, `BackupCounts`, `PluginManifestSnapshot`, `PermissionState`, `BackupManifest`, `RestoreOptions`, `RestorePreview`, `RestoreResult`, `IBackupService`, `IRestoreService`, `IBackupScheduler`, `BackupException`, `RestoreException`. `IBackupCipher` in `Abstractions/Security/`. `IBackupStoragePaths : IDbStoragePaths` (aggiunge `AttachmentsDirectory`, `BackupsDirectory`, `AutoBackupKeyPath`) — implementato da `RecordumOptions`, evita che Data dipenda da WPF.

**Data (`Recordum.Data/Backup/` + `Services/`)**: `AesGcmBackupCipher` (cifrario completo), `BackupArchiveWriter`/`BackupArchiveReader` (ZIP I/O), `BackupService.CreateAsync` (pipeline: count → snapshot plugin manifests + permission state → `ATTACH ... KEY '' + sqlcipher_export('plaintext')` → copy attachments → archive → encrypt → write `.recbak` → log a `backup_log`), `RestoreService.PreviewAsync` (decrypt+read manifest+warnings) e `RestoreAsync` (safety pre-restore snapshot opzionale → close pool → extract DB+attachments → swap files atomico → unavailable-plugins list), `BackupScheduler.RunDueAsync` (controlla `backup.auto.enabled` + `backup.auto.frequency_days` + `backup.auto.last_run_utc`, self-disable se chiave DPAPI mancante), `BackupLogService` (history CRUD).

**Plugin manager**: `IPluginManager` esteso con `IReadOnlyList<PluginManifest> ListActivatedManifests()` — `BackupService` lo legge per popolare lo snapshot manifest nel backup, `RestoreService` lo confronta col manifest del file per emettere warning "plugin X non più presente".

**WPF — 4 ViewModel + 4 View**: `BackupDialogViewModel` (passphrase strength: <8 Weak, <16 Ok, ≥16 Strong; `CanRun` richiede ≥12), `RestoreDialogViewModel` (preview→confirm→restore, safety snapshot opt-out), `BackupHistoryViewModel` (rows da `backup_log`, delete/open-folder), `BackupSettingsViewModel` (`backup.auto.*` via `IAppSettingService` + `IAutoBackupKeyStore` per DPAPI wrap). Views: `BackupDialog.xaml`, `RestoreDialog.xaml`, `BackupHistoryView.xaml`, `BackupSettingsDialog.xaml`. **Astrazioni file dialog dedicate** (`IBackupSaveFileDialog`, `IOpenFileDialog`) per non coupling backup all'`ISaveFileDialog` di export.

**Integration**: `BackupSchedulerHost` (`PeriodicTimer` 60min, fire-and-forget loop con `IServiceScopeFactory`) start da `ApplicationInitializer.InitializeAsync` dopo WebView2 probe. **Wait one tick prima del primo run** (deviation post-impl: il fire-immediato race-conditionava startup DB activity nei test `BootstrapSmokeTests`). `MainWindow` top-nav: menu "Backup" con `Create backup…`, `Restore from backup…`, `Backup history`, `Auto-backup settings…`. `AppWorkspace` esteso con `BackupHistory`. `BackupDialogLauncher` (`IBackupDialogLauncher` + `WpfBackupDialogLauncher`) registra tutti i service backup in `ServiceRegistrar` (singleton + scoped + transient consistenti).

**i18n**: ~25 chiavi resx en/it (`Backup.*`, `Restore.*`, `BackupHistory.*`, `BackupSettings.*`, `Workspace.BackupHistory`) + designer underscore props.

**Deviazioni dal plan** (documentate sui singoli commit):
- `IBackupStoragePaths` introdotto per evitare cast `(RecordumOptions)paths` in Data layer.
- `BackupService.DumpDbAsync` usa `ATTACH ... KEY '' + sqlcipher_export()` (non `VACUUM INTO` come da plan): solo il primo produce un file SQLite davvero plaintext fuori da SQLCipher.
- `Pipe` extension del cipher (Task 7) sostituito con `_ = ex; throw ...` perché il pattern del plan aveva type error.
- Passphrase strength thresholds del `BackupDialogViewModel` calibrati `<8`/`<16` per matchare i test inline del plan.
- `IBackupSaveFileDialog` separato da `ISaveFileDialog` (export-specific signature).
- `BackupSchedulerHost.LoopAsync` non fa più tick immediato — wait one interval prima.

**Known limits**:
- Auto-backup è Windows-only (DPAPI). Su altre piattaforme servirà alternativa (Fase 5/6).
- `BackupRoundTripE2ETests` usa `ExecuteDeleteAsync` con `IgnoreQueryFilters()` per evitare il global filter soft-delete su DB con solo seed.
- Nessuna compressione differenziale / incremental backup (full snapshot ogni volta).
- Nessun cloud upload integrato — l'utente sceglie dove salvare `.recbak`; provider terzi via Fase 6.

---

## Fase 4 — Polish/UX (corrente)

Branch dedicato: `fase-4-polish` (creato 2026-05-29 da `milestone-3.10b-backup-restore`).

### Avanzamento 2026-05-29 (giorno 1)

**Theming Pass 1 — `6a15cea`**:
- Nuovo `Resources/BaseStyles.xaml` (always-loaded da `App.xaml`) con implicit `Style` per ~20 control type: `TextBlock`, `Window`, `UserControl`, `Page`, `Label`, `TextBox`, `PasswordBox`, `ComboBox`/`ComboBoxItem`, `CheckBox`, `RadioButton`, `ListBox`/`ListBoxItem`/`ListViewItem`, `TabControl`/`TabItem`, `GroupBox`, `Expander`, `Menu`/`MenuItem`/`ContextMenu`, `ToolTip`, `Button`/`RepeatButton`/`ToggleButton`. Tutti cablati a `Brush.Text` / `Brush.Surface` / `Brush.SurfaceElevated` / `Brush.Border` via `DynamicResource`.
- Foreground è il fix principale: senza style, WPF default = `SystemColors.ControlTextBrush` = nero, illeggibile su dark `Brush.Background` = `#FF09090B`.
- `ControlTemplate` Aero di `Button`/`TabItem`/`ComboBox` NON sostituiti — solo Foreground via Setter. Pass 2 quando confermato visivamente.

**Crash tab Dettaglio — root cause confermata, `abb9e4c`**:
- Era l'ipotesi (a) della lista bloccanti: `CommandParameter="Days30"` string passato a `IAsyncRelayCommand<TrendRange>`. WPF binding non auto-converte string→enum per `CommandParameter`, `RelayCommand<T>.ThrowArgumentExceptionForInvalidCommandArgument` esplode al primo CanExecute check del Button.
- Sorgente del crash al launch: nel runtime con Theming Pass 1 attivo il template DataTemplate per `DetailViewModel` viene materializzato prima (probabilmente layout pass diversa); l'errore esisteva da M3.7 ma si manifestava solo al click sulla tab.
- Fix: `CommandParameter="{x:Static vmd:TrendRange.Days30}"` (e `Days90`/`Year1`).

**Verifica visiva**:
- App si avvia, tab Dettaglio funziona (✓ confermato utente).
- Lista altri bug ancora aperta — triage giorno per giorno in `docs/phase-4-issues.md`.

### Avanzamento 2026-05-29 (sessione estesa)

Diagnosi e fix di produzione (commit su `fase-4-polish`):

- **B1 "Save note" reinquadrato** — non un bug di Save: (a) chrome chiaro-su-chiaro (R1) rendeva il bottone invisibile; (b) WebView2 in fallback. Diagnosi via screenshot utente.
- **WebView2 falso negativo (`dc332cb`)** — `CoreWebView2Environment.CreateAsync` richiede STA + message-pump; `ApplicationInitializer` lo awaita dentro catena `ConfigureAwait(false)` (thread MTA) → lanciava → fallback silenzioso nonostante runtime installato. Fix: marshal su `Application.Current.Dispatcher` in `WebView2Availability.ProbeAsync` + logging nel catch (prima inghiottito → da qui in poi i crash XAML appaiono in `recordum.log`).
- **Theming Pass 2 (`2935091`)** — flat `ControlTemplate` per `Button`/`TabItem`/`ComboBox`. `RepeatButton`/`ToggleButton` lasciati Foreground-only (un template bottone romperebbe ScrollBar/Expander).
- **Theming Pass 3 (`4c6bac5`)** — `CheckBox` flat (implicito), toggle "Advanced" pill (stile keyed `Recordum.PillToggle`, NON implicito), `DatePicker`/`Calendar` tematizzati (flat `CalendarDayButton`/`CalendarButton`). Chiude B7. Quirk: `CalendarButton.IsSelected` non è trigger valido (usato `IsPressed`) → highlight mese non persiste.
- **B4 (`fd0cddb`)** — `HomeRowViewModel` alza `Changed` dopo soft-delete/archive/unarchive/restore/purge/move/pin; `HomeViewModel` ricarica preservando selezione per id.
- **B5 (`fd3f69a`, `93d3323`)** — `QuickActionExecuted` gestito (recorded→refresh via bus, !recorded→apre dettaglio); `HomeViewModel` sottoscrive `Completion*` del bus e ricarica (marshallato su Dispatcher — il bus NON marshalla, e i quick-action handler usano `ConfigureAwait(false)`). Quirk `CrashLoggerTests`: usavano `FileInfo.Length` (byte) come indice `Substring` (char), rotti dall'em-dash nel messaggio WebView2 → fix offset char-based (`e211c18`).
- **B6a (`05f61c9`)** — `HabitTemplateService.InstantiateAsync` scartava metrics/segments/goal (il `TemplatePayload` non aveva quei campi). Ora materializza tutto, risolve unit/scale per codice, snapshotta le label scala sulla metrica (così `StateQuickAdd` mostra la scala). `DbFixture` HA il seed (commento "no seed" fuorviante).
- **B6c — UI sezioni editor** (spec `2026-05-29-editor-sections-ui-design.md`, plan `2026-05-29-editor-sections-ui.md`, eseguito subagent-driven). `EditorPane.SectionsHost` era vuoto ("Phase D" mai fatta): i 11 section VM (M3.4) non avevano View. Costruite 10 View + host `ItemsControl`/`Expander` + DataTemplate impliciti; `SectionViewModelBase.Title/IsVisible/IsExpanded`; `HabitEditorViewModel.Sections` + `IsApplicable`→`IsVisible`; nuovo `IReferenceDataQueryService` (liste unit/scala per i picker). Crash runtime: `{StaticResource *Values}` (ObjectDataProvider enum) in `SectionTemplates.xaml` non risolvibili dalle view istanziate via DataTemplate fuori dall'albero → spostati in `App.xaml` (`d3ad3cb`).
- **B9 i18n (`3b42d2f`)** — 25 stringhe editor+picker localizzate EN+IT. Nomi display dei template seed lasciati (dati DB). Fix test culture-independent (`c0c515b`): asserire contro `Strings.*`, non letterali inglesi (macchina it-IT).
- **Rifiniture B6c (`50ef22d`)** — day-picker schedule `SpecificDays` (convenzione `["Mon".."Sun"]` da `ScheduleEvaluator`); chip Tag con stato selezionato.

Suite a fine sessione: **WPF 335 / Data 296 verdi**.

**Deciso ma parcheggiato**: modello d'interazione "A evoluto" (colonne toggle) — spec `2026-05-29-navigation-model-design.md`, da implementare dopo le basi.

**Aperti**: B6b template personalizzato (feature, richiede design); rifiniture minori (picker scala, layout scala State, "+" su State già selezionata); nav-model. In attesa del report visivo dell'utente.

### Avanzamento 2026-06-23 (da macOS)

> Dettaglio per-finding in `docs/phase-4-issues.md` §"Stato al 2026-06-23"; vincoli build/test e checklist di verifica VM in `docs/dev-environment-sync.md` §"Log sessione 2026-06-23".

3 commit su `fase-4-polish`:
- **`24c0e60`** — chiusi i 4 findings sicurezza/perdita-dati **CR1–CR4** (backup/restore, Data layer): restore atomico + rollback (CR1), guard `schema_version` reale con confronto major.minor (CR2), bound sui parametri Argon2id non fidati + cap anti zip-bomb (CR3), shred dei temp in chiaro (CR4). Nuovi helper `SchemaVersionGuard`/`SecureWipe`. Test `SchemaVersionGuardTests` + casi CR3 verdi (osx-arm64).
- **`3497021`** — **appearance pass**: editor diario tematizzato (segue tema **e** font a caldo via tripwire `SetResourceReference`) + diario serif; input minimal **underline** esteso a `PasswordBox` e preview `NotesTabView`; **font UI Palatino** (`Tokens.Font.Default` ereditato da `Window/Page`; `Consolas` resta monospace) con **switch utente** in Impostazioni → Aspetto — nuovo `AppFontService` (mirror di `ThemeService`, AppSetting `ui.font`, default Serif); il diario segue il font via push C#.
- **docs** — questo log + tracker + `CLAUDE.md` + anteprime browser.

Sessione su macOS: tutto **compila** (`-p:EnableWindowsTargeting=true`) ma WPF/SQLCipher non eseguono lì → suite test runtime + restore-corrotto + verifica visiva **in sospeso sulla VM**.

### Bug e rifinitura aperti

Vedi `docs/phase-4-issues.md` per il tracker triagato (`reported / verified / triaged / in-progress / fixed / verified-fixed`).


---

# Archivio da CLAUDE.md (2026-09-30)

> Il 2026-09-30 CLAUDE.md è stato riscritto da capo (da 103 KB a 13 KB): il *perché* in `docs/PRODUCT.md`, il *dove
> siamo* in `docs/ROADMAP.md`, le regole in CLAUDE.md. Qui sotto, **parola per parola**, le parti del vecchio file che non
> sono entrate nel nuovo: la catena delle versioni dello schema, i dettagli per versione, e la cronaca della §7 (roadmap e
> progressi di Fase 4, onde, R9, Avalonia F0-F1, revisione e Passo 0). Il file intero resta in git fino al commit
> precedente alla riscrittura.

## Catena delle versioni dello schema (vecchio §1)

- **DB schema**: v2.18 (settembre 2026) — delta `workout_log_weekly_target` v2.18 (data-only: il template seed "Workout log" passa da `SpecificDays` senza giorni — mai programmato — a `WeeklyX` con target 3, chiude W25-2) > `food_diary_quick_note` v2.17 (data-only: il template seed "Food diary" perde le tre metriche fantasma e dichiara `journal_gesture: QuickNote`, "Daily journal" dichiara `Diary`) > `workout_log_unit` v2.16 (data-only: il template seed "Workout log" dichiara `"unit":"minutes"` sulla metrica durata, chiude R7) > `sleep_repeatable` v2.15 (data-only: seed Sleep ripetibile + serie nominate "Night"/"Nap") > `per_type_prefs` v2.14 (`habit_nodes.ui_prefs_json` + `habit_configs`/`habit_config_histories.repeatable`+`series_labels_json`) > `attachment` v2.13 (tabella `attachment` singolare Note-only, sostituisce lo scaffold polimorfico `attachments` mai wired) > `abstinence_period` v2.12 (astinenza a stato esplicito; chiude anche **CR8** riallineando il seed `schema_version` a 2.12) > `note_category` v2.11 (`notes.category` + `notes.completion_id`) > `counter_reclass` v2.10 (data-only: resistance-serie → counter) > `note_history` v2.9 > `abitudine_calendar` v2.8 > `backup_log` v2.7 > `streak_broken_at` v2.6 > `active_timer` v2.5 > PinScope v2.4 > provenance plugin v2.3 > v2.2 > v2.1.

## Schema — dettagli per versione (vecchio §6)

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

**NoteCategory v2.11**: `notes.category TEXT NOT NULL DEFAULT 'Veloce'` (maxLength 16, CHECK `ck_note_category` `category IN ('Diario','Veloce')`) + `notes.completion_id TEXT NULL` (FK soft a una completion, indice `ix_notes_completion_id`). Migration additiva `NoteCategoryV211` bumpa `schema_version` a `2.11`.

**AbstinencePeriod v2.12**: tabella `abstinence_period` (`id` GUID PK, `habit_node_id` FK CASCADE dal nodo, `started_at_ms INTEGER`, `ended_at_ms INTEGER NULL`, `created_at`/`updated_at`/`updated_at_ms`) + indice `ix_abstinence_period_habit_started` su `(habit_node_id, started_at_ms)`. Evento-figlio → **niente** `created_by_plugin_id`. Migration additiva `AbstinencePeriodV212` bumpa `schema_version` a `2.12` **e** riallinea il seed costante `AppSettingSeed.schema_version` a `2.12` — **chiude CR8** (il vecchio disallineamento seed a 2.9, senza impatto runtime, è risolto).

**Soft delete**: solo su `HabitNode`. Cascade in transazione singola ai discendenti. Restore ripristina solo i discendenti chiusi nella stessa operazione. Purge permanente è job background per nodi >30 giorni (ordine: file → tabelle figlie → HabitNode).

**Naming**: tabelle e colonne snake_case (via `EFCore.NamingConventions`). Classi/proprietà C# PascalCase. Tutte le cascade rules e tutti gli indici **dichiarati esplicitamente** in Fluent API — mai default EF Core.

---

## Roadmap e cronaca (vecchio §7)

> **Roadmap rivista 2026-06-19** (brainstorm con l'autore): vedi `docs/superpowers/archive/specs/2026-06-19-roadmap-revision-design.md`. Mobile anticipato sopra il web; Web/AI rimandati; sync di terzi confermato.

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

**Branch**: il lavoro di Fase 4 è stato svolto su `fase-4-polish` (creato 2026-05-29 da `milestone-3.10b-backup-restore`) e **consolidato in `main` il 2026-07-01** per la pubblicazione su GitHub — cronologia completa preservata, tag di backup locale `archive/full-history-2026-07-01`. Da qui in poi: **feature-branch → merge in `main`** (l'hook `guard-main.js` blocca i commit/push diretti su `main` nelle sessioni Claude Code).

**Decisione (freeze 2026-06-16)**: nessuna nuova fase finché il desktop non è una **v1.0 funzionante e gradevole**. Asticella v1.0 = *core solido + UX gradevole + esplorazione dati funzionante* (vedi spec `docs/superpowers/archive/specs/2026-06-19-roadmap-revision-design.md`). **Niente nuove feature.**

**Scope v1.0** (exit criteria): chiudere i findings del tracker, **priorità CR1–CR4** (perdita-dati/sicurezza backup-restore); **far funzionare i chart** (oggi rotti); **verificare+fixare export e plugin**; esplorazione dati di base (heatmap/statistiche); perf (<500ms home); UX/theming "gradevole". Aree core verificate a schermo dall'autore; `/security-review` superata sui findings di sicurezza. **Fuori scope**: AI/correlazioni, audit accessibilità completo (→ 1.x).

**Bug + rifinitura aperti** — tracker triagato in [`docs/phase-4-issues.md`](docs/phase-4-issues.md). Stati: `reported / verified / triaged / in-progress / fixed / verified-fixed`. Code review 2026-06-19: findings **CR1–CR24** (4 alti su sicurezza/perdita-dati). **Stato al 2026-06-24: 22/24 `fixed`** (compile-verified su Mac; i test WPF/Data girano sulla VM) — **CR6 `wontfix`** (reclass resistance, interpretazione dubbia + zero dati first-party), **CR8 `fixed`** (chiuso dalla migration `AbstinencePeriodV212` che riallinea il seed a 2.12). Backlog del code review chiuso.

**Progresso 2026-05-29**: Theming Pass 1→3 (BaseStyles implicit style + chrome flat Button/TabItem/ComboBox + CheckBox flat + DatePicker/Calendar tematizzati); crash tab Dettaglio risolto; WebView2 probe sul Dispatcher UI (editor markdown ripristinato); refresh Home dopo mutazioni riga (B4) e dopo ogni completamento (B5); instanziazione template materializza metriche/scala/goal/segmenti (B6a); **costruita l'intera UI di personalizzazione dell'editor** — 10 section view + host Expander (B6c); i18n editor+picker (B9); day-picker schedule + chip Tag selezionate. Suite WPF 335 / Data 296 verdi. **Aperti**: B6b template custom (richiede design), rifiniture minori (picker scala, scala State, "+" State), nav-model a colonne toggle (spec parcheggiato). Tracker autoritativo `docs/phase-4-issues.md`; spec/piani in `docs/superpowers/`.

**Progresso 2026-06-23** (sessione da macOS — vincoli build/test e checklist di verifica VM in [`docs/dev-environment-sync.md`](docs/dev-environment-sync.md) §"Log sessione 2026-06-23"): **CR1–CR4 chiusi** (commit `24c0e60`) — restore atomico + rollback, guard `schema_version` reale (major.minor), bound Argon2id + cap anti zip-bomb, shred dei temp in chiaro. **Appearance pass** (commit `3497021`): editor diario tematizzato (segue tema **e** font a caldo via tripwire) + diario in serif; input minimal **underline** esteso a `PasswordBox` e preview note; **font UI Palatino** con **switch utente in Impostazioni → Aspetto** (nuovo `AppFontService`, mirror di `ThemeService`; AppSetting `ui.font`, default Serif). Tutto **compila** su Mac (`-p:EnableWindowsTargeting=true`) ma WPF/SQLCipher **non girano** lì → suite test + restore-corrotto + verifica visiva **da fare sulla VM**. Dettaglio: `docs/phase-4-issues.md` §"Stato al 2026-06-23".

**Progresso 2026-06-23 (continuazione, da macOS)** — chiusi altri findings del code review. **Data layer** (commit `fe6cb9d`, `37a2fd5`): CR5 (State valore-0 nel calendario, Data+WPF), CR7 (materializzazione template a singola transazione), CR9 ("Goal" hardcoded → eredita nome habit), CR10 (validazione Move condivisa), CR11 (length streak astinenza ancorato a createdDate; toccava anche l'export). **WPF** (compile-only su Mac, run+verifica VM): CR13 (durate visibili in Cronologia), CR20 (`IClock` in BackupDialog), CR22 (year-heatmap a 54 settimane → 31 dic non sparisce). **WONTFIX**: CR6 (riclass resistance — interpretazione dubbia, zero dati first-party). **CR8** rimandato alla VM (regen ModelSnapshot via `dotnet ef`). **Scoperto e sistemato**: il progetto `Recordum.WPF.Tests` non compilava dalla `3497021` (costruttore `SettingsViewModel` con nuovo `IAppFontService`, 2 test non aggiornati) → `FakeAppFontService` + 4 siti. Test Data verdi su `osx-arm64` (suite 339; 17 fail = solo DPAPI/SQLCipher Windows-only); WPF compila. Dettaglio: `docs/phase-4-issues.md` §"Continuazione 2026-06-23". **UX** (anteprime browser approvate, resa da verificare sulla VM): restyle dei **menù tasto-destro** (`ContextMenu`/`MenuItem` in `BaseStyles.xaml`) — da default Aero a pannello flottante arrotondato + ombra, niente colonna-icone, hover accento 14%, submenu con chevron (`docs/design/context-menu-preview.html`); **Slider** della scala State (era thumb Aero → traccia flat + fill accento + thumb circolare, `docs/design/slider-preview.html`); **simbolo del programma** nella title bar (era icona Lucide "activity" a linea → **R serif Palatino** nel colore testo, `MainWindow.xaml`, `docs/design/brand-mark-preview.html`); **dropdown ComboBox** allineato alla famiglia popup dei menù (ombra + raggio 8 + hover accento, `docs/design/combobox-preview.html`); **scorrimento colonne workspace** ripristinato come **ibrido fill-then-scroll** (regressione da `f08fba9` star-split; ora `HabitScreen.xaml` avvolge la griglia in uno ScrollViewer orizzontale con `Grid.Width=ViewportWidth` + `Grid.MinWidth=OpenColumnsMinWidth` = colonne×320 → riempie se c'è spazio, scrolla se sono troppe, mai sotto 320, `docs/design/column-scroll-preview.html`); **ToolTip** allineato alla famiglia popup (era rettangolo squadrato Aero → raggio 6 + ombra morbida, `docs/design/tooltip-preview.html`); **divisori colonne** (`GridSplitter`) da barra grigia 4px a **sash** (hairline a riposo, barra accento su hover/drag, stile `HabitScreen.ColumnSplitter`, `docs/design/column-splitter-preview.html`); **empty-state** (`EmptyState.xaml` Home + welcome `WorkspaceView`) da singola riga grigia a **icona line + titolo** centrati, icona per-tab (Sprout/Moon/Sparkles/BookOpen, `docs/design/empty-state-preview.html`); **scrollbar** (già flat dalla Fase C) con **stato hover/drag** (scurisce+allarga) e **min-size** del thumb (`docs/design/scrollbar-preview.html`); **TabStrip workspace** (`WorkspaceView.xaml`) con angoli tab arrotondati + chiusura come icona X Lucide (era "✕"), underline accento e pin/preview invariati (`docs/design/tabstrip-preview.html`).

**Color Picker v2** (feature, brainstorm→spec→plan→subagent-driven, commit `505cdc9`→`e953e89`): overhaul di `ColorSwatchPicker` (ereditato da editor/builder/note) — polish famiglia flottante + hex "#", **anello di selezione** (F1), **ramp tonale** auto (`ShadeRamp.For`, F2), **preferiti salvabili** (`IFavoriteColorStore`, AppSetting `ui.favorite_colors` cap 12, F4), **contagocce schermo** (`IScreenColorPicker` Win32 isolato, VM-only, F3). Spec/piano in `docs/superpowers/`, mockup `docs/design/color-picker-preview.html`. **Nota build**: una nuova stringa = 3 edit (`Strings.resx`+`Strings.it.resx`+proprietà manuale in `Strings.Designer.cs`); nessun ResX code-gen.

**Progresso 2026-06-24 (da macOS)** — chiusura del backlog del code review (CR rimasti) + una rifinitura UX. **Robustezza/integrità** (commit `90c92c4`/`cc85c4d`/`d0e1e93`): CR14 (generation guard in `HabitEditorViewModel.LoadAsync` — cambi rapidi di habit non interlacciano più i load), CR15 (gli 8 handler `async void` in MainWindow/HabitScreen ora in try/catch+trace — niente crash su errore transitorio), CR16 (drag-drop tollera nodo/parent sparito da reload concorrente). **Guard/perf** (commit `885942e`/`68f3c89`/`6e760d1`): CR17 (zona-drop dalla card via `FindRowCard`), CR18 (generation guard su `HomeViewModel` reload), CR19 (`CancellationTokenSource` per-reload in `HabitRecordsViewModel`; VM ora `IDisposable`), CR24 (`LoadMarkersAsync` parallelizzato con `Task.WhenAll`), CR23 (nuovo `DebouncedColorRecorder` — il MRU colori cattura solo il colore assestato, non ogni tasto). **Convenzioni/reuse** (commit `04522ea`): CR21 (errori QuickAdd localizzati via `SetUnexpectedError` + nuova `SeriesQuickAddViewModelBase` che deduplica `ToggleChrono`). **Cosmetico** (commit `60ead30`): CR12 (`CurrentLength` richiede `BreakReason is null` → una ricaduta in giornata non lascia il numero stale). **UX**: CTA **"Crea la prima abitudine"** nell'empty-state Attive (`EmptyState.xaml` → `OpenNewHabitCommand`; nascosta su Archiviate/Cestino) + corretta la copy datata `Home.Empty.Active` (diceva ancora "milestone 3.4"). Ogni fix WPF ha un test (compile-only su Mac, run VM). Tutto compila (entrambi i progetti); **da fare sulla VM**: suite test completa Data+WPF + verifiche visive accumulate + CR8. Dettaglio: `docs/phase-4-issues.md`, handoff VM in `docs/dev-environment-sync.md`.

**Progresso 2026-07-01 (VM Windows)** — chiuso **U9 — Astinenza a stato esplicito** (spec/piano `2026-06-28-abstinence-explicit-state*`, 11 task): tabella `abstinence_period` (schema v2.12, chiude CR8), `IAbstinenceService`, motore streak da periodi, quick-add Inizio/Ricaduta, azione Home contestuale, calendario colorato dai periodi. Verifica visiva dall'autore → 2 lacune → **U9.1 — Orologio vivo + registrazioni periodi** (spec/piano `2026-07-01-abstinence-clock-and-records*`, 9 task TDD, eseguito subagent-driven): `DurationFormat.FormatStopwatch`, helper `LiveElapsedClock`, cronometro vivo in quick-add **e** Dettaglio, `IConfirmationDialog`, lista "Registrazioni" a righe-scheda nella Cronologia (durata/ricaduta/**elimina con conferma**), `IAbstinenceService.DeletePeriodAsync`. Suite verdi (WPF 679 / Data 370). Fix note-badge Resistance (regressione da U9 Task 10). **Review finale (opus) = "with fixes"** → **giro di fix chiuso e verificato a schermo dall'autore (2026-07-01)**: registrazioni chiuse mostrano l'**intervallo di tempo** (non più "N giorni puliti"); reload di records+quick-add dopo Inizio/Ricaduta/Elimina (niente più "In corso" stale); cronometro vivo `DispatcherTimer` reso `IDisposable` e fermato su reload/teardown (fix leak); heatmap del Dettaglio period-aware per l'astinenza (+ andamento/orario nascosti); cestino non più tagliato. Suite finali **Data 370 / WPF 683**. **U9/U9.1 `verified-fixed`.** Dettaglio nel ledger `.superpowers/sdd/progress.md`.

**Repo-prep per GitHub (2026-07-01)**: aggiunti `README.md` e `docs/ARCHITECTURE.md` (onboarding collaboratore); mockup di design spostati in `docs/design/`; licenza **MIT** (`LICENSE` + metadata SPDX in `Directory.Build.props`); `.gitignore` deduplicato.

**Piano di ridefinizione v1.0 (2026-07-03)** — mappa autoritativa: [`docs/superpowers/specs/2026-07-03-v1-redefinition-plan-design.md`](docs/superpowers/specs/2026-07-03-v1-redefinition-plan-design.md) (onde, exit criteria E1–E16, metodo vincolante per gli esecutori). **Onda 0 "verità di base" eseguita** (sessione VM con l'autore): 14 findings F0-1…F0-14 triati nel tracker — chart builder skeleton+crash, pipeline streak/andamento/orario, export non period-aware, stamp backup hardcoded, notifiche configurabili ma senza motore. **Spec approvate** per tutte le onde (1a chart+export, 1b foto alle note, 2 esperienza per-tipo + schema v2.13, 3 coerenza estetica dual-theme, 4 preparazione al pubblico con Velopack) + **piano implementazione 1a pronto** (`docs/superpowers/archive/plans/2026-07-03-wave-1a-charts-export.md`, autosufficiente). Esecuzione: **1a → 1b → 2.0-2.5 → 2.9 (misura core) → 3 → 4**; i piani delle onde successive si scrivono a ridosso dell'esecuzione, ciascuno dalla propria spec.

**Progresso 2026-07-09→11 (onde 1a/1b/2.0/2.1 + audit)** — **1a, 1b (schema v2.13), 2.0 (schema v2.14 + `IHabitUiPrefsService`) mergiate in main.** **Onda 2.1 (U6 riga Home) + giro "2.1-bis" APPROVATI dall'autore e mergiati in main il 2026-07-11** (Core 92 / Data 415 / WPF 754): chip valore-chiave + striscia per-tipo da `ui_prefs_json`, clock astinenza vivo coi secondi in-riga, sezione Configura "Riga Home", F0-11; il 2.1-bis (commit `7d5aa25`) ha aggiunto gli **stili striscia per-tipo** della spec §1 (quadretti action, mini-barre valore counter via `HomeTreeRow.ValuesLast14Days`, banda continua resistance, punti journal, ramp state) in un `HeatmapStrip` a **larghezza totale fissa 96px**, tolto la parola-tipo dalla riga (→tooltip, chiude A6) e reso muted lo zero-state (`×0`). **Prossima: Onda 2.2** (piano già scritto e arricchito). Durante la verifica autore, tre bug gravi trovati e fixati sullo stesso branch: **data-wipe ComboBox** (`4d01f9c` — `Clear()` di un ItemsSource bindato coerce SelectedValue a null e le sezioni Scale/Metriche lo persistevano: mai svuotare liste bindate senza guard `_loading`/detach; i dati già azzerati NON si auto-riparano — scala Mood ricollegata in sessione, unità counter da ri-selezionare a mano), **colonna Configura vuota** (`9af35e0` — wiring load-editor perso nella workspace shell `fdb6c2e`: ora lo screen carica `Editor` su apertura chip e in `LoadAsync` se già aperta; gate = `HabitScreenEditorWiringTests`), **W1a-VF2 risolto** (`0f4f637` — root cause vera: `LoadNew()` del chart builder non seminava righe serie; layout sano). **Audit interattivo 2026-07-11** (l'app si naviga via PowerShell+UIA: `SetProcessDPIAware()` obbligatorio a 125% scaling, preferire i pattern UIA ai click sintetici — inaffidabili sull'albero Home, finding A11): findings **A1–A11** nel tracker §"Audit interattivo 2026-07-11" — aperti i visivi A4 (trend/hourly su card bianche in dark + trend Mood vuoto, estende W1a-VF1 → Onda 3), A5 (heatmap anno implausibile), A6 (parola tipo ridondante nella riga Home), A7 (zona destra righe tagliata nell'albero stretto), A8 (sezioni editor sbordano dalla colonna — MaxWidth mancante), A9 (5 eventi/comandi morti), A10 (a11y UIA). Piano 2.2 aggiornato con questi fatti (sezione "New facts from the 2026-07-11 interactive audit"). Il redesign del builder "Crea nuova abitudine" (E7) resta l'ultimo pezzo dell'Onda 2 (U6→U10→U8→U7→builder).

**Progresso 2026-07-13→18 (onde 2.2/2.3 + cronometro + fix unità durata)** — **Onda 2.2** (striscia "Oggi" + colonne per-habit) mergiata in `main` (ff a `06a3ef4`, verifica visiva poi fatta). **Onda 2.3 — U8 registrazione** (repeatable, somma, chip serie nominate, selettore unità tempo) **code-complete sul branch `wave-2-3-registration`** (8 task, piano `docs/superpowers/archive/plans/2026-07-13-wave-2-3-registration.md`): flag `habit_config.repeatable`+`series_labels_json` (già schema v2.14), migration data-only **v2.15** (Sleep ripetibile + serie "Night"/"Nap"), cella durata con selettore h/min/sec normalizzato a secondi, totale counter = somma metrica (non occorrenze). **Review finale (opus) pulita + 2 fix**: F1 localizzazione unità durata in Cronologia (nuovo `DurationUnitDisplay` WPF, `Core.SecondsDisplay` resta numerico), F2 guard overflow decimal in `BuildValue`. **Cronometro quick-add con orologio vivo** (feature nuova, piano `2026-07-18-quick-add-live-chrono.md`, subagent-driven 3 task, review opus Ready-to-merge, commits `db77de0`/`01f0c23`/`3b3449f`): `LiveElapsedClock ChronoClock` su `SeriesQuickAddViewModelBase` + `IsChronoRunning` per-cella + base `IDisposable` + `HabitScreenViewModel` dispone il quick-add uscente; icona clock↔square + tempo che scorre accanto al campo. **Fix unità durata** (pass visivo, commits `d1bbecf`/`82dddee`): i 3 totali (riga Home `HabitTreeQueryService`, "Oggi" `CounterTodayContent`, striscia `PushDaySummaryAsync`) **convertono i secondi nell'unità della metrica** (90s su metrica "minutes" → 1.5, non più "90 minutes" = 60× sbagliato); unità doppia nascosta via `SeriesCellViewModel.ShowUnitLabel`. Suite **Core 98 / Data 419 / WPF 820** verdi. **Branch `wave-2-3-registration` NON mergiato** (manca il pass visivo completo dei restanti item U8: chip serie, somma non-ripetibile, builder, export). **Parcheggiato → giro con Fable** (memoria `recordum-counter-repeatability-rethink`): 4 questioni-modello counter — (1) flag ripetibile inerte sui tap counter + etichetta fuorviante, (2) Sonno totale-vs-sessioni, (3) metrica durata + unità separata (+ **bug goal-actual 60× ancora aperto**), (4) più metriche durata + più cronometri (`active_timer` UNIQUE per habit blocca il 2° timer).

**Progresso 2026-07-28→08-07 (chiusura dei findings del pass visivo 2.3)** — pass visivo dell'Onda 2.3 fatto il 2026-07-28 (evidenza in `docs/verification/2026-07-28-wave-2-3/`, **cartella gitignorata**: unica copia locale) → findings **W23-VF1…VF8**. Chiusi in codice: **VF1** (tap-counter, quattro numeri diversi per lo stesso giorno — due cause: fallback occorrenze mancante nel Data + `DayCount` mai riallineato), **VF2/VF3** (durate senza unità in secondi grezzi; serie nominate lette "Serie 1/2"), **VF6** (una registrazione a serie singola spezzava i campi su righe diverse, quella multi-serie no — due layout per lo stesso oggetto), **goal-actual 60×** (`SecondsDisplay.ConvertTotal` in Core, goal card + export convertono, i 3 siti a mano consolidati), **export** (ogni valore nomina la sua unità; il summary stampa il goal, che prima solo il JSON portava). Commit `9c759d9`, `3cec5d1`, `bebfd06`, `37ecd8c`, `09b3107`, `5949f58`. VF4/VF5/VF8 restano questioni di modello → giro con Fable; VF7 (grafici Dettaglio vuoti) è pre-esistente → Onda 3.

**Progresso 2026-08-07 (unità durata obbligatoria — spec+piano+esecuzione)** — chiusa la domanda aperta lasciata dall'export (`27000 seconds` nel file vs `7:30:00` nelle Registrazioni). Decisione dell'autore: **rendere obbligatoria l'unità temporale su una metrica durata**. Spec `docs/superpowers/archive/specs/2026-08-07-duration-unit-required-design.md` (`fd2639c`), piano `docs/superpowers/archive/plans/2026-08-07-duration-unit-required.md` (`5067648`), esecuzione subagent-driven. **Quattro decisioni di modello**: (1) un **evento singolo si legge come orologio**, gli **aggregati nell'unità** — quindi `DurationUnitDisplay` sparisce (aveva un solo chiamante) e l'export non ha bisogno di un formatter durata invariant in Core; (2) **nessun backfill** — le durate legacy senza unità restano legali e si sistemano a mano da Configura, quindi **il ramo "senza unità" resta vivo nel codice, non è morto**; (3) il builder **blocca** come già fa per le scale (`HabitBuilder.LaunchHint` prometteva già questa regola e mentiva: il gate controllava solo le scale); (4) Configura **preseleziona minuti**, perché autosalva e non ha un Salva da bloccare — conservando però un'unità temporale già scelta. Invariante in Core (`SecondsDisplay.TimeUnitCodes`), **non** nel service né a schema: validare lì è incompatibile col "nessun backfill" (ogni `UpdateAsync` su dati vecchi diventerebbe un errore). Commit `7f628c3`, `72d406a`, `fa6ce92`, `6741fb1`. **La review finale ha trovato un Critical da perdita dati** (`1d96967`): `RebuildUnits()` svuotava una `ItemsSource` viva e bindata, **riaprendo dentro la riga la coercizione-a-null di `4d01f9c`** — e non solo sulle durate (un cambio `Decimal`→`Integer` cancellava i litri). La nota del piano che dichiarava sicuro quel `Clear()` era sbagliata: **la scrittura all'indietro avviene al momento del `Clear()`, ripopolare dopo non la annulla** — è esattamente ciò che dice il messaggio di `4d01f9c`. Lezione operativa: *mai svuotare una collection che è `ItemsSource` di un controllo bindato two-way senza sopprimere la persistenza per tutta la durata dell'operazione e ri-affermare il valore dopo*. Suite **Core 113 / Data 430 / WPF 844** (+ ExportEngine 37 / Plugins 66 / ChartEngine 18) verdi. Nuovo finding a tracker: **R7** (il template seed "Workout log" produce ancora una durata senza unità; serve una migration data-only, e `SaveAsTemplateAsync` non è protetto da `CanLaunch`).

**Progresso 2026-08-08 (R7 + A9 + merge dell'Onda 2.3)** — **R7** (`bc23e5a`): nessun template spedito può più creare una durata senza unità. Il seed "Workout log" dichiara `"unit":"minutes"`; migration data-only **`WorkoutLogUnitV216`** (schema **2.16**); `SaveAsTemplateAsync` protetto da `CanLaunch` — con guard **anche dentro il metodo**, perché `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`, quindi il solo pulsante grigio non basta; un test di guardia percorre **tutti** i template ufficiali e fallisce su qualsiasi metrica `Seconds` senza unità temporale. Le abitudini già istanziate dal vecchio template conservano la metrica senza unità: sono dati utente, coperti dal "nessun backfill". **A9** (`eacf534`): rimossi 5 eventi + 1 comando senza ascoltatori, ognuno verificato prima di cancellarlo (un membro morto può essere impalcatura *o* una feature persa in un refactor). `ConfigureRequested`/`OpenConfigureCommand` erano ridondanti con `LoadEditorAsync` aggiunto da A1; `CompletionSaved` inutile perché la schermata si aggiorna da sé (B5); `SeriesChanged` doppione di `RaisePreviewRequested`; `EditRequested` superfluo mentre **il pulsante Modifica funziona** (è `IsEditing` ad aprire l'editor inline). La CTA "apri Configura" della card Goal era invece una lacuna d'interfaccia vera → promossa a **R8** invece di sparire col suo codice morto. **Merge**: `wave-2-3-registration` → `main` in **fast-forward** (`eacf534`), branch cancellato, suite verificate prima *e* dopo (**Core 113 / Data 432 / WPF 842 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = 1508**). **Decisione dell'autore**: l'Onda 2 si costruisce per intero (2.4 U7 → 2.5 builder) e si verifica a schermo **una volta sola** a valle della 2.5 — è ciò che §8 della spec per-tipo già chiede; conseguenza: **2.2 e 2.3 sono in `main` senza essere mai state guardate**. `main` è ~107 commit avanti a `origin/main` e non è pushato (scelta, non svista).

**Progresso 2026-08-08 (Onda 2.4 — U7 food diary note-first, schema 2.17)** — chiuso **U7** in sette task (spec/piano `2026-08-08-wave-2-4-food-diary*`, branch `wave-2-4-food-diary`). Decisione di modello: **la nota veloce è una voce di diario, non un tipo a parte** — niente sesto `habit_type`; il gesto primario (`Diary` vs `QuickNote`) vive in `ui_prefs_json` (`journalGesture`, previsto dal modello tipizzato di Onda 2.0 ma rimasto inerte finché non aveva un consumer reale) e cambia la **presentazione** della colonna ✎ Diario, non l'identità dell'habit. Il payload template guadagna una sezione `ui_prefs` (`2cf89e3`), applicata via `IHabitUiPrefsService` dopo la creazione del nodo. Il seed "Food diary" perde le tre metriche fantasma (`f126ff3` — un Journal non ha superficie per inserirle, gliele promettevano senza poterle offrire) e dichiara `journal_gesture: QuickNote`; "Daily journal" dichiara `Diary` esplicitamente — migration data-only **`FoodDiaryQuickNoteV217`**, **schema 2.17**, con un test di guardia (gemello di quello di R7) che percorre tutti i template ufficiali e blocca qualsiasi journal con metriche. **Le abitudini già istanziate dal vecchio template conservano le tre metriche fantasma**: dati utente, nessun backfill. Il gesto si cambia da Configura → sezione Registrazione, ora Counter-o-Journal (`86e9a79`). Nuovo composer nota veloce (`JournalQuickEntryViewModel`/`JournalEntryRowViewModel`, `1a06ae8`): foto tenute in attesa finché la nota non esiste — allegarle prima orfanerebbe una bozza — lista voci del giorno, edit inline, elimina con conferma. La colonna ✎ Diario (`50c19ad`) ospita **il composer o l'editor Markdown, mai entrambi**: WebView2 non viene nemmeno istanziato per un journal `QuickNote`; un journal senza preferenza resta sull'editor. **Bug preesistente scoperto e fixato** (`4d2e4c4`+`20741e3`): `HabitBuilderViewModel` scriveva `repeatable`/`counts_occurrences`/`mode`/`metrics[].sort_order` alla radice del payload, ma `HabitTemplateService` legge `repeatable` da `config.repeatable` e ignora gli altri tre — salvare una composizione ripetibile come template e ristanziarla la restituiva silenziosamente non-ripetibile; il payload del builder ora rispecchia la forma letta dal servizio, e porta anche il gesto journal. Suite **Core 113 / Data 436 / WPF 861 / Plugins 66 / ExportEngine 37 / ChartEngine 18**. **Niente verifica visiva in questa onda** — l'autore guarda l'intera Onda 2 una volta sola dopo la 2.5, e a quel pass resta parcheggiato anche il binding placeholder **inerte** del composer (`JournalQuickEntryView.xaml` → `Strings.Diario_QuickEntry_Placeholder` sul `TextBox.Tag`, senza meccanismo Tag-driven in `BaseStyles.xaml`, stesso pattern morto di `EditorPane.xaml:62`).

**Review finale dell'Onda 2.4 (2026-08-09/10) — due Critical, e il branch NON è mergiato.** La review sull'intero branch (opus, `5d9667a..3925a9e`) ha trovato due difetti che le sette review per-task, ciascuna cieca fuori dal proprio diff, non potevano vedere. **C1 — il WebView2 si istanziava lo stesso** (`1a97aa8`): la mutua esclusione della colonna Diario era garantita solo dal VM (uno dei due binding null), ma **un `ContentControl` con `ContentTemplate` esplicito applica il template anche con `Content` null** — `ContentPresenter.ChooseTemplate()` non controlla il contenuto. Su ogni Food diary partiva quindi un processo browser e, essendo `WebView2` un `HwndHost` che WPF non può sovradisegnare, l'editor fantasma avrebbe coperto il composer. Chiuso con un `ContentTemplateSelector` (`SelectTemplate` torna null per contenuto non-Journal), non col guard su `Visibility` che la review prescriveva: l'implementatore ha smontato quella prescrizione con una sonda propria. **Regola generale per questo repo: un `ContentControl` con `ContentTemplate` statico va sempre accoppiato a un selector o a un guard, mai al solo binding null.** **C2 — due sezioni di Configura si annullavano a vicenda** (`16f6ffb`): `RegistrationSectionViewModel` e `HomeRowSectionViewModel` scrivevano entrambe `ui_prefs_json` da uno snapshot dell'intero record preso al `LoadAsync`, quindi cambiare cifra chiave e poi gesto riportava indietro la prima — e Configura autosalva, quindi la perdita si vedeva solo al reload successivo. Nuova API di merge `IHabitUiPrefsService.UpdateAsync(id, mutate, ct)` che rilegge **dentro** la scrittura; tutti e quattro i writer (due sezioni, `PersistSubjectColumnsAsync`, istanziazione template) ci passano — il read-modify-write non è più riarmabile dalla prossima sezione che vorrà una manopola lì. Più tre Important: il gesto del builder ora si applica anche a **Lancia** (`21038f6`, decisione dell'autore contro il piano, che lo lasciava a Configura), reentrancy guard su `SetDay`/`ReloadEntriesAsync` (`f15a8bc`), e lettura di compatibilità per i `repeatable` alla radice dei template salvati prima di quest'onda (`17eca02`). Suite finali **Core 113 / Data 440 / WPF 872 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = 1546**. **Residui parcheggiati** (nessun secondo giro di fix): il gesto del builder si scrive *dopo* `CreatedHabitNodeId`, che ha già chiuso il dialog — un errore transitorio uscirebbe da un comando già riuscito (fix da una riga: scrivere le prefs prima); il test di regressione dell'interleaving non fallisce contro il codice pre-fix (rilegge `_day` dopo l'await invece di catturarlo); i due test su `IsSaving` asseriscono solo `false→false`. **Domanda aperta più grossa dei residui**: `NotesTabView` ospita un secondo `MarkdownEditorControl` semplicemente `Collapsed` — se la sonda dell'implementatore su `Collapsed` è giusta, quell'editor si istanzia per **ogni** abitudine e la promessa "niente WebView2" è falsa comunque; si adjudica allargando `FindDescendants<MarkdownEditorControl>` dal `DiarioColumn` all'intera view col Note chiuso, in `HabitScreenDiarioColumnViewTests`. **Stato del branch**: **mergiato in `main` in fast-forward il 2026-08-11** (branch cancellato), dopo suite completa ri-verificata sulla VM — v. progresso 2026-08-10/11.

**Progresso 2026-08-10/11 (revisione della mappa v1.0 + merge 2.4)** — punto della situazione con l'autore → **revisione approvata della mappa** (`2026-07-03-v1-redefinition-plan-design.md`, nuovo **§1-bis**): **Onda 5 riconfermata intera** ma il suo ordine interno si decide dopo ≥2 settimane di dogfooding; **dogfooding = nuovo E21** (parte subito dopo il pass visivo dell'Onda 2, con disciplina dati scritta: solo build di `main`, backup prima di ogni build nuova, migrazioni provate su copia); **E14 anticipato** (motore notifiche minimo — toast ad app aperta/tray — prerequisito del dogfooding, fuori dall'Onda 4); la **2.5 ha mandato doppio**: anteprima viva (E7) **+ test round-trip di parità template↔builder** (automatizza E16, gemello del guard R7); push di `main` su `origin` parcheggiato con scadenza inizio Onda 4. **Residui 2.4 (W24-R1/R2/R3, W24-Q1) e questioni-modello counter (CM-1…CM-4) trascritti nel tracker** — prima vivevano solo nella prosa di questo file e in una memoria machine-local che Opus/Sonnet non vedono. **Suite completa ri-verificata prima del merge**: Core 113 / Data 440 / WPF 872 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = **1546 verdi**. Gotcha ambiente (W24-E1 a tracker): il run WPF **parallelo** può appendersi su questa VM (due hang riprodotti a zero fail; sonda WebView2 + smoke/E2E concorrenti; run serializzato `-- xUnit.ParallelizeTestCollections=false` = 872/872), e il lock `CreateAppHost` di DriveFS conferma l'obbligo di `-p:UseAppHost=false`. **Prossima: Onda 2.5** (ultimo pezzo dell'Onda 2), poi pass visivo unico, E14, ★ dogfooding.

**Progresso 2026-08-12 (Onda 2.5 — il builder compone tutto, e ora è dimostrato)** — chiusa sul branch `wave-2-5-builder` (11 task, piano `2026-08-11-wave-2-5-builder.md`). **Nessuna migrazione: lo schema resta 2.17.** Il builder guadagna i mattoni che gli mancavano — **Programma** (schedule/streak/recovery), **Obiettivo**, **Sessione** (timer integrato + segmenti), **serie nominate** sotto il flag Ripetibile, e **unità/scale custom staged** create nei micro-form senza uscire dal builder (Guid pre-assegnati, materializzate da Lancia nella stessa transazione o incorporate nel payload, che `InstantiateAsync` **risolve-o-crea** per Code: ristanziare lo stesso template non moltiplica "tazze, tazze-2"). Il payload template diventa **v2**, letto dalla stessa `HabitConfig` che `BuildConfig` produce — le due strade non possono divergere. **Anteprima a 3 piani (E7)**: riga Home col `HeatmapStrip` vero e stile per-tipo da `HomeRowPresentation`, fascia "Oggi" col **componente reale** alimentato a mano da stub inerti (mai `LoadAsync`; il clock astinenza si imposta, non si `Start()`a), riquadro Registrazione a modello visivo minimo — scelta deliberata: i DataTemplate quick-add veri vivono dentro le risorse di `HabitScreen.xaml` e estrarne ~500 righe dalla schermata non ancora verificata a schermo comprava una regressione, e così "niente WebView2 nell'anteprima" è vero **per costruzione**.

**Il mandato doppio è servito subito.** Il **guard di parità template↔builder** (E16 automatizzato, gemello di R7: copertura di tutti i seed + confronto semantico dei payload + whitelist delle chiavi note, così una capacità nuova nei template non può passare semplicemente non essendo confrontata) ha trovato **al primo run** un template ufficiale che l'utente non poteva ricostruire: "Workout log" dichiara `Weight` come `Decimal` **senza unità**, mentre nel builder una Quantità senza unità derivava sempre `Integer`. Chiuso col mattone mancante (`FieldBlockViewModel.AllowsDecimals`), non aggiustando la fixture. Un secondo test E2E fa il round-trip **vero** su DB cifrato: componi → Lancia → salva come template → ristanzia → le due abitudini devono essere indistinguibili riga per riga.

**Due residui dell'Onda 2.4 chiusi per strada.** **W24-R1** `fixed`: il gesto journal viaggia ora dentro `ComposedHabitRequest.UiPrefs`, serializzato sul nodo **prima** dell'insert, invece di essere scritto dopo che `CreatedHabitNodeId` ha già chiuso il dialog. **W24-Q1** `verified-fixed` e **l'esito era quello brutto**: il test allargato all'intera `HabitScreen` col pannello Note **chiuso** è nato **rosso** — l'editor Markdown del Note si istanziava comunque, quindi la promessa "niente WebView2 per un Food diary" era **falsa** anche dopo il fix C1, che copriva solo la colonna Diario. Chiuso con la stessa cura ma sul Content: `NotesForColumn` torna il VM solo mentre la colonna è aperta. **Lezione, ora dimostrata due volte: `Visibility="Collapsed"` non impedisce la materializzazione di un template — solo un `Content` null (o un selector) lo fa.**

**Review finale di branch (2026-08-12) — sei findings, due alti, tutti chiusi** (`26c487c`). Il peggiore: un goal **`Fixed` o `Rolling` violava `ck_goal_period_consistency`** perché `GoalBrickViewModel.Build` non scriveva mai `period_start`/`period_end`/`rolling_days` mentre la ComboBox offriva tutti e tre i periodi — Lancia moriva su una violazione di constraint **senza try/catch e senza superficie d'errore** (il pulsante sembrava non fare nulla) e lo stesso payload istanziato da template lasciava un'**abitudine orfana senza config**, perché l'eccezione arrivava dopo il commit del nodo. Il builder compone ora solo goal aperti, l'unica forma che i template ufficiali usano; il periodo resta a Configura, che ha i campi e la validazione. Secondo alto: il builder era **l'unico writer che saltava `ValidateCrossFields`** (`ComposedHabitService` scrive la config dritta sul DbSet), quindi "volte a settimana"/"ogni X giorni" col campo vuoto creava abitudini su cui esplodeva ogni modifica successiva, e "giorni specifici" senza giorni ne creava una **mai programmata in silenzio**; un solo gate `Schedule.IsComplete` chiude tutte e tre le strade su Lancia e su Salva-come-template. Più tre minori: la spunta "Decimali" copriva la freccia del picker unità, `IsHitTestVisible` sullo ScrollViewer rendeva irraggiungibile il fondo dell'anteprima, e rimuovere un campo lasciava aperto il suo micro-form puntato su un VM staccato.

**Chiusi anche i due residui-test della 2.4.** **W24-R2** non era solo debito di test: `ReloadEntriesAsync` **rileggeva `_day` dopo l'await**, quindi un reload superato si ri-puntava sul giorno diventato corrente e sembrava corretto — difetto vero, oltre che la ragione per cui il test non discriminava. **W24-R3**: `FakeDiaryService.NextWriteDelay` rende osservabile lo stato in volo, e i test asseriscono `false → true → false`. Entrambi **verificati rompendo il codice**: disattivati i tre generation-guard e l'assegnazione `IsSaving = true`, i tre test falliscono; ripristinati, tornano verdi.

**Nuovo finding a tracker: W25-2** — il template ufficiale **"Workout log" crea un'abitudine mai programmata** (dichiara `SpecificDays` senza dire quali giorni, e `MatchesDayMask(null)` è false per ogni data). È il seed a essere difettoso, non il builder — che ora quella composizione la rifiuta. Fix = migration data-only, fuori da quest'onda; il gate naturale è un test-guardia gemello di R7.

Suite **Core 118 / Data 445 / WPF 988 / Plugins 66 / ExportEngine 37 / ChartEngine 18 = 1672** verdi (WPF serializzato, W24-E1). **Mergiata in `main` in fast-forward il 2026-08-12** (`14cbb56`), branch cancellato. *Gotcha del mount*: il merge è stato rifiutato una prima volta per una modifica **fantasma** a `docs/phase-4-issues.md` — worktree, indice e HEAD avevano lo **stesso hash**; è la cache di stato di git su DriveFS. Si sblocca con `git checkout -- <file>` (no-op sul contenuto). **Prossima: pass visivo unico dell'Onda 2** (checklist `2026-08-11-wave-2-visual-pass-checklist.md`, sezione 6 confermata a fine esecuzione) → E14 → ★ dogfooding.

**Progresso 2026-08-14→17 (pass visivo dell'Onda 2, in corso + mini-giro di fix)** — venti findings **`W2V-1…W2V-20`** nel tracker. Metodo che funziona con questo autore: lui naviga e manda screenshot, io **verifico ogni sospetto nel codice prima di segnarlo** (due volte un sospetto è morto alla verifica). **Chiusi in positivo**: lo sfarfallio dell'anteprima del builder non esiste, Food diary apre il composer e non l'editor Markdown (C1 e W24-Q1 tengono **in esecuzione**), lo staging di unità/scale non sporca il catalogo su Annulla. **Mini-giro anticipato** sui due findings che avvelenerebbero il dogfooding: **W2V-1 + W2V-17c** (`e8bd480` — `CultureBootstrap` + `NumericInputRule`) e **W2V-6** (`abstinence.started/.lapsed/.period.deleted` con un solo `AbstinenceChangedPayload`; i tre guard restano muti, un test lo blocca). Poi **selettore di cultura anticipato su richiesta dell'autore** (`d14b49f`, `d5a75c4`): due manopole distinte — lingua UI e formato regionale, come le separa Windows — in **Generale**, non in Aspetto (la cultura è comportamento, non aspetto), con **anteprima viva** al posto dell'esempio criptico. Chiude il **selettore** di F0-6; restano il default EN e il seeder demo. Suite **Core 118 / Data 449 / WPF 1022 / Plugins 66 / ExportEngine 37 / ChartEngine 18**. **Resta da guardare**: §3 registrazione (il blocco grosso — tap counter, somma, layout, cronometro, export), §1 Configura "Riga Home", §2 colonne default e memoria per-abitudine, §4 journal, più i quattro residui del builder (round-trip istanziando due volte, serie nominate, ricostruzione di Workout log, journal `Diary` senza WebView2). Branch **`wave-2-visual-pass`**, non mergiato. *Ambiente*: W24-E1 (hang della suite WPF) si è ripresentato **anche su un run filtrato** — rilanciare, non indagare.

**Progresso 2026-08-19 (due brainstorm, zero codice) — e un difetto di correttezza trovato per strada.** Sessione di sola progettazione: due spec approvate e committate su `wave-2-visual-pass`, nessun file di `src/` toccato.

1. **Modalità Calendario** (`docs/superpowers/specs/2026-08-19-calendar-lens-design.md`, `cefbb26`) — idea dell'autore: calendari per attività (videogiochi, film, libri) con voci per giorno, colonne a destra "Lista" e "Inseriti", vista aggregata. Decisione di modello: **un calendario è un'abitudine vista con un'altra lente**, non un oggetto parallelo — counter ripetibile, la voce di giornata è una `Completion`, l'aggregato è **un nodo gruppo selezionato** (i gruppi che l'autore chiedeva esistono già). Unico mattone nuovo: `catalog_item` (voci con identità propria + tre stati fissi `Todo`/`InProgress`/`Done`), referenziata da un campo-soggetto `MetricType.Item` — l'unico punto non additivo è la **ricostruzione di `metric_definition`** per il CHECK sul tipo, accettata contro l'alternativa implicita. Layout scelto a mockup (companion visuale). **Fuori dal freeze v1.0**: candidata al riordino dell'**Onda 5** post-dogfooding, nessun piano.
2. **R9 — il giorno di appartenenza** (`docs/superpowers/archive/specs/2026-08-19-day-boundary-design.md`, `1005d6f`) — **da eseguire prima di E14**, quindi prima del dogfooding. Il tracker diceva "`reset_time` non lo consuma nessuno"; verificando il codice il problema è più grosso: le registrazioni si stampigliano `clock.UtcNow.UtcDateTime` e si bucketizzano per **data UTC**, quindi in Italia d'estate **una registrazione delle 00:30 del 19 finisce nel 18** — in streak, heatmap, calendario, andamento ed export. Il sistema è *coerente* in UTC, ed è per questo che non ha mai prodotto risultati palesemente assurdi. In più, su **55 siti** che derivano un giorno da un istante, **14 calcolano "oggi" in UTC e 9 in locale**: fra mezzanotte e le 02:00 la striscia "Oggi" e il calendario indicano D mentre i dati atterrano su D−1 (**R10 era un campione di questa famiglia, non un caso isolato**). Decisioni: un solo risolutore (locale + cascata `reset_time`) come **valore risolto una volta + funzione pura in Core** (`DayBoundary`), con **versione batch** per le aggregazioni multi-abitudine o l'albero Home torna a N query; retroattive nuove a **mezzogiorno locale**; **backfill** delle sole retroattive esistenti come passo di manutenzione una tantum allo startup, **non** una migration (`datetime(…,'localtime')` di SQLite è inaffidabile sulle date passate). **Nessun cambio di schema**: si continua a salvare istanti, il giorno resta un'interpretazione. Rischio da dire prima, non da scoprire: passando a locale **lo storico si ri-interpreta** e un numero di streak può cambiare al primo avvio.

**Ordine aggiornato**: pass visivo Onda 2 (in corso) → **R9** → E14 → ★ dogfooding. Prossimo passo concreto: **il piano di implementazione di R9** (`writing-plans`), il cui primo task è l'**audit dei 55 siti** — ognuno etichettato *converti* / *è presentazione* / *è già dominio*, perché è l'audit a decidere la dimensione vera del lavoro.


**Progresso 2026-09-09 (W25-2 — il template che non scadeva mai).** Migration data-only **`WorkoutLogWeeklyTargetV218`**, schema **2.18**: *"Workout log"* dichiarava `SpecificDays` **senza dire quali giorni**, e `MatchesDayMask(date, null)` è false per ogni data — l'abitudine istanziata dal template ufficiale non scadeva mai e la sua streak restava a zero per sempre, su ogni installazione. Ora dichiara `WeeklyX` con target 3, l'unica lettura coerente con lo `streak_mode: Weekly` che il payload già portava. **Decisione dell'autore** contro l'alternativa "giorni fissi lun/mer/ven": un template spedito indovinerebbe la settimana di uno sconosciuto, e il principio 8 dice che il template dà il mattone, non la settimana altrui. *Chiarimento di modello utile oltre questo caso*: lo `schedule_type` **non tocca la correlabilità dei dati** — statistiche e incroci leggono le completions e i loro istanti; il programma decide soltanto in quali giorni l'abitudine è attesa e come si calcola la streak. Guard gemello di quello di R7 (`No_shipped_template_ships_a_schedule_that_never_comes_due`, provato rosso contro il seed vecchio) più il test specifico; aggiornata la fixture di `BuilderTemplateParityTests`, che fin qui **documentava** il difetto invece di impedirlo. Le abitudini già istanziate restano non programmate: dati utente, "nessun backfill". Suite su Linux: **Core 143 / Data 478 (+18 DPAPI) / Plugins 66 / ExportEngine 37**.

**Progresso 2026-09-08/09 (R9 — il giorno di appartenenza, chiuso; e il DB era in chiaro).** Branch `r9-day-boundary` (da `wave-2-visual-pass`), piano `2026-09-07-r9-day-boundary.md`, 12 task. **Nessuna migration, schema fermo a 2.17**: i dati restano istanti, il giorno resta un'interpretazione. Un valore puro `DayBoundary` in Core (`DayOf`/`Today`/`StartOf`/`NoonOf`/`RangeOf`) prodotto da `IResetTimeResolver` — che prima **non aveva un solo chiamante** — si risolve una volta in cima all'operazione e scende: i servizi Data se lo risolvono da soli (firme pubbliche invariate, ~20 call site intatti), i VM lo ricevono da chi carica la schermata. Convertiti **58 siti** che derivavano un giorno da un istante e **~20** che costruivano finestre di query a mezzanotte UTC — convertire solo i primi avrebbe dato bucket giusti che pescano da finestre sfalsate. **`reset_time` ha finalmente un consumatore** (era configurabile in due punti d'interfaccia e inerte, come F0-14 per le notifiche); **R10 chiuso per costruzione**. Retroattive nuove a **mezzogiorno locale**, ed **`RetroactiveNoonBackfill`** ri-stampiglia una tantum quelle già in archivio (idempotente, nell'initializer, guardato da un AppSetting). **Rischio dichiarato**: lo storico si ri-interpreta, un numero di streak **può cambiare al primo avvio** — è corretto, non è una regressione.

*Trovato di rimbalzo, ed è la cosa più grave di questa sessione*: **il database non era più cifrato** (SEC-1, `feae26b`). `Recordum.Data` referenziava `Microsoft.EntityFrameworkCore.Sqlite` — il pacchetto completo — che trascina `bundle_e_sqlite3`; quel bundle e `bundle_e_sqlcipher` spediscono **lo stesso nome di assembly**, ne sopravvive uno solo, e quando vince quello in chiaro `PRAGMA key` diventa un **no-op silenzioso**. Nessun commit l'ha rotto: è uscita la 2.1.12 del pacchetto transitivo e ha scavalcato il nostro 2.1.11 sotto un wildcard di versione. Fix: `.Sqlite.Core`, in `src/Recordum.Data` **e** nel progetto di test; guard `SqlCipherProviderGuardTests` (`PRAGMA cipher_version`). *Lezione*: un rosso che sembra ambiente va provato, non archiviato — bastava uno `git stash` per vederlo fallire su `HEAD` pulito.

*Il WIP ereditato compilava ed era muto in tre punti*: `TrendChartViewModel`, `NotesViewModel` e `AbstinenceQuickAddViewModel` ricevevano il `DayBoundary` e **non lo assegnavano al campo** — firma giusta, chiamante giusto, corpo che butta via il valore, zero warning. Più A39 (il calendario si apriva sul giorno dell'ora locale nuda) e B11/B12 non convertiti. Sono stati trovati **leggendo**, non compilando: è la ragione per cui il piano pretende un caso di test dove il default risponde diverso.

*Deviazione dal piano, con la sua ragione*: il Task 11 voleva la chiave di guard dentro `AppSettingSeed.All` dichiarandola "dati, non schema, nessuna migration". È falso per EF — quel registro è `HasData`, cioè modello, e aggiungerci una riga fa fallire `MigrateAsync` con `PendingModelChangesWarning` (38 test rossi, misurati). Siccome R9 vieta le migration, la chiave nasce alla prima scrittura e "assente" vale "da fare".

*La review di branch ha trovato due Critical che le review per-task non potevano vedere*, ed è la ragione per cui su questo repo si fa. **C1**: `StateTodayContent` riceveva il `DayBoundary` e **non lo assegnava** — il quarto caso della stessa famiglia dei tre del WIP, e l'unico che arriva ai dati, perché `RecordAsync` fa last-wins cancellando le registrazioni nella finestra del giorno: con la finestra in UTC, alle 00:30 del 19 cancellava il valore delle 20:00 del **18**. Era passato perché `TodayStripDayBoundaryTests` copriva Action, Resistance, Counter e riga Home — **non State**: il buco stava nel test. **C2**: `RangeOf` esplodeva su `DateOnly.MinValue` (regressione *introdotta* da R9) — `AbstinencePeriodListViewModel` passa `from ?? DateOnly.MinValue` per "tutte le registrazioni", e `StartOf(0001-01-01)` in un fuso a offset **positivo** costruisce un istante che in UTC cade nell'anno 0; su un'abitudine Resistance bastava aprire Registrazioni → "Tutto". Invisibile alla suite perché l'unico test che passa `MinValue` gira con `FakeClock` a fuso **UTC**, dove l'offset è zero. Più **I1**: la Panoramica di un gruppo riceveva il confine del soggetto precedente, perché `_dayBoundary` si risolveva *dopo* il ramo gruppo. Tutti e tre chiusi (`e04bff3`) con i rispettivi test; sei minori a tracker con la loro ragione, incluso il fatto che il backfill **non è idempotente a UTC+13/+14**. *Lezione*: §0 del piano non elencava `PanoramicaViewModel` né la sentinella `MinValue` di B4 — erano buchi dell'**audit**, non solo dell'esecuzione, e la tabella è stata corretta.

*Ambiente*: questa sessione è girata su **Linux** (repo clonato in `~/dev/recordum`, `.NET 10.0.401` in `~/.dotnet`). Lì build completa con `-p:EnableWindowsTargeting=true`, e **Core, Data, Plugins ed ExportEngine girano**; **WPF no** — serve `Microsoft.WindowsDesktop.App`, che su Linux non esiste — e ChartEngine nemmeno (`net10.0-windows`). Conteggi su Linux a fine sessione: **Core 145 / Data 478 (+18 DPAPI, Windows-only per natura) / Plugins 66 / ExportEngine 37**. *(Il `-p:RuntimeIdentifier=linux-x64` a mano e i due rossi di ExportEngine, che questo paragrafo riportava, sono stati eliminati nella stessa sessione: v. il progresso sull'ambiente qui sotto.)* ~~Restano da eseguire sulla VM: l'intera suite WPF, gli step "rosso-prima" dei task WPF~~ → **fatto sulla VM il 2026-09-11** (paragrafo seguente); merge in `main` fatto il 2026-09-25 (`911ed24`). Resta la **verifica a schermo**, spostata su Avalonia (F2).
**Progresso 2026-09-11 (VM — la parte di R9 che Linux non poteva eseguire).** Suite complete sulla VM: **Core 145 / Data 496 (DPAPI inclusi) / WPF 1044 / Plugins 66 / ExportEngine 37 / ChartEngine 18**, tutte verdi. Al primo run WPF un solo rosso, `State_reads_todays_value_through_the_reset_time_it_is_given`, ed era la **fixture**: la scala finta senza `MinValue`/`MaxValue` né etichette dava un solo gradino, e il `Clamp` schiacciava a 0 un valore letto correttamente (`23be772`). Poi **14 sonde "rosso prima"** sui siti WPF di R9, ognuna una rottura isolata ripristinata con `git checkout`: le tre prescritte dal piano (Task 8, 9, 10), I1, C1 e i siti che ricevono il confine. Dodici rosse sul test atteso; **due verdi, e lì mancava copertura**: il `_day` di `DetailViewModel` serve solo ai reload scatenati dal bus, e quello di `CompletionRowViewModel` solo alla modifica inline dell'orario — togliere l'assegnazione non faceva fallire niente. Chiuse con due test in `DetailDayBoundaryTests`, provati rossi a loro volta. Dettaglio in `docs/phase-4-issues.md` §"Verifica sulla VM di R9". *Gotcha*: in `QuickAddViewModelTests.cs` la classe si chiama `QuickAddViewModelBaseTests`, e un `--filter ~QuickAddViewModelTests` la salta — una sonda sembrava verde perché i suoi test non venivano eseguiti.

**Progresso 2026-09-09 (la versione Linux — decisione, spec, piano, e le prime due fette).** Decisione dell'autore, presa consapevolmente **contro il freeze del 2026-06-16**: Recordum si descrive *cross-platform* e spedisce solo su Windows, e un utente Linux deve poterlo installare. Modo di lavorare concordato: **quando la sessione gira su Linux si porta avanti questa linea**, con leggera priorità al codice della v1.0 quando c'è qualcosa da chiudere; la VM resta la macchina della v1.0, questa diventa la macchina della portabilità. Sul **sync non si apre un fronte**: il ponte dati è `.recbak`, come la roadmap già prevede per la Fase 5 mobile.

**La misura che rende il progetto plausibile**, e che nessuno aveva mai verificato: su **126 file di ViewModel (12.300 righe) gli agganci reali a WPF sono sei righe in sei file** — quattro `DispatcherTimer` (che Avalonia ha con lo stesso nome, in `Avalonia.Threading`), un `Application.Current?.Shutdown()`, un `TryFindResource` per un colore del tema. Più un **settimo** trovato scrivendo il piano: `HomeViewModel.ApplyDropAsync` prende un `TreeDragDropBehavior.DropPlan`, cioè un tipo del progetto WPF — il grep iniziale cercava tipi WPF *dentro* i ViewModel, non tipi WPF *usati* dai ViewModel. La promessa di §4 ("Core/Data/Plugins riusati senza modifiche") è stata **verificata per la prima volta su una macchina non-Windows**: quei progetti compilano e i loro test passano su Fedora, SQLCipher incluso.

**Scelte** (spec `2026-09-09-avalonia-linux-slice-design.md`, piano `2026-09-09-avalonia-linux-slice.md`, branch `avalonia-linux-slice`): **Avalonia** contro Uno (due traduzioni invece di una), Blazor/PWA (cambia il prodotto e butta via anche i ViewModel), MAUI (non fa desktop Linux), GTK/Electron (riscrittura). *(Poi superato dall'onda F0: i ViewModel sono stati spostati davvero in `Recordum.Presentation`.)* I ViewModel si **collegano come sorgenti** (`<Compile Include>`) invece di essere spostati: l'estrazione di un vero `Recordum.Presentation` è l'**esito** della fetta, non la premessa, perché spostare 126 file citati da 872 test dentro il branch che deve diventare v1.0 renderebbe impossibile capire quale delle due cose ha rotto cosa. **Regola di convivenza**: finché la fetta è esplorativa, le feature nuove si fanno **solo su WPF** e Avalonia insegue.

**Due fatti tecnici che valgono oltre la fetta.** (1) Sull'editor markdown la versione Linux **non porta WebView2, lo elimina** (`Avalonia.AvaloniaEdit` + `Markdown.Avalonia`): via la dipendenza che ha già prodotto C1 dell'Onda 2.4 e W24-Q1 della 2.5. (2) `Avalonia.Headless.XUnit` rende i test d'interfaccia eseguibili su Linux, e la **sonda screenshot è riuscita**: un `[AvaloniaFact]` renderizza con Skia e salva un PNG vero (verificato aprendolo), quindi una schermata si può **guardare** senza che sia l'autore a mandarne una — con il limite, registrato in review, che un assert sul peso del file non certifica *cosa* mostra l'immagine.

**Stato**: Task 1 e 2 del piano chiusi (scheletro + primo test headless; sonda PNG), ognuno con review e giro di fix. **I Task 3-9 NON si eseguono come scritti**: provavano "il riuso dei ViewModel è reale?", a cui i primi due hanno già risposto, mentre la decisione pendeva su altre incognite. Ledger in `.superpowers/sdd/2026-09-09-avalonia-linux-slice/progress.md`. *Gotcha di build ereditato da qui*: `Avalonia.Headless.XUnit` dipende da **xunit.v3**, non dal 2.9.3 del resto del repo, e un progetto v3 pretende un apphost — per questo `UseAppHost=false` è passato dalla riga di comando a `Directory.Build.props` (v. §8 → Build), dove è una property sovrascrivibile invece di una global property che vince sempre.

**Progresso 2026-09-09/10 (da "versione Linux" a "Avalonia come piattaforma" — quattro sonde, e il preventivo).** L'autore è arrivato alla conclusione che **Avalonia sarà comunque il framework unico** per tutte le piattaforme: non aggiunge Linux, **si mangia anche la Fase 5 (MAUI) e la futura Web/PWA**. Da qui la domanda che ha guidato la sessione — *"se ci devo arrivare, perché non farlo ora?"* — con una scadenza reale: **l'Onda 3 è coerenza estetica, cioè interfaccia pura**, quindi farla su WPF sapendo di migrare butterebbe via una fase intera. Prima di riscrivere spec e piano si è misurato il costo con quattro sonde, ognuna col proprio report in `docs/design/`:

| Incognita | Esito misurato | Report |
|---|---|---|
| Riuso dei ViewModel | **6 agganci a WPF** su 126 file / 12.300 righe (4 `DispatcherTimer` — che Avalonia ha con lo stesso nome —, uno `Shutdown()`, un `TryFindResource`). Un **settimo** scoperto scrivendo il piano: `HomeViewModel.ApplyDropAsync` prende un `TreeDragDropBehavior.DropPlan`, tipo del progetto WPF. **Tutti e sette chiusi in F0** (`ITickSource`/`ITickSourceFactory`, `IApplicationLifetime`, `MarkerTint`, `DropPlan`/`DropResolver`; più, scoperti eseguendo il Task 7, `IClipboard`, `IEditorAvailability` e `SubscribeOnUi` su `SynchronizationContext`) | — |
| **Stili** | delle 844 righe di `BaseStyles.xaml` attribuibili a un controllo, **~682 (81%) non hanno contropartita**: erano il costo di *disfare Aero*. Residuo reale scritto: **64 righe**. Sorpresa: sottolineatura dei tab, thumb dello slider, chevron dell'Expander, popup arrotondati **coincidono** col default Fluent — non erano scelte di Recordum, erano design moderno che WPF non aveva | `2026-09-09-avalonia-theme-probe.md` |
| **Controlli disegnati a mano** | **nessuno usa disegno immediato** (zero `OnRender`/`DrawingContext` in tutto `src/`): sono `ItemsControl`/`Canvas` con la geometria calcolata in C#. `HeatmapStrip`, il caso peggiore, passa da 190 a 224 righe (**+6%** al netto dei commenti); gli altri quattro sono porting diretti | `2026-09-09-avalonia-controls-probe.md` |
| Editor markdown | sostituibile senza WebView2. Requisiti veri letti da `editor.js`: Toast UI è in **WYSIWYG**, 13 comandi, **niente immagini** (le foto passano dal composer di U7). `Placeholder`/`IsReadOnly` sono **superficie morta**, nessun call site le imposta | `2026-09-09-avalonia-editor-probe.md` |
| **Esperienza di scrittura** | **Live Preview stile Obsidian raggiunta**: titoli/grassetto/corsivo resi mentre si scrive, marcatori nascosti, **grezzo sulla riga del cursore**. 358 righe; le costruzioni mancanti costano linearmente | `2026-09-10-avalonia-live-preview-probe.md` |

**Cosa NON transfer**: il chrome custom della finestra (`WindowChromeStyles.xaml`, 129 righe, API concettualmente diversa) e i tre widget a pillola. Il **motore dei grafici** non è un porting ma un'operazione architetturale: `Recordum.Plugins.ChartEngine` è `net10.0-windows`+`UseWPF`, mentre la mappatura delle serie è già priva di dipendenze WPF — va estratta in un progetto condiviso con due renderer sottili (il pacchetto `LiveChartsCore.SkiaSharpView.Avalonia` esiste). Il **runtime plugin della Fase 7** vuole un webview: su Avalonia significa CEF o i controlli per-OS. Con una correzione a una mia affermazione precedente: **non serve che la shell sia un webview perché i plugin lo siano** — WPF oggi fa esattamente questo con WebView2 dentro una colonna.

**Tre gotcha d'ambiente che varranno per tutto il porting**: (1) `Avalonia.Headless.XUnit` dipende da **xunit.v3**, non dal 2.9.3 del resto del repo, e un progetto v3 pretende un apphost — per questo `UseAppHost=false` è passato dalla riga di comando a `Directory.Build.props` (§8 → Build); (2) **in headless il primo `AvaloniaEdit` della sessione non fa il layout**, quindi lo screenshot esce vuoto **e il test passa lo stesso** se l'assert guarda solo il peso del file — trappola W24-R2/R3 in veste nuova, si rimedia primando con una vista *diversa*; (3) il progetto `Recordum.Avalonia` **fa ombra al namespace radice `Avalonia`** (serve `global::`) — **risolto in F0 Task 1**: rinominato `Recordum.UI`, con `NamespaceShadowingTests`.

**Stato**: spec e piano correnti (`2026-09-09-avalonia-linux-slice*`) sono **superati nello scope** — descrivono una fetta per Linux, non la migrazione di piattaforma. *(Riscritti: spec `2026-09-10-avalonia-as-the-platform-design.md` + piano F0; v. sotto.)* Branch `avalonia-linux-slice`, pushato, nessun merge previsto finché la v1.0 WPF non ha fatto il suo pass visivo.

**Progresso 2026-09-19/20 (onda F0 — le fondamenta della piattaforma: il progetto senza UI esiste, ed è provato).** Spec `2026-09-10-avalonia-as-the-platform-design.md` (nove onde F0–F8), piano `2026-09-10-wave-f0-presentation-extraction.md` (10 task, subagent-driven, ledger in `.superpowers/sdd/2026-09-10-wave-f0-presentation-extraction/`). **Chiusa sul branch `avalonia-linux-slice`; nessun merge, e la metà WPF non è mai stata eseguita** (v. sotto).

*Cosa esiste ora.* `Recordum.Presentation` (`net10.0`, niente `-windows`): 126 ViewModel, 504 stringhe, servizi portabili, bootstrap portabile. **916+ test di ViewModel girano su Linux** (prima: zero; erano tutti inchiodati a `net10.0-windows`). `Recordum.Avalonia` → `Recordum.UI` (Task 1). Il font di spedizione è **Source Serif 4** (scelta dell'autore, OFL 1.1, 4 TTF statici + `OFL.txt` in `src/Recordum.UI/Fonts/`; Semibold non spedito, quindi `FontWeight.SemiBold` — i titoli dell'editor — risolve sul Bold). `YearHeatmapControl`, `StreakTimelineControl` e il converter dell'intensità portati (Task 10).

*Le sette cuciture, tagliate prima di spostare*: `ITickSource`/`ITickSourceFactory` (quattro `DispatcherTimer` → un'astrazione in Core, il battito si inietta, e per la prima volta si prova un timer davvero), `IApplicationLifetime` (`Shutdown()`), `MarkerTint` (il VM dichiara un'**intenzione** di tinta, la view la risolve in pennello: fine di `Application.Current.TryFindResource` in un VM), `DropPlan`/`DropResolver`. Il Task 7 ne ha scoperte altre tre che il piano non conosceva: `IClipboard`, `IEditorAvailability` (il VM non deve sapere di WebView2), `SubscribeOnUi` (da `Dispatcher` a `SynchronizationContext`; 17 call site tracciati, nessuno raggiunge il ramo "senza contesto").

*Cosa ha insegnato l'esecuzione (vale oltre F0).*
- **La risalita nel namespace contenitore è cieca alla `sed`.** Un riferimento come `Formatting.CounterTotalDisplay` dentro `Recordum.WPF.ViewModels` funzionava perché C# risale al namespace padre; spezzato l'albero in due assembly non arriva più a destinazione, e nel testo non compare mai la stringa da sostituire. Si corregge con uno `using` esplicito, **l'elenco autorevole è l'output del compilatore, mai un `grep`** (un `new(...)` target-typed nasconde siti di costruzione).
- **Un test di guardia va visto scattare.** Quattro test inerti in fila, ciascuno prescritto dal piano, e un quinto scoperto solo dalla review finale **con una mutazione misurata**: cancellati entrambi i `_tick?.Stop()`, l'intera suite (916) restava verde — il ticker svuotava i callback *prima* di fermare la sorgente, quindi l'assert passava lo stesso. Ora i test osservano la sorgente (`FakeTickSourceFactory.RunningCount`). `NoWpfReferenceTests` asseriva su `TargetFrameworkAttribute.FrameworkName`, che vale `.NETCoreApp,Version=v10.0` **sia** per `net10.0` **sia** per `net10.0-windows`: il discriminante vero è `TargetPlatformAttribute`.
- **Il primo giro di test su Linux ha trovato un test che non poteva passare**: la guardia della perdita dati C1 di R9 (`TodayStripDayBoundaryTests.State_reads_todays_value…`) usava una scala finta con Min=Max=0 → slider clampato a 0, mai stato verde da quando fu scritta (i test WPF non giravano). Difetto del test, non della produzione; corretta la fixture e **provata rossa** rompendo `_day = day`.
- **Il glyph typeface di Avalonia riporta lo `Style`/`Weight` *richiesti* anche a file mancante**: la prima versione del test del font passava senza l'Italic. L'assert che scatta è `FontSimulations == None`.
- **Un timer che nasce in un costruttore va disposto da chi lo possiede**, e il DI del battito è una registrazione che compila anche se manca: `MainWindowViewModel` ha `IApplicationLifetime` obbligatorio con `ThrowIfNull` — se `ServiceRegistrar.cs:233` sparisse, l'app muore in `OnStartup`. Il gate è `BootstrapSmokeTests` (non ancora eseguito).

*Non consegnato, e non lo si finge*: (a) **il debito verso la VM** — `Recordum.WPF.Tests` (38 file) e l'intera R9 WPF non sono mai stati eseguiti; comando in `docs/dev-environment-sync.md` §"Handoff per la VM — onda F0" (prima `r9-day-boundary`, poi `avalonia-linux-slice`, così un rosso ha un colpevole). Il prerequisito del Task 7 (una corsa verde di R9 sulla VM) è stato **scavalcato su decisione esplicita dell'autore**: se R9 richiede correzioni, dovranno atterrare attraverso la rinomina di 126 file; (b) il **re-tint live del calendario al cambio tema non c'è** (`HabitColorTintConverter` legge `Brush.Danger` imperativamente dentro un `MultiBinding` le cui sorgenti non cambiano; nessuna sottoscrizione a `IThemeService.EffectiveThemeChanged`) — non è una regressione, ed è casa dell'Onda 3; (c) la shell `Recordum.UI` **non ha ancora un container DI**: quando lo avrà, `ITickSourceFactory`, `IApplicationLifetime`, `IEditorAvailability`, `IClipboard`, `IConfirmationDialog`, i dialog file e i launcher vogliono ciascuno il proprio gemello (oggi solo `ITickSourceFactory` ce l'ha).

*Difetto di prodotto scoperto per strada, pre-esistente e non toccato*: **la heatmap-anno si riempie per righe**. `YearHeatmapViewModel` emette 371 giorni consecutivi e `UniformGrid Rows=7 Columns=53` li dispone per riga: un anno reale si legge come 7 righe di giorni successivi, non come colonne di settimane stile GitHub (identico in WPF; probabile causa di A5). Il test del nuovo controllo lo **documenta** (`…fills_ROW_MAJOR`), così correggerlo — nel VM, emissione column-major, o con un altro pannello — farà scattare un test *consapevolmente*.

*Conteggi su Linux a fine onda*: **Core 150 / Presentation 918 / UI 27 / Plugins 66 / ExportEngine 37 / Data 478 (+18 DPAPI, Windows-only)**. WPF e ChartEngine si compilano soltanto.

**Progresso 2026-09-20 (piano dell'onda F1 — Home — scritto, NON eseguito).** Piano
[`docs/superpowers/plans/2026-09-20-wave-f1-home.md`](docs/superpowers/plans/2026-09-20-wave-f1-home.md),
13 task, dalla spec §6 riga F1: dopo l'esecuzione `Recordum.UI` dovrà aprire il DB cifrato vero, sbloccarlo
(DPAPI su Windows, passphrase Argon2id con cache altrove — nessuna strada esisteva ancora per crearne uno
nuovo), mostrare l'albero Home con le righe vive (tinta, rinomina, menu, drag & drop) dentro una finestra
col proprio chrome. Scritto da quattro autori in parallelo, ciascuno con codice **verificato per davvero**
in un worktree usa-e-getta (build+test rosso→verde) prima di essere trascritto in prosa; il controller ha
riconciliato i tre punti dove i blocchi si toccavano senza essersi visti (la firma vera di
`IDbUnlockService`, che vive in Core perché Data non può referenziare Presentation) e ha scritto
l'intestazione e il Task 13 di chiusura. **Tredici finding di prodotto già raccolti per il tracker** (da
scrivere lì al Task 13, a esecuzione fatta): due difetti già presenti in WPF scoperti portando il codice
fedelmente (un commento falso sulla verifica della chiave, cinque icone disegnate storte), il resto
conseguenze misurate di Avalonia 12 (niente fantasma nel drag & drop — non c'è `QueryContinueDrag`; il
chrome della finestra **ignorato da X11**, verificato solo su Linux, checklist a 7 punti pronta per la VM).

**Progresso 2026-09-23/24 (onda F1 in esecuzione — Task 1-5 di 13 chiusi, sessione sospesa a metà del Task 6).**
Esecuzione subagent-driven sul branch `avalonia-linux-slice` (pushato fino al Task 5, HEAD `df059b2`).
**Chiusi e rivisti**: T1 icone Lucide (58) + 5 convertitori; T2 stili globali + ritinta accento; T3 bootstrap
portabile (`AddRecordumCore`, `CoreInitializer`, `AppFontService`/`CultureService` spostati in Presentation);
T4 sblocco del DB (`CachingKeyProvider`, `DbUnlockService`, `IDbUnlockService` in **Core**, `UnlockViewModel`);
T5 servizi UI (tema, uscita, conferma) + dodici `Null*`. Split di modelli scelto dall'autore: implementatori
Haiku su T1/T2/T5 e Sonnet altrove, review Sonnet con **Opus su T4, T10 (chrome) e review finale**, controller
Sonnet. Le review hanno trovato difetti veri anche in questa onda (T4: nessun test falliva togliendo la
`ClearAllPools()` che impedisce a una passphrase errata di risultare valida; T5: quattro registrazioni DI mai
risolte da un test, e il dialog di conferma senza owner restava appeso alla X) — tutti chiusi con test provati
rossi. **Per riprendere**: il ledger locale (git-ignored, unica copia) è in
`.superpowers/sdd/2026-09-20-wave-f1-home/progress.md` — fidati di quello e di `git log`, non del contesto.
Il **lavoro parziale del Task 6** (righe Home: `HabitRow`, `GroupRow`, `HomeRowTemplate`,
`InlineRenameBehavior`, fixture `HomeFakes`, un test) **non è committato su `avalonia-linux-slice`**: l'agente
è stato fermato a metà, il test non compilava (manca `using Avalonia;`), quindi vive sul branch
**`wip/f1-task6`** (superato: il Task 6 è chiuso a `aaa676b`; un solo commit sopra `avalonia-linux-slice`). Per riprenderlo:
`git checkout avalonia-linux-slice && git merge --ff-only wip/f1-task6 && git reset --soft HEAD~1`
(i file tornano in staging, il branch resta pulito), poi si ri-dispatcha il Task 6 dicendo
all'implementatore di **ripartire da quei file** invece che da zero. Dal Task 7 in poi, come da piano.
Il tool `SendMessage` è *deferred*: caricarlo con ToolSearch prima di riprendere un implementatore.

**Revisione 2026-09-25 — leggila prima di riprendere il Task 6.** Revisione del percorso v1.0 su Avalonia fatta coi plugin
(feature-dev, pr-review-toolkit, ponytail, context7), ogni finding riverificato:
[`docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md`](docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md).
**Prima del Task 6 viene il Passo 0** (§6 della revisione): (a) ~~`main` costruisce un DB in chiaro~~ → **risolto il 2026-09-25**: l'autore
ha portato `main` in fast-forward a `911ed24` (R9 + SEC-1); i merge in `main` li esegue l'autore, perché da una sessione Claude
il classificatore della modalità automatica blocca l'aggiornamento di `main`; (b) `SQLitePCLRaw.bundle_e_sqlcipher` 2.1.11 è
**deprecato** (NuGet, "Legacy") e il suo nativo contiene **SQLite 3.39.2** (CVE-2025-6965, invisibile all'audit): sonda sul
sostituto prima dei dati veri; (c) `DbUnlockService` riscrive il salt quando trova "salt senza DB", non c'è lock di istanza
singola, il salt non viene sincronizzato su disco; (d) su Linux backup e restore scrivono il dump in chiaro in `/tmp`, e
l'auto-backup (DPAPI dentro `BackupScheduler`, in Data) si spegne da solo; (e) ~~i tre commit della VM di R9 non sono sulla pila~~ → **riconciliati il
2026-09-25** (merge di `r9-day-boundary`: i due test nuovi girano in `Recordum.Presentation.Tests` e sono stati riprovati rossi). **Decisioni dell'autore**: dogfooding su **Windows e Linux** (F2 si allarga: nota
veloce, backup e restore incrociato, su Linux picker dei template e builder); **niente promemoria** prima del dogfooding (E14 dopo
F8); **merge in `main` a ogni onda chiusa**; piani da F2 fatti di **decisioni, interfacce e test**. Aperte e urgenti: D3
(sblocco su Linux, prima di F2) e D7 (chrome su Linux, prima del Task 10). L'errata del piano F1 è già applicata (cinque errori
verificati, nota in testa al file).

**Passo 0 in esecuzione (2026-09-25, stessa sessione della revisione).** Piano
[`docs/superpowers/plans/2026-09-25-passo-0-messa-in-sicurezza.md`](docs/superpowers/plans/2026-09-25-passo-0-messa-in-sicurezza.md)
(formato D5: decisioni, interfacce, test), tabella di stato in testa al piano. **Fatti**: 0.1 R9 riconciliato nella pila
(`main` = `911ed24`); 0.2 **CI GitHub Actions** (job Linux + Windows, WPF serializzata, PNG headless negli artefatti; i test
che dipendono da DPAPI portano `Category=WindowsOnly`, esclusi solo su Linux) — il primo run completo su Windows dopo la
rinomina di F0 ha trovato soltanto due difetti di test, F0-D1 chiuso; 0.3 backup e restore girano anche su Linux (fixture
con chiave fissa) e un `.recbak` fatto su Linux si ripristina su Windows (`CrossMachineRestoreTests`); 0.4 sblocco sicuro
(`ReconcileOrphans`, salt `CreateNew` + fsync, log, `DataDirectoryLock` anche in WPF); 0.5 cartelle dati 0700 su Unix e file
in chiaro solo in `TempDirectory` (guardia `SystemTempGuardTests`); 0.6 `RecordumOptions.Resolve` (`--data-dir` /
`RECORDUM_DATA_DIR`), foto che seguono l'override, `--seed-demo` che rifiuta i dati veri; 0.7 `IAutoBackupKeyStore` in Core,
`DpapiAutoBackupKeyStore` in Data, scheduler che si spegne scrivendolo nel log; 0.8 `Directory.Packages.props` senza
wildcard + **EF Core 10.0.12**. **Restano**: 0.9 sonda sul provider di cifratura (D9), 0.10 documenti (questo file sotto i
15 KB, indice del tracker, ledger), il key store Linux (D3), e nella shell Avalonia i collegamenti del Task 11 di F1 (lock,
`ReconcileOrphans`, `Resolve`, sink di log). *Gotcha nuovi*: la regola `Backup*/` del `.gitignore` di Visual Studio esclude
qualunque cartella `Backups/` (una fixture non era mai entrata nel repo — l'ha scoperto la CI); in un test, fissare la
cultura quando si confrontano etichette con i nomi dell'enum (in inglese coincidono); su Windows `File.OpenRead` fallisce su
un DB che SQLite tiene aperto (usare `FileShare.ReadWrite`); il push di un file in `.github/workflows/` chiede al token di
`gh` lo scope `workflow`.

**Progresso 2026-10-02 (onda F1 — Home: codice completo, non ancora chiusa).** Piano
`2026-09-20-wave-f1-home.md`, 13 task su `avalonia-linux-slice` (`aaa676b`…`6200ede` per i Task 6-12): un implementatore
per task, in sequenza, e una review dopo ciascuno (Sonnet; Opus sui Task 4, 10 e 11: sblocco, chrome e avvio). Nessun
Critical; una tornata di correzione sui Task 4, 5, 7, 10 e 11. Finding a tracker: `docs/phase-4-issues.md`, sezione "Onda F1".

*Cosa esiste ora.* `Recordum.UI` si avvia da sé su Linux e apre il database cifrato vero: lock della cartella dati,
riconciliazione del salt, sblocco (DPAPI senza finestra su Windows; altrove passphrase Argon2id con cache), poi la Home con
righe vive (card tinta per riga, rinomina in linea, menu contestuale, trascinamento senza fantasma), rail e tab del
workspace, finestra principale. Su Linux la finestra tiene le decorazioni di sistema (D7), senza ripiego né maniglie nostre;
il codice chiede l'estensione dell'area client e nasconde i nostri pulsanti se la piattaforma non la accoglie. Il log degli
errori sta in `<cartella-dati>/logs` (0700/0600) e ogni guasto di sblocco, di qualunque tipo, vi lascia una riga.

*Cosa ha insegnato l'esecuzione.* Le review hanno trovato difetti veri, spesso test che non potevano fallire: il gestore
dei tasti agganciato al `TopLevel` non veniva mai tolto allo stacco (`GetTopLevel(this)` è già nullo in
`OnDetachedFromVisualTree`) e il test del tasto Canc non falliva comunque; nel Task 4 togliere `ClearAllPools()` lasciava
tutto verde; nel Task 5 quattro registrazioni DI non erano risolte da nessun test. .NET applica il modo di creazione Unix solo
alla cartella foglia, non ai genitori mancanti: va impostato a mano. `IsExtendedIntoWindowDecorations` è osservabile e
non si legge una volta sola in `Opened`. Gli sblocchi falliti con eccezioni fuori dal filtro di `DbUnlockService` non
lasciavano traccia: ora un filtro `when` che non cattura le registra tutte. La misura ha smentito un'ipotesi: i gruppi
chiusi **non** realizzano i figli; il costo viene dagli stili inclusi per riga, dal menu da 10 voci per riga e dalla
mancata virtualizzazione. Un limite di sessione ha interrotto il Task 12 (ripreso con lo stesso agente); il Task 6 era
stato fermato per budget e ripreso da un branch `wip`. Le parti del brief scritte prima di D7 e dei Task 9-11 erano stantie.

*Non consegnato*: il ramo Windows reale dello sblocco e il chrome su Windows non sono mai girati (handoff VM in
`docs/dev-environment-sync.md`); la Home con 300 righe è sopra il budget (F1-PERF1); rail e Ctrl+4/5 spenti (F1-RAIL1);
nessun editor, builder, dialog di export/backup (F4-F6), nessun grafico (F7); WPF resta la shell che spedisce. Conteggi su Linux:
**Core 150 / Data 539 (+7 `WindowsOnly` che falliscono qui) / Plugins 66 / ExportEngine 37 / Presentation 953 / UI 183**.
**Prossimo**: la review dell'intero branch, poi la checklist a schermo dell'autore su Windows e GNOME e il merge in `main`.

**Progresso 2026-10-12 (onda F2a — Registrazione su Avalonia: codice completo, non mergiata).** Piano
`2026-10-06-wave-f2a-registration`, 8 task su `f2a-registration` (parte da `f2-perf1-home`, a sua volta da
`avalonia-linux-slice`; nessuno dei tre in `main`). *Cosa esiste ora*: la schermata dell'abitudine su Avalonia (intestazione,
colonna "Oggi" per azione, contatore, astinenza e stato, calendario Mese/Settimana, Cronologia), il quick-add dei cinque tipi,
la barra dei timer con cronometri vivi, e il percorso "ieri" provato sul DB SQLCipher vero (reset 04:00, orologio finto alle
02:30: la registrazione va a `NoonOf(D-1)`, la cella D-1 la mostra, la riga Home la riflette, la cancellazione dalla
Cronologia arriva a DB e riga).
*Difetti latenti trovati e corretti*: la barra dei timer non si aggiornava mai; il cronometro di sessione non scorreva (né
quello della Cronologia, anche in WPF); l'ora delle registrazioni era mostrata in UTC; la cancellazione non chiedeva conferma
(ora sì, anche in WPF, che condivide il ViewModel); la rinomina dalla Home non arrivava all'intestazione della schermata
perché `HomeRowViewModel.CommitRename` non pubblicava `HabitUpdated` (preesistente): la radice è che chi scrive il nome non
annunciava niente, quindi ora lo fa `HabitNodeService.UpdateAsync`, in un punto solo, e l'editor ha smesso di pubblicarlo
(niente doppio ricarico). Un numero scritto col separatore dell'altra lingua svuota la cella invece di valere dieci o mille
volte di più.
*Cosa ha insegnato*: le fixture condivise e le date vere fanno rossa la CI (cultura fissata nei test del calendario, `b0e0cec`;
DB privato per i test che scrivono, Ruling R8). Gli screenshot hanno mostrato che W2V-4/U4 (cerchietto sul numero del giorno)
non sono chiusi dal nuovo calendario. Misura: tab con 730 registrazioni ≈ 0,7 s a caldo, 4 s a freddo (headless).
*Suite su Linux a fine Task 8*: Data 543 (senza `WindowsOnly`), Presentation 968, UI 252 (`Category!=Performance`).
*Non consegnato*: gruppo senza chip né colonne (F3), vista Anno e salto mese/anno (F3), marcatore d'origine di `active_timer`
(CM-4), purge che lascia una riga morta nella barra, tracce `Debug.WriteLine` invisibili in Release: tutto in
`docs/phase-4-issues.md`, sezione "Onda F2a". **Prossimo**: review dell'intero branch e CI (controllore), checklist a schermo
dell'autore (v. ROADMAP), poi F2b.

**Progresso 2026-10-15 (onda F2b: creare e configurare su Avalonia: codice completo, non mergiata).** Piano
`2026-10-13-wave-f2b-creation`, 9 task su `f2b-creation` (da `f2a-registration`; nessun branch della pila in `main`).
*Cosa esiste ora*: i picker di colore e d'icona, il builder con anteprima viva (stessa striscia per tipo della Home),
il picker dei template in una finestra modale, le porte d'ingresso della Home riaccese (F1-RAIL1), la schermata a due
colonne con ridimensionamento e modalità focus, e Configura con le dodici sezioni (Notifiche aspetta E14). Provato sul DB
SQLCipher vero: template → registrazione → obiettivo cambiato, e builder → unità personalizzata → quick-add; le quattro
schermate (picker, builder, Configura) in chiaro e in scuro con asserzioni strutturali.
*Cosa ha insegnato*: tre test a tempo reale erano instabili e sono stati resi deterministici (R3); un CTS liberato con
lavoro in volo faceva lanciare un ricaricamento (R5); lo screenshot finale ha trovato un difetto che nessun test aveva
misurato, le sezioni di Configura larghe quanto il contenuto (F2b-4: l'`Expander` era allineato a sinistra).
*Non consegnato*: contagocce del colore (decisione 5), "Date fisse" dell'obiettivo (F2b-2), W2V-11, reset time di un
gruppo verso i figli aperti (F2b-1). Suite UI su Linux: 343 in en_US e it_IT. **Prossimo**: review dell'intero branch e
CI, la checklist a schermo dell'autore su GNOME, poi F2c.

**Progresso 2026-10-16 (onda F2c: dati al sicuro su Avalonia: codice completo, non mergiata).** Piano
`2026-10-16-wave-f2c-data-safety`, 8 task su `f2c-data-safety` (da `f2b-creation`; nessun branch della pila in `main`).
*Cosa esiste ora*: `ISecretStore`/`LibsecretSecretStore` (schema `org.recordum.Secret`, una voce per cartella dati, chiamate
fuori dal thread dell'interfaccia); sblocco dalla chiave ricordata con il tetto di 10 s (`StoredKeyUnlock`) e scelta
"Ricorda" a tre stati; backup automatico avviato dalla shell, con la passphrase nel portachiavi (DPAPI su Windows) e
auto-spegnimento segnalato (`backup.auto.disabled_reason`); fixture `.recbak` di Windows e test di restore incrociato;
dialog di file asincroni; finestre Backup e Ripristina (chiusura bloccata durante l'operazione, spegnimento dopo un restore
riuscito); finestra Impostazioni con "Dati e backup" e "Dimentica su questo dispositivo"; file del database 0600 (F1-SEC1).
Provato sul portachiavi e sul DB veri, in una cartella privata: creazione con "ricorda" → riavvio senza finestra → "Dimentica"
→ finestra; "Backup ora" → "Ripristina…" → riavvio ancora senza passphrase (`KeyringStartupIntegrationTests`).
*Cosa ha insegnato*: una chiamata libsecret sincrona può aspettare un prompt senza limite (tetto e `Task.Run` ovunque); la
`Write` tardiva di un tentativo abbandonato può disfare un "Dimentica" (F2c-4); in un test d'integrazione la
`IConfirmationDialog` vera apre una finestra e il test non finisce mai.
*Non consegnato*: "Apri cartella" nello storico, cancello fra restore e auto-backup, modo del file dopo un restore, avviso
di timeout preciso: `docs/phase-4-issues.md`, sezione "Onda F2c" (F2c-1…5). Suite su Linux: Data 581, Presentation 998,
UI 376 (en_US e it_IT; `Category!=Performance`). **Prossimo**: review dell'intero branch e CI, la checklist a schermo
dell'autore su GNOME (v. ROADMAP), poi F2d.

**Progresso 2026-10-22 (onda F2d-1: tag e foto nella nota veloce: codice completo, non mergiata).** Piano
`2026-10-21-wave-f2d1-tags-photos`, 8 task su `f2d1-tags-photos` (da `f2c-data-safety`; nessun branch della pila in `main`),
dal 2026-10-21 al 2026-10-22, `0693a5d..f8bcf4d5` (16 commit).
*Cosa esiste ora*: schema 2.19 (`note_tag`; le foto nel DB cifrato con miniatura); `IPhotoImporter` (Skia: orientamento EXIF
applicato, metadati tolti, ridimensionamento, tetto di pixel, tre scelte D10); import all'avvio dei file storici nel DB, con
cancellazione dei file orfani; tag nel testo (`#nome`) e nella riga Tag, menu `#` con «Crea «#…»»; nota veloce su Avalonia con
tag e foto, lightbox, export con le foto del DB.
*Cosa ha insegnato*: un'immagine piccola con dimensioni enormi esaurisce la memoria (il tetto di pixel è nato dalla security
review di T3); l'orologio di sistema andato indietro ha fatto saltare la build incrementale (`--no-incremental`); la review
finale ha trovato ciò che quelle per task non vedevano: una voce di sole foto non modificabile e nomi della riga Tag che il menu
`#` inseriva come un altro tag (F2d1-1, F2d1-2), più sette minori (F2d1-3), tutti corretti con la riesamina.
*Non consegnato*: miniature fuori dal thread UI (da firmare), pulsanti del composer durante una modifica, chip dei tag in
Cronologia (F2d-2), decodifica JPEG scalata, avanzamento dell'import al primo avvio: `docs/phase-4-issues.md`, sezione "Onda
F2d-1" (F2d1-4…13). Suite su Linux: Core 170, Data 646 (+1 saltato), Presentation 1067, UI 401 + 3 Performance (en_US e it_IT),
ExportEngine 39. **Prossimo**: la checklist a schermo dell'autore, con la prova su una copia dei dati veri in testa (v. ROADMAP),
poi F2d-2.

**Progresso 2026-10-27 (onda F2d-2: editor del diario della sera: codice completo, non mergiata).** Piano
`2026-10-22-wave-f2d2-diary-editor`, 8 task su `f2d2-editor` (da `f2d1-tags-photos`; nessun branch della pila in `main`), dal
2026-10-22 al 2026-10-27, `f3ff895f..5172e730` (con una pausa chiesta dall'autore a metà del Task 3).
*Cosa esiste ora*: `DiaryEditor` (AvaloniaEdit) con anteprima dal vivo fuori dalla riga del cursore — titoli, grassetto e corsivo,
elenchi che continuano con Invio, citazioni, `#tag` nel colore d'accento col menu condiviso con la nota veloce
(`TagMenuController`), immagini nel testo come `![nome](photo:<id>)` con la miniatura; la colonna Diario in modalità diario (gesto
Diario) con una superficie sola per voce nuova e voce in modifica, conferma prima di scartare una modifica (anche cambiando giorno),
bozza scritta a ogni modifica in `app_setting` e ripresa dopo un crash; Cronologia con chip dei tag e "[foto]"; export Markdown
come cartella con le immagini e riferimenti risolti (`ExportOutput`), HTML con l'immagine al suo posto, PDF col testo delle voci;
`Markdown.Avalonia` e le sonde tolte.
*Cosa ha insegnato*: il "priming" dei test headless copriva un bug di produzione (il tema di AvaloniaEdit non era incluso in
`App.axaml`: senza il pacchetto che lo caricava per caso, l'editor sarebbe stato 0x0); un testo messo nel documento da un binding
va tolto dalla pila di annullamento, o Ctrl+Z riporta la bozza di prima sopra una voce; la review finale ha trovato che la
schermata richiamava `SetDay` sullo stesso giorno subito dopo aver ripristinato la bozza, e la ricarica cancellava la modifica
appena ripresa: i test del composer passavano perché non passavano dalla schermata (F2d2-1). Un fake che sblocca scritture da più
thread ha fatto un rosso solo su Windows (lista senza lock). Un commit intermedio pushato non compila da solo (`ab77aa48`).
*Non consegnato*: rimozione delle foto senza riferimento in modalità diario e bozza per giorno (decisioni dell'autore), export
da Avalonia (F6), virtualizzazione delle voci, minori: `docs/phase-4-issues.md`, sezione "Onda F2d-2" (F2d2-5…11). Suite su
Linux: Core 177, Data 649 (+1 saltato), Presentation 1124, UI 453 (`Category!=Performance`), ExportEngine 46. **Prossimo**: le
checklist a schermo dell'autore (F2d-1 con la prova sui dati veri, poi F2d-2; v. ROADMAP) e i merge della pila.

**Progresso 2026-10-27 (lancio stabile su Linux e decisioni dell'autore, `f2e-linux-launch`).** Decise dall'autore: D9
(SQLite3MC, opzione A della sonda, onda a sé prima dei dati veri), F2d1-4 (miniature sul thread UI accettate), F2d1-5, F2d2-5,
F2d2-6. Fatto: `scripts/install-linux.sh` (Release di `main` da `git archive`, `~/.local/share/recordum/app`, `.desktop` con
`StartupWMClass=recordum` e la finestra che dichiara lo stesso `WM_CLASS`, icona provvisoria, domanda sul backup,
`app.prev` per tornare indietro), provato in una cartella di prova; composer della nota veloce spento durante una modifica,
con comandi propri per la riga; × sulle foto senza riferimento in modifica nel diario; bozza che ricorda il suo giorno e
riapre composer e calendario su quello. Con questo F2 è a codice completo: restano le checklist a schermo, il merge e D9.

**Progresso 2026-10-28 (onda D9: SQLite3 Multiple Ciphers, `d9-sqlite3mc`, da mergiare).** Piano
`2026-10-28-wave-d9-sqlite3mc`, da `main` (`de3deaf2`, la pila F1 → F2e mergiata), `e2a27759..`.
*Cosa esiste ora*: `SQLite3MC.PCLRaw.bundle` 2.6.1 al posto di `bundle_e_sqlcipher` 2.1.11 (SQLite 3.39.2); il formato SQLCipher
4 dichiarato prima di ogni chiave da `CipherPragmas`; backup con `VACUUM INTO` + `rekey ''`, restore con `rekey` sul file in
chiaro in `TempDirectory`; guard di SEC-1 con la domanda nuova (`sqlite3mc_version`, SQLite ≥ 3.50), rosso contro
`bundle_e_sqlite3`; un DB del bundle 4.5.2 congelato in `Fixtures/Cipher` che si apre con lo stesso contenuto;
`THIRD-PARTY-NOTICES.md`.
*Cosa ha insegnato*: il journal di rollback non è parte del "formato": SQLCipher calcola il checksum delle pagine del journal
sul cifrato, MC sul chiaro, e un journal caldo del bundle di prima viene scartato in silenzio lasciando il DB a metà (D9-1).
L'ha trovato la fixture del journal caldo, non la sonda. Le prestazioni, misurate alternando prima e dopo, sono uguali.
*Non consegnato*: rollback dei journal vecchi (D9-1, deciso dall'autore: la difesa è in `install-linux.sh`); review dell'intero
branch. Suite su Linux: Core 177, Data 651 (+2 generatori saltati), Presentation 1136, UI 454 (`Category!=Performance`),
Plugins 66, ExportEngine 46.

**Progresso 2026-10-28 (direzione visiva: review, spec, onda 1 a codice completo).** Review UI/UX con Obsidian e Loop come
riferimenti (`UX-1…15`), confronto dei font su PNG (Inter scelta dall'autore), brainstorm col companion visuale e spec
`2026-10-28-ux-direction-design` approvata; piano dell'onda 1 eseguito su `ux1-foundations` (8 task).
*Cosa esiste ora*: Inter per l'interfaccia e Source Serif 4 solo per diario e marchio; la scala 12/13/14/16/20; i neutri
caldi con tre livelli di testo e l'inchiostro come accento (anche quello di Fluent), coi contrasti WCAG provati da un test;
la tinta dell'abitudine come livello con un'opacità per tema e la variante "testo" a 4,5:1 per qualunque colore; la striscia
della Home che scrive i valori (✓, 5, 1,2k) sotto un'intestazione dei giorni con oggi a destra, 4/7/14 giorni secondo la
larghezza; heatmap e mappa nel colore dell'abitudine (i verdi di GitHub vietati da una guardia); pulsanti a contorno, campi
con la cornice; la Home "Oggi" con la data, il log rapido con l'icona del tipo, i gruppi con "3 di 5"; la barra a sinistra
col marchio una volta sola, senza ☰; schede col pallino del colore, timer tinto, sblocco col marchio e l'avviso.
*Cosa ha insegnato*: i PNG vanno guardati, non solo prodotti: due difetti (nomi illeggibili a 280 px, campo nero a fuoco in
scuro) li hanno trovati le immagini, non i test, e solo dopo sono diventati test. Un'opacità da un token tematico è il modo
più semplice di avere una tinta che segue il tema a runtime. La macchina ha avuto due ondate di carico sul disco (suite da
3 a 20 minuti) e una sospensione per memoria: si misura in alternanza prima/dopo, mai contro un numero del mattino.
*Non consegnato*: le onde 2 e 3; ricarica della Home con 300 righe da ~120 a ~185 ms (budget 500 rispettato). Minori della
review rimandati: variante "testo" su riga selezionata (~4,1:1), icona di "fatte" accesa a basso contrasto, intestazione col
giorno dell'app e strisce col giorno dell'abitudine, tempo del timer 13/600 invece di 15/600 nella variante "testo", errore
fatale senza marchio serif, avviso sulla passphrase doppio al primo avvio, `HeatmapStrip` ricostruita fino a 4 volte per
riga, `YearHeatmapControl.HabitColor` da legare quando l'onda 2 ospita il controllo. Onda 1 in `main` (`c2887bd1`). Suite su Linux:
Presentation 1166, UI 540 (`Category!=Performance`), dopo la review dell'intero branch e la sua onda di correzioni.

**Progresso 2026-10-29 (direzione visiva, onda 2a: la schermata dell'abitudine, a codice completo).** Piano
`2026-10-29-ux-wave2a-habit-screen` eseguito su `ux2-habit-screen` (10 task), review dell'intero branch con un'onda di
correzioni.
*Cosa esiste ora*: la testa dell'abitudine col nome 20/600 nella variante "testo", un solo comando che registra nel colore
dell'abitudine per ciascuno degli otto casi (azione, tocco, misurato, serie, sessione, stato, astinenza, diario), quattro
numeri sui giorni previsti dal programma, "per un altro giorno"; la fascia "Oggi" e la barra dei chip sostituite. Colonne
Diario · Cronologia · Note · Configura, con ×, "a tutta finestra" e Esc. La Cronologia: cinque periodi con ‹ › e "Dal… al…",
mappa che cambia forma (mese a griglia; tre mesi, anno e "Tutto" come heatmap disegnata da `HeatGrid`), il giorno scelto con
la sua nota, l'elenco del periodo reso (il diario non è più Markdown grezzo) e virtualizzato. La colonna Note portata da WPF:
elenco, editor, versioni, elimina sul posto, allegati e visore; il testo non salvato non si perde (cambio di nota, eventi,
chiusura, cambio di abitudine).
*Cosa ha insegnato*: i PNG hanno trovato quattro difetti che i test non vedevano (heatmap dell'anno col nome del tipo al
posto delle celle per un binding letto prima che la proprietà esistesse, due etichette troncate, il visore che Esc non
chiudeva perché MainWindow prende Esc in tunnel); un test di prestazioni nuovo ne ha trovato un quinto: "Tutto" su tre anni
a ~3,7 s, di cui ~2,3 s per 1500 celle-pulsante e ~1,5 s per 1100 righe costruite tutte, ora ~0,35 s. Misurare per parti
prima di correggere: il primo sospetto (le celle) era metà del costo. Il bus degli eventi pubblica in modo sincrono:
un salvataggio dentro un setter rientra nella rilettura prima che il setter finisca. Le misure si fanno a macchina ferma:
un giro con una build in parallelo raddoppiava i tempi della Home.
*Prestazioni (locale, alternando due volte primo commit del branch e HEAD)*: Home 300 righe a caldo 308/338 ms prima,
151/170 ms dopo; tab con un anno 182/153 ms prima, 179/283 ms dopo; tre anni giornalieri (test nuovo) `LoadAsync` a caldo
197/315 ms, "Tutto" 323/344 ms. I test di prestazioni di Data non stampano numeri.
*Non consegnato*: UX2A-1 (le registrazioni precedenti alla creazione fuori da "Tutto" e dai numeri: decisione dell'autore),
UX2A-2…9 (minori nel tracker), le miniature delle foto nelle righe del diario della Cronologia, `TodayStripView` Avalonia
non più usata. Suite su Linux: Presentation 1261, UI 604 (`Category!=Performance`), Data 652 (+2 saltati).

**Progresso 2026-11-04 (direzione visiva, onda 2b: gruppo, Configura in cinque blocchi, scelta del colore, a codice
completo).** Piano `2026-10-30-ux-wave2b-group-configure` eseguito su `ux2b-group-configure` (9 task, ognuno con la sua
review), impilato su 2a.
*Cosa esiste ora*: la schermata del gruppo con l'avanzamento del giorno e i numeri del gruppo (serie di giorni completi,
30 giorni, obiettivo, la più saltata); le figlie come righe della Home, segnabili dal gruppo; "Insieme", una riga per figlia
nel periodo scelto (una cella per giorno, mai sotto i 4 px: si scorre, aperto su oggi); la mappa dell'anno del gruppo. Le
figlie di un gruppo hanno i giorni previsti e fatti da una query nuova di Data; l'inizio di un'abitudine è il più vecchio
fra creazione e prima registrazione (UX2A-1); un'astinenza è fatta nei giorni puliti. Configura in cinque blocchi (Chi è,
Cosa registri, Quando conta, Come appare in Home, Avanzate) con le righe-riassunto; la Configura del gruppo (Per le figlie,
Contenuto). La scelta del colore di §7.1. Chiusi UX2A-1, 5, 6, 8, 9.
*Cosa ha insegnato*: i PNG del Task 9 hanno trovato le iniziali della Settimana di "Insieme" accavallate (UX2B-1, tre
lettere su una cella di 14 px) e due finestre di test senza lo sfondo dell'app (il selettore del colore e il creatore in
scuro uscivano su nero); il gruppo vuoto con numeri senza dati e la mappa stretta aperta a gennaio vanno all'autore
(UX2B-2, 3). Le misure in parallelo a un compilatore residente (`VBCSCompiler`) oscillano del doppio: prima
`dotnet build-server shutdown`.
*Prestazioni (locale, alternando primo commit del branch e HEAD)*: Home 300 righe a caldo 116–329 ms prima, 147–286 ms dopo
(rumore, nessuno scarto chiaro; budget 500); gruppo di 12 figlie con tre anni (test nuovo) `LoadAsync` + Anno 572–841 ms a
caldo, "Tutto" 288–351 ms. Due righe della tabella sono **più alte dopo**: tab con un anno 345–486 ms contro 277–368, tre
anni `LoadAsync` 384–645 ms contro 268–487. La macchina era rumorosa (i giri si muovono del doppio sullo stesso lato), quindi
non è detto che sia una regressione, ma nemmeno che non lo sia: **prima del merge serve una nuova misura a macchina ferma**.
*Misura a macchina ferma (2026-11-04, dopo l'onda di correzioni)*: build server spento, worktree di `c0fc1306` contro `083597ef`,
tre giri alternati, solo i test di prestazioni UI (a caldo, ms, prima → dopo): Home 300 righe 132/291/165 → 144/314/137,
ricarica 131/283/221 → 171/274/137; tre anni `LoadAsync` 344/431/251 → 274/460/354; "Tutto" 568/604/573 → 544/538/596: nessuno
scarto. Tab con un anno 280/380/259 → 341/589/295: **più alto in tutti e tre i giri** (+36…+61 ms nei giri tranquilli), coerente
con la lettura dell'inizio aggiunta da UX2A-1; budget 5 s, accettato. Gruppo di 12 × tre anni: `LoadAsync` + Anno 514–703 ms,
"Tutto" 310–325 ms. Nodi visuali della Home 512 → 538.
Data: i quattro test di prestazioni verdi prima e dopo.
*Non consegnato*: "Salva come modello" del gruppo (in arrivo: un modello di gruppo è un formato nuovo), UX2B-2…4 e i minori
UX2B-M1…M25 nel tracker. Dopo l'onda di correzioni della review dell'intero branch (UX2B-5…8: il gruppo segue
Inizia/Ricaduta delle figlie, l'obiettivo e i rinomi, "Tutto" dall'inizio del gruppo, ricarica solo per le discendenti):
suite su Linux Presentation 1337, UI 701 (`Category!=Performance`), Data 664 (+2 saltati, non toccata, `40c5f8e3`).

**Progresso 2026-11-06 (direzione visiva, onda 3a: il creatore nei cinque blocchi e il selettore dei modelli, a codice
completo).** Piano `2026-11-04-ux-wave3a-creator-templates` eseguito su `ux3a-creator-templates` (7 task, ognuno con la
sua review), da `main` a `8e9af6f9`.
*Cosa esiste ora*: i modelli, le scale e le unità ufficiali parlano la lingua dell'interfaccia (UX-11): nome, descrizione e
ogni testo del contenuto vengono da `.resx` per Id stabile, mai per nome, e si fissano alla creazione; le etichette di una
scala ufficiale nascono nella `LabelsJson` della metrica; i modelli dell'utente restano come li ha scritti; nessuna
migrazione. Il creatore nei cinque blocchi di Configura (Chi è, Cosa registri, Quando conta, la colonna "Come appare in
Home", Avanzate chiuso), con le quattro tessere che dicono a cosa servono o perché sono spente, la riga "Si comporta come
**contatore**", "Crea abitudine" con la ragione quando è spento, senza esadecimale né "Dati d'esempio" (UX-12); mattoni
nuovi: obiettivo con nome, tolleranza della serie, "Conta sugli ultimi N giorni", riposo, richiesta automatica, anche nei
modelli. Il creatore si carica da un modello. Le etichette delle scale sono un elenco di campi, in Configura e nel
creatore. Il selettore dei modelli: "Da zero" e "Gruppo vuoto", le schede con la ricerca sul testo tradotto (senza
maiuscole né accenti), "I tuoi modelli" con Rinomina ed Elimina nel ⋮, il dettaglio con la riga della Home e cosa contiene,
"Usa questo modello" (passa dal creatore senza mostrarlo, una sola abitudine anche col doppio clic) e "Personalizza…".
*Cosa ha insegnato*: i PNG del Task 7 hanno trovato il riassunto della riga Scale ancora in inglese accanto al menu tradotto
(UX3A-3, corretto) e le etichette di una scala ufficiale in italiano in Configura ma in inglese nella testa della stessa
schermata (UX3A-2, corretto nell'ondata finale: il cambio di scala fissa le etichette tradotte come il creatore, e Configura
senza override ripiega sulle seminate come la schermata); "2 hours" in Home e nelle anteprime è il `Code` dell'unità (UX3A-1, precedente). Un
`KeyDown` in tunnel su una lista prende anche i tasti dei `MenuItem` dei suoi figli: Invio su Rinomina eseguiva "Usa"
(corretto per il pulsante ⋮; per il `MenuItem` serve lo schermo, perché in headless i suoi tasti non passano dalla lista).
*Non consegnato*: timer integrato, modalità e obiettivo della sessione nel creatore (UX3A-5, precedente), un modello di
gruppo, le righe che spiegano gesto del diario, tolleranza, giorni mobili e riposo (UX3A-M3); le domande UX3A-Q1…Q4 vanno
all'autore. Suite su Linux: Presentation 1430, UI 741 (suite intere, prestazioni comprese), Data 670 (+2 saltati, `Category!=WindowsOnly`), a `e249023c`.
*Ondata finale* (dalla review dell'intero branch, "With fixes"): UX3A-2 corretto; schede ufficiali in ordine col nome
tradotto; `DetailFailed` azzerato quando carica la scheda successiva; un modello senza `config` col recupero `None` come
`InstantiateAsync`; una guardia sui due nomi di un modello ufficiale (scheda e abitudine creata). Quattro scarti accettati
nel tracker (UX3A-A1…A4). Suite su Linux a `189664a1`: Presentation 1435, UI 741, Data 670 (+2 saltati).
