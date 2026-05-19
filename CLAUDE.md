# Recordum — Contesto di Progetto per Claude Code

> *Record your existence.* — Editor di abitudini open-source, offline-first, multipiattaforma. Questo file riassume gli invarianti e lo stato. **I documenti sorgente in §10 sono autoritativi** quando entrano in conflitto con questo riassunto.

---

## 1. Identità

Editor di abitudini con tipi multipli (action, counter, resistance, journal, state) componibili in alberi gerarchici. Rifiuta la riduzione *fatto / non fatto*: deve servire chi traccia salute cronica, allenamento, scrittura, astinenza, ecc.

- **Autore**: ITS Web Solutions Architect · Torino
- **Product spec**: v2.0 (aprile 2026)
- **DB schema**: v2.3 (maggio 2026) — delta provenance plugin v2.3 > v2.2 > v2.1.
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
| Editor Markdown | WebView2 + Toast UI / Milkdown |
| Backend web (Fase 4) | ASP.NET Core + React/Svelte PWA |
| Export PDF | QuestPDF |
| Mobile (Fase 6) | .NET MAUI |
| Health (Fase 6) | CoreMotion (iOS) / Health Connect (Android) |
| Statistica (Fase 8) | Math.NET Numerics |

---

## 4. Architettura — 5 progetti `src/`

| Progetto | Responsabilità |
|---|---|
| **Recordum.Core** | Domain models, abstractions (`IClock`, `IPluginContext`). C# puro, zero dipendenze UI/EF. |
| **Recordum.Data** | `RecordumDbContext`, Fluent configurations, migrazioni. Referenzia Core. |
| **Recordum.Plugins.Contracts** | Interfacce host, manifest record, permission/extension-point/event ID. Referenzia Core. |
| **Recordum.Plugins** | `PluginManager`, `InProcessPluginHost`, `AlwaysAllowPermissionBroker`, `ExtensionPointRegistry`, `EventBus`, `ApiRouter`. In Fase 3 host in-process per first-party fidati. WebView2 host in Fase 7. |
| **Recordum.WPF** | App + Views + ViewModels + `Bootstrap/` (ServiceRegistrar, ApplicationInitializer, RecordumOptions). |

Core/Data/Plugins.Contracts/Plugins riutilizzati senza modifiche in Fase 4 (web) e Fase 6 (MAUI).

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

**Soft delete**: solo su `HabitNode`. Cascade in transazione singola ai discendenti. Restore ripristina solo i discendenti chiusi nella stessa operazione. Purge permanente è job background per nodi >30 giorni (ordine: file → tabelle figlie → HabitNode).

**Naming**: tabelle e colonne snake_case (via `EFCore.NamingConventions`). Classi/proprietà C# PascalCase. Tutte le cascade rules e tutti gli indici **dichiarati esplicitamente** in Fluent API — mai default EF Core.

---

## 7. Roadmap

| Fase | Scope | Stato |
|---|---|---|
| 0–2 | Console + WPF prototyping | ✓ formativo (non importare codice in Fase 3) |
| **3** | WPF production: schema + UI + plugin foundation | **→ corrente** |
| 4 | Web/PWA | Pianificata |
| 5 | Cloud sync via provider terzi | Pianificata |
| 6 | MAUI mobile | Pianificata |
| 7 | Plugin runtime WebView2 + marketplace + widget + accessibilità | Pianificata |
| 8 | AI insight + correlazioni | Pianificata |

**Principi roadmap**: piattaforma completa prima di espandere. Schema finale dalla Fase 3 — nessuna migrazione invasiva successiva. Ogni fase = prodotto funzionante. Nessuna UI placeholder.

### Milestone Fase 3

