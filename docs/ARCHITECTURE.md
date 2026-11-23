# Architecture

This document explains how Recordum is put together, for someone reading the codebase for the first
time. It complements [`../CLAUDE.md`](../CLAUDE.md) (the dense invariants digest) with a narrative
overview.

## The big picture

Recordum is a layered .NET solution. The lower layers know nothing about the UI, so they can be
reused unchanged by future front-ends (mobile, web, sync).

```
Recordum.WPF ──────────────► the Windows desktop app (Views + ViewModels + DI bootstrap)
     │
     ├──► Recordum.Data ────► EF Core + SQLite3MC (SQLCipher 4 format): DbContext, migrations, service impls
     │        │
     │        └──► Recordum.Core ─► domain models + abstractions (interfaces, IClock). Pure C#.
     │
     └──► Recordum.Plugins ─► plugin runtime (in-process host, event bus, extension points)
              │
              └──► Recordum.Plugins.Contracts ─► the stable plugin-facing surface. ─► Core
```

**Dependency rule:** dependencies point *downward* only. `Recordum.Core` has no dependency on EF,
WPF, or anything else — it is the shared vocabulary. `Recordum.Data` implements Core's service
interfaces against EF Core. `Recordum.WPF` composes everything via dependency injection.

## Layer by layer

### Recordum.Core — the domain
Plain C# domain models and the abstractions everything else depends on. Two abstractions worth
knowing immediately:

- **`IClock`** — the *only* source of time. Nothing anywhere calls `DateTime.Now`/`UtcNow` directly;
  this keeps behavior deterministic and testable (tests inject a `FakeClock`).
- **Service interfaces** (`IHabitNodeService`, `ICompletionService`, `IAbstinenceService`, …) —
  implemented in `Recordum.Data`, consumed by ViewModels.

### Recordum.Data — persistence
EF Core over SQLite, **encrypted in SQLCipher 4 format via SQLite3 Multiple Ciphers** (the format is declared
with every key in `Security/CipherPragmas`). Highlights:

- **`RecordumDbContext`** with Fluent configurations. All cascade rules and indexes are declared
  **explicitly** — never left to EF defaults.
- **Migrations** are the source of truth for the schema (currently **v2.12**). Add one with
  `dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF`,
  and verify the generated SQL against the schema docs before applying. The generated
  `*.Designer.cs` / `*ModelSnapshot.cs` files are **never** hand-edited (a repo hook enforces this).
- **`TimestampInterceptor`** auto-populates `CreatedAt` / `UpdatedAt` / `UpdatedAtMs` (and plugin
  provenance) on save, so services don't set timestamps by hand.
- **Soft delete** is a global query filter on `HabitNode` (`DeletedAt == null`). Use
  `IgnoreQueryFilters()` for Trash / diagnostics.
- The **encryption key** comes from an injected `IDbKeyProvider` (DPAPI by default, opt-in Argon2id
  passphrase). Losing a passphrase means losing the data — there is no server-side recovery.

#### `habit_node.ui_prefs_json` keys (per-node presentation prefs)

Serialized from `HabitUiPrefs` (`Recordum.Core.Models`) via `IHabitUiPrefsService`. NULL column
or absent key = fall back to the habit type's default. Governance: every key lives here.

| Key | Type | Meaning | Introduced |
|---|---|---|---|
| `keyFigure` | string | Home-row key figure id (per-type set) | Onda 2 |
| `strip` | enum `None`/`Days7`/`Days14` | Home-row strip length | Onda 2 |
| `openColumns` | string[] | Per-habit open workspace columns | Onda 2 |
| `journalGesture` | `"Diary"` \| `"QuickNote"` | Gesto primario di un habit journal: scrivere la voce di oggi nell'editor Markdown, oppure il composer nota veloce con foto. Assente = `Diary`. | Onda 2.4 |

### Recordum.Plugins(.Contracts) — extensibility
First-party features (the chart engine, the export engine) are themselves plugins that register
capabilities through extension points, rather than reaching into the core directly. `Contracts`
is the stable surface plugins compile against; `Recordum.Plugins` is the runtime (in-process host,
`EventBus`, `ExtensionPointRegistry`, API router). A sandboxed WebView2 host is planned for later.

