# Recordum — Storico stati milestone

> **Quando consultare.** Riferimento storico per le milestone Fase 3 e l'avanzamento Fase 4 (in corso). `CLAUDE.md` resta il file principale auto-caricato; questo file è il companion "a scalare" per ricostruire decisioni passate, deviazioni dai plan, schema delta dettagliati, e known quirks per milestone.
>
> Ogni sezione "Stato Milestone X" è il digest scritto a chiusura, autoritativo per la struttura del codice consegnato. Per il dettaglio task-by-task vedi `docs/superpowers/plans/2026-MM-DD-milestone-X.Y-*.md`. Per gli script mentali aggiornati (commit messages incluse le deviazioni) vedi `git log`. Per i quirks più rilevanti vedi le memorie utente `recordum-milestone-X-Y-spec`.

---

## Fase 3 — milestone

### Stato Milestone 3.1

49 task del plan `docs/superpowers/plans/2026-05-14-milestone-3.1-foundation.md` completati. 95 test verdi (Core 18, Data 32, Plugins 41, WPF 4 smoke). Build pulito, zero warning. Stack consegnato: 5 progetti src + 4 test, 25 entity + Fluent configs + indexes + cascade rules + soft-delete filter + TimestampInterceptor, InitialSchema migration + seed (14 UoM, 7 ScaleTemplate, 8 HabitTemplate, 4 ExportTemplate, 13 AppSetting con `schema_version=2.3`), plugin contracts (9 extension points, 12 events, manifest record), plugin host (broker/registry/event bus/api router/in-process host/manager), WPF bootstrap (ServiceRegistrar + ApplicationInitializer + .resx), `tools/plugin-api-ts/` (manifest.schema.json + index.d.ts), `docs/plugin-api.md`.

### Stato Milestone 3.2

36 task del plan `docs/superpowers/plans/2026-05-18-milestone-3.2-core-data-layer.md` completati. 167 test verdi (Core 24, Plugins 41, Data 97, WPF 5 smoke incl. E2E encrypted bootstrap). Build pulito, zero warning. Data layer + SQLCipher consegnati: 14 service aggregate + query + 1 key transition (Recordum.Data.Services / .Queries), `HabitConfigHistoryInterceptor` registrato prima del `TimestampInterceptor`, `IHabitTreeQueryService.GetHomeTreeAsync` con perf budget p95 < 300 ms su dataset seedato (200 habit × 50k completion). SQLCipher attivo via `SQLitePCLRaw.bundle_e_sqlcipher`; `DpapiKeyProvider` di default + `PassphraseKeyProvider` (Argon2id 64 MiB / 4 iter / p=2) opt-in; `IKeyTransitionService` headless DPAPI↔passphrase con backup-and-rollback. Nuovo indice composito `(habit_node_id, completed_at_ms)` su `completions` (migration `20260519131117_CompletionCompletedAtMsIndex`). SQLCipher overhead empirico ~4× plain su workload SELECT 50k (assoluto < 30 ms/call): target 30% del plan irrealistico per AES-CBC su read-heavy; soglia di regressione fissata a 5×. `RecordumOptions` ora implementa `IDbStoragePaths` e ospita `DatabasePath` + `KeyFilePath` + `SaltFilePath`. Nessuna UI Settings per la transition: arriva in 3.3+.

### Stato Milestone 3.3

33 task del plan `docs/superpowers/plans/2026-05-19-milestone-3.3-home-screen.md` completati. 243 test verdi (Core 30, Plugins 41, Data 126 incl. perf budget, WPF 46 incl. E2E `MainWindowViewModel` smoke). Build pulito, zero warning. Home Screen consegnato: 4 ViewModel (`HomeViewModel`, `HomeRowViewModel`, `SettingsViewModel`, `MainWindowViewModel`) con commands tab-aware e filtri client-side (type/tag/hide-completed/search/sort + pin layering Global > Local > None); 6 View XAML (`MainWindow`, `HomeView`, `HabitRowTemplate`, `GroupRowTemplate`, `HeatmapStrip`, `EmptyState`, `SettingsPopup`); 3 Behavior (`InlineRenameBehavior`, `TreeDragDropBehavior` con 3-band drop zone + `CyclicHabitNodeMoveException` guard, `ThemeService` registry watcher singleton); 7 converter (`HexToBrush`, `HabitTypeLabel`, `BoolToVisible`, `BoolToHidden`, `EnumToBool`, `StateToVisible`, `ActiveTabVisible`). Schema delta v2.4 (`PinScope` enum + migration). `HomeTreeRow` esteso a `PinScope`/`ChildCount`/`IsArchived`/`CompletionsLast7Days[7]`/`TagIds`. `HabitTreeQueryService.GetTrashTreeAsync` separato. Theming System/Light/Dark con switch live via `Resources/MergedDictionaries[1]` swap. Localization en + it tramite `Strings.resx` + designer auto-generato. Scorciatoie tastiera: Ctrl+1/2/3 tab, Ctrl+F focus search, F2 rename, Del soft-delete, Ctrl+E toggle archive, Ctrl+R restore. **Known limits**: drag-drop persiste solo `ParentId` (no `SortOrder` reorder in 3.3); heatmap intensity step binario (proporzionale a `TargetValue` → M3.7).

### Stato Milestone 3.4