| # | Obiettivo | Stato |
|---|---|---|
| **3.1** | Scaffold soluzione + schema completo + InitialSchema migration + plugin foundation (contracts + in-process host + manifest schema + plugin-api-ts skeleton + docs/plugin-api.md) | ✓ **completata 2026-05-14** |
| 3.2 | Core Data Layer: CRUD HabitNode/Config/Completion, soft delete + restore, HabitConfigHistory snapshot. SQLCipher via `IDbKeyProvider` (DPAPI baseline + opt-in passphrase Argon2id). Perf budget home <500ms | ✓ **completata 2026-05-19** |
| 3.3 | Home Screen: tab nav, albero habit, sort/filter, hide-completed, context menu | **→ next** |
| 3.4 | Habit Editor: progressive disclosure 3 livelli |  |
| 3.5 | Completion Recording: 5 tipi + session mode + multi-serie + timer recovery + retroactive |  |
| 3.6 | Journal Editor + Notes (WebView2 + Toast UI/Milkdown) |  |
| 3.7 | Habit Detail Screen: heatmap, streak history, chart, distribuzione oraria, goal progress |  |
| 3.8 | Streaks Engine: tutte le combinazioni schedule_type × streak_mode × streak_recovery × pause + HabitConfigHistory |  |
| 3.9 | Custom Charts (LiveCharts2). **Chart Engine come core plugin pilota** consumer di `chart.renderer` |  |
| 3.10 | Export System (PDF/HTML/Markdown/JSON/CSV) + Backup/restore con lista plugin + permission state |  |

### Stato Milestone 3.1

49 task del plan `docs/superpowers/plans/2026-05-14-milestone-3.1-foundation.md` completati. 95 test verdi (Core 18, Data 32, Plugins 41, WPF 4 smoke). Build pulito, zero warning. Stack consegnato: 5 progetti src + 4 test, 25 entity + Fluent configs + indexes + cascade rules + soft-delete filter + TimestampInterceptor, InitialSchema migration + seed (14 UoM, 7 ScaleTemplate, 8 HabitTemplate, 4 ExportTemplate, 13 AppSetting con `schema_version=2.3`), plugin contracts (9 extension points, 12 events, manifest record), plugin host (broker/registry/event bus/api router/in-process host/manager), WPF bootstrap (ServiceRegistrar + ApplicationInitializer + .resx), `tools/plugin-api-ts/` (manifest.schema.json + index.d.ts), `docs/plugin-api.md`.

### Stato Milestone 3.2

36 task del plan `docs/superpowers/plans/2026-05-18-milestone-3.2-core-data-layer.md` completati. 167 test verdi (Core 24, Plugins 41, Data 97, WPF 5 smoke incl. E2E encrypted bootstrap). Build pulito, zero warning. Data layer + SQLCipher consegnati: 14 service aggregate + query + 1 key transition (Recordum.Data.Services / .Queries), `HabitConfigHistoryInterceptor` registrato prima del `TimestampInterceptor`, `IHabitTreeQueryService.GetHomeTreeAsync` con perf budget p95 < 300 ms su dataset seedato (200 habit × 50k completion). SQLCipher attivo via `SQLitePCLRaw.bundle_e_sqlcipher`; `DpapiKeyProvider` di default + `PassphraseKeyProvider` (Argon2id 64 MiB / 4 iter / p=2) opt-in; `IKeyTransitionService` headless DPAPI↔passphrase con backup-and-rollback. Nuovo indice composito `(habit_node_id, completed_at_ms)` su `completions` (migration `20260519131117_CompletionCompletedAtMsIndex`). SQLCipher overhead empirico ~4× plain su workload SELECT 50k (assoluto < 30 ms/call): target 30% del plan irrealistico per AES-CBC su read-heavy; soglia di regressione fissata a 5×. `RecordumOptions` ora implementa `IDbStoragePaths` e ospita `DatabasePath` + `KeyFilePath` + `SaltFilePath`. Nessuna UI Settings per la transition: arriva in 3.3+.

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

### Migrazioni

```bash
dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF
dotnet ef database update    --project src/Recordum.Data --startup-project src/Recordum.WPF
```

Verifica il SQL generato contro la documentazione schema **prima** di eseguire l'update.

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