### Recordum.WPF — the app
MVVM with `CommunityToolkit.Mvvm`. `Bootstrap/` wires up DI, applies migrations at startup
(`ApplicationInitializer`), and shows a localized fatal dialog on failure. Views bind to ViewModels;
ViewModels depend only on Core interfaces.

## The domain model

Everything is a **`HabitNode`** — both habits and groups — arranged in a recursive tree via
`parent_id`.

- A **group** aggregates children and can carry its own goal; it is not directly completable.
- A **habit** has a `habit_type` and its own completion history, config, streak, goal, and
  notifications. The five types:

  | Type | Meaning |
  |---|---|
  | `action` | binary done / not-done |
  | `counter` | occurrences / measured values; supports named session segments |
  | `resistance` | **abstinence** — see below |
  | `journal` | a dated writing habit (Markdown notes) |
  | `state` | a value on a scale (e.g. mood 1–5) |

**Abstinence (`resistance`) is an explicit-state model** (added July 2026). Instead of completions,
it uses the `abstinence_period` table — one row per attempt (`started_at_ms` + optional
`ended_at_ms`) — behind `IAbstinenceService` (Start / Lapse / DeletePeriod / GetState / ListPeriods).
The current state is *derived*: an open period ⇒ *Abstaining*; the most recent closed one ⇒
*Relapsed*; none ⇒ *NotStarted*. The streak engine, the quick-add (with its live stopwatch), the Home
row, the calendar, and the Cronologia "records" list all read from these periods.

Other domain concepts to know: **config versioning** (every `HabitConfig` change snapshots into
`HabitConfigHistory`), the **`reset_time` cascade** (a habit's streak day-boundary falls back through
ancestor groups to a global default), and **scales** (a local `MetricDefinition.labels_json` overrides
a shared `ScaleTemplate` — CSS-style specificity; the stored value is always the integer).

## The desktop UI shape

`Recordum.WPF` is organized around a **workspace shell**: a home tree on the left and a habit screen
made of toggleable side-by-side **columns** (Panoramica, Diario, Cronologia, Dettaglio, Note,
Configura). Each column is a focused ViewModel:

- **Cronologia** — calendar + quick-add + the "records" list. For abstinence habits the records list
  shows period cards (live clock for the open period, elapsed interval + relapse date for closed
  ones, delete-with-confirmation).
- **Dettaglio** — year heatmap, streak summary + timeline, trend chart, hourly distribution, goal
  progress. Visualizations that don't apply to a given type are hidden (e.g. completion charts for
  abstinence).

Cross-cutting UI concerns: theming (light/dark), an `IClock`-driven live-elapsed clock helper reused
across the quick-add / detail / records views, and an `EventBus` that lets one mutation refresh the
other columns.

## Sync ("Unisci")

Cross-device synchronization relies on encrypted `.rcsync` exchange files on a shared folder
(computer) or SAF documents (phone), without a central server
(spec `docs/superpowers/specs/2026-11-22-unisci-design.md`).

- **Catalog (`SyncCatalog`)**: frozen contract of what travels (26 user data entities and
  only the `daily_reset_time`, `monthly_summary_day`, `monthly_summary_time` keys of
  `app_settings`) and what stays local to the device (`active_timer`, `backup_log`,
  `export_jobs`, `ai_insights`, `sync_meta`, `sync_tombstone`, `sync_base`, `sync_field_ms`,
  local settings, and `sync.*`). Maps internal columns to stable public `snake_case` names,
  separating the format from the SQLite schema. Guard tests on the EF model fail if a
  table, column, `app_settings` key, or unique index is not classified.
- **Triggers (`SyncTriggerInstaller`, `SyncConnectionInterceptor`, `SyncConnectionState`)**: because
  dozens of writes (`ExecuteUpdate`/`ExecuteDelete`, raw SQL, cascades) bypass
  `TimestampInterceptor`, capture happens in the database. `SyncTriggerInstaller.EnsureAsync`
  recreates at every startup the `trg_sync_del_*`, `trg_sync_upd_*`, `trg_sync_ins_*`, and
  `trg_sync_fms_*` triggers on catalog tables. Triggers write tombstones to `sync_tombstone`,
  advance `updated_at_ms` (`MAX(recordum_now_ms(), OLD.updated_at_ms + 1)`), and record in
  `sync_field_ms` the prior timestamp of unmodified fields. The timestamp comes from `IClock` via
  the per-connection SQLite function `recordum_now_ms()`, registered at connection open by
  `SyncConnectionInterceptor` alongside `recordum_sync_applying()`. During merge,
  `SyncConnectionState.Applying` (on the connection opened by EF) disables triggers to preserve
  remote timestamps and avoid generating tombstones.