30 task del plan `docs/superpowers/plans/2026-05-20-milestone-3.4-habit-editor.md` completati. 341 test verdi (Core 30, Plugins 41, Data 139, WPF 131 incl. E2E template→edit smoke). Build pulito, zero warning. Habit Editor consegnato: pane laterale destro `EditorPane` aperto su `Home.SelectedRow` (`MainWindowViewModel` routing); `HabitEditorViewModel` ospita L1 header (Name/Description/Color/Icon + Node/Habit type combos) + 11 sezioni VM (`Scheduling`, `Streak`, `Timer`, `Metrics`, `Scales`, `Goal`, `Notifications`, `Tags`, `AiSharing`, `DataSource`, `ResetTime`) — ciascuna estende `SectionViewModelBase` con autosave per-campo (`ApplyAsync` → Saving→Saved|Error). Visibilità per tipo via `IsApplicable(NodeType, HabitType?)`, coperta dal matrix test (spec §6). Service extensions: `IHabitTemplateService.InstantiateAsync` (clona seed in nuovo habit + config), `ITagService.SetTagsAsync` (replace idempotente), `IHabitConfigService.ListSegmentsAsync/UpdateSegmentAsync`, `IGoalService.GetActiveAsync/UpsertActiveAsync`. Create flow: `HabitTemplatePicker` modal (8 seed templates + Blank habit/group) lanciato da `+ New` button su Home via `IHabitTemplatePickerLauncher` (DI-injectable per test). Footer pane aggrega status (Error > Saving > Saved > Idle). Pane width persistito in `AppSetting "ui.editor_pane_width"` (default 480, clamp 320..900) — push back via `GridSplitter.DragCompleted` → `MainWindowViewModel.CommitEditorPaneWidthAsync`. Tutte le stringhe utente in `Strings.resx` + `Strings.it.resx` (en/it). **Known limits**: `DataSourceSection` backed by `AppSetting` placeholder (revisit a M3.6 dopo decisione schema); reorder segmenti/metrics non implementato (drag-drop intra-section deferred).

### Stato Milestone 3.5

40 task del plan `docs/superpowers/plans/2026-05-21-milestone-3.5-completion-recording.md` completati. 452 test verdi (Core 31, Plugins 41, Data 167, WPF 213 incl. E2E smoke quick-action + timer recovery). Build pulito, zero warning. Completion Recording consegnato.

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

30 task del plan `docs/superpowers/plans/2026-05-22-milestone-3.6-journal-editor.md` completati. ~491 test verdi (Core 31, Plugins 45, Data 175, WPF 240 incl. E2E Note CRUD smoke). Build pulito, zero warning nuovi. Journal Editor + Notes consegnato. **Schema delta: zero** — `note` e `attachment` esistono già da `InitialSchema` (M3.1); `schema_version` resta `2.5`.

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

27 task del plan `docs/superpowers/plans/2026-05-22-milestone-3.7-habit-detail-screen.md` completati. 540 test verdi (Core 38, Plugins 45, Data 191, WPF 266 incl. E2E `HabitScreenDetailSmokeTests`). Build pulito, 2 warning pre-esistenti, zero nuovi. **Schema delta: zero** — `schema_version` resta `2.5`.

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

26 task del plan `docs/superpowers/plans/2026-05-24-milestone-3.8-streaks-engine.md` completati. 613 test verdi (Core 40, Plugins 45, Data 261, WPF 267 incl. E2E `Detail_with_scheduling_aware_aggregator_reports_streak_length` + perf trait `OneYearHistory_completes_within_budget`). Build pulito, zero warning nuovi.

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

24 task del plan `docs/superpowers/plans/2026-05-25-milestone-3.9-custom-charts.md` completati (21–22 risolti come "no-migration" documentato — vedi sotto). 665 test verdi (Core 40, Plugins 61, **ChartEngine 18 nuovo progetto**, Data 266, WPF 280 incl. E2E `ChartsWorkspaceE2ETests`). Build pulito, zero warning. **Schema delta: zero** — `chart`/`chart_series` esistono da `InitialSchema`; `schema_version` resta `2.6`. Nessuna migration.

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

22 task del plan `docs/superpowers/plans/2026-05-27-milestone-3.10a-export-system.md` completati. 697 test verdi (Core 40, Plugins 64, **ExportEngine 17 nuovo progetto**, ChartEngine 18, Data 272, WPF 286 incl. E2E launch tests). Build pulito, zero warning nuovi. **Schema delta: zero** — `export_job`/`export_template` esistono da `InitialSchema`; `schema_version` resta `2.6`. Nessuna migration.

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

25 task del plan `docs/superpowers/plans/2026-05-28-milestone-3.10b-backup-restore.md` completati. **740 test verdi** (Core 41, Plugins 66, ChartEngine 18, ExportEngine 17, Data 292, WPF 306 incl. E2E `BackupRoundTripE2ETests`). Build pulito, zero warning nuovi. **Schema delta v2.7**: tabella `backup_log` + 5 AppSetting `backup.auto.*` + bump `schema_version` 2.6→2.7. Migration `BackupLogV27` additiva.

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

### Bug e rifinitura aperti

Vedi `docs/phase-4-issues.md` per il tracker triagato (`reported / verified / triaged / in-progress / fixed / verified-fixed`).