- **Format (`SyncFileCipher`, `SyncArchiveWriter`, `SyncArchiveReader`, `SyncRowCodec`)**: every
  device writes its full state into an `.rcsync` file. On the outside, `SyncFileCipher`
  applies the authenticated `RCSY` envelope (Argon2id + AES-256-GCM, with in-memory caching of
  derived keys per salt). Inside is a ZIP archive containing `manifest.json`, one JSON Lines file
  `data/<entity>.jsonl` per entity (with timestamps of individual fields in `_field_ms` when lower than
  `updated_at_ms`), `tombstones.jsonl`, and binaries in `photos/`. Full public contract
  specifications: [`formats/sync-file.md`](formats/sync-file.md).
- **Engine (`SyncMerger`, `TextConflictBlock`, `NaturalKeyUnifier`, `DomainInvariantRepair`,
  `SyncFieldTimes`)**: applies a peer's snapshot in a single transaction with parameterized SQL
  built from the catalog, without EF tracking. Fields with their own timestamp (local
  `sync_field_ms`, `_field_ms` in the file) merge field-by-field under the LWW rule (Ruling 20); on
  timestamp ties, the value with the smaller canonical UTF-8 representation wins (for photos, the
  smaller hexadecimal SHA-256 hash). The per-peer base (`sync_base`) and the manifest's
  `merged_from` map are used only for rule B of free text (`note.body_md`, `completion.notes`) and
  for rescuing as a quick note deleted text that the deleter had not seen: if both sides
  have new text, `TextConflictBlock` appends the losing version to the same entry
  (`— <device> · yyyy-MM-dd HH:mm —`). `NaturalKeyUnifier` merges duplicates by natural key
  (`tag.name`, `unit_of_measure.code`, `scale_template.code`, `scale_anchor`) onto the smaller
  GUID, rewrites foreign keys, and records a tombstone with `alias_of`. At the end of the merge,
  `DomainInvariantRepair` closes excess open abstinence periods to zero duration (keeping open
  the one started earlier) and breaks cycles in `habit_nodes.parent_id` by reverting the most
  recent move (preserving field timestamps via `SyncFieldTimes`).
- **Service and scheduler (`SyncService`, `FolderSyncFileStore`, `SyncSchedulerHost`)**: only
  one run at a time across the entire process (`SyncRunState`). On a computer, `FolderSyncFileStore`
  reads others' `.rcsync` files in the folder and atomically writes its own (`.tmp` with disk
  flush and rename), skipping the write if the state signature is unchanged; plaintext
  temporaries reside only in `TempDirectory` and are destroyed with `SecureWipe`.
  Identity (`device_id`, `device_name`) lives in `sync_meta` and regenerates, zeroing `sync_base`,
  after a restore or if the machine name changes (also detecting a local file written by a
  clone with the same identity). On the first connection of a database with user data
  (`JoinAsync`), the user chooses whether to start from others' data or also keep their own,
  always after an automatic backup encrypted with the sync passphrase. `SyncSchedulerHost` starts a
  run at startup, every 5 minutes, upon prompt (`Nudge` with a 30 s cooldown), and at shutdown
  (`FlushAsync`).

## Conventions cheat-sheet

- Time only via `IClock`; a `DispatcherTimer` is created only when a WPF app is actually running
  (so headless unit tests stay deterministic).
- User strings in `.resx` (English first). Adding one = an entry in `Strings.resx` + `Strings.it.resx`
  + one accessor line in `Strings.Designer.cs` (this designer is hand-maintained, not codegen).
- DI everywhere; ViewModels take interfaces, never concrete services.
- Tests accompany behavioral changes; the data layer and streak engine are covered against a real
  in-memory database, not mocks.

For the exhaustive, always-current list of invariants (schema deltas, provenance rules, build/RID
notes, hooks), read [`../CLAUDE.md`](../CLAUDE.md).
