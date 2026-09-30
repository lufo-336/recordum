# Onda 2.0 — Migrazione schema v2.14 + servizi prefs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lay the data foundation for Onda 2 (per-type experience): add the three persistence columns the later sub-plans (2.1–2.5) consume — `habit_node.ui_prefs_json`, `habit_config.repeatable`, `habit_config.series_labels_json` — via one additive migration, plus a typed read/write service for `ui_prefs_json`.

**Architecture:** Pure Data/Core layer work. `repeatable` + `series_labels_json` are *versioned* semantics ⇒ mirrored in `habit_config_history` and snapshotted by the existing `HabitConfigHistoryInterceptor`; they are read/written through the existing `HabitConfigService.GetAsync`/`UpsertAsync` (no new API). `ui_prefs_json` is *presentation* per-node (sibling of `pin_scope`) ⇒ new `IHabitUiPrefsService` serializes a typed `HabitUiPrefs` record to/from the column. A data-aware backfill sets `repeatable = 1` for habits that already have multi-series completions, so no existing behavior changes.

**Tech Stack:** C# / .NET 10, EF Core + SQLite (SQLCipher at runtime), EFCore.NamingConventions (snake_case), System.Text.Json, xUnit.

## Global Constraints

- **Schema version is `2.14`, NOT `2.13`.** The spec (`2026-07-03-wave-2-per-type-experience-design.md` §6) says "bump a 2.13", but that number was already consumed by Onda 1b (`AttachmentV213`). This migration takes the next free number, **2.14**. (Consequence: the master plan reserved v2.14 for Onda 5 — Onda 5 will take v2.15 at its execution time. Note this in that sub-plan when it is written; nothing to change now.)
- **Migration name:** `PerTypePrefsV214`.
- **Build/test command:** `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo`. The whole solution builds with `-p:UseAppHost=false`. WPF tests only run on Windows (this VM).
- **EF-generated files are hook-protected.** The `guard-generated.js` hook denies Edit/Write on `*.Designer.cs` and `*ModelSnapshot.cs`. `dotnet ef migrations add` writes all three files via the shell (allowed). **Do NOT hand-edit** `<ts>_PerTypePrefsV214.Designer.cs` or `RecordumDbContextModelSnapshot.cs`. The main migration body `<ts>_PerTypePrefsV214.cs` is **not** a Designer file → editable normally (this is where the hand-added backfill SQL goes).
- **Migration command (run from repo root):**
  `dotnet ef migrations add PerTypePrefsV214 --project src/Recordum.Data --startup-project src/Recordum.WPF`
  Verify the generated SQL against this plan before trusting it.
- **No new user-facing strings** in this sub-plan → `Strings.Designer.cs`/`.resx` untouched.
- **Time:** seed timestamps in migrations/`HasData` are literal `DateTime` constants (HasData determinism — the `TimestampInterceptor` does not run there). Runtime code uses `IClock`, never `DateTime.Now`. `HabitUiPrefsService` performs no timestamping itself (the node's `UpdatedAt` is bumped by the existing `TimestampInterceptor` on SaveChanges).
- **Snake_case table names:** `habit_configs`, `habit_config_histories` (EF pluralizes "history"→"histories"), `habit_nodes`, `completions`, `completion_values`. (`SchemaTests.All_29_tables_exist` asserts an exact table count of 29 — this migration adds columns only, no tables, so it stays green.)
- **Branch:** work on a feature branch `wave-2-0-schema-prefs`; the `guard-main.js` hook blocks direct commits to `main`.
- **AppSetting seed is `HasData`-managed** (`AppSettingConfiguration.HasData(AppSettingSeed.All)`), so bumping `AppSettingSeed` + running `dotnet ef migrations add` auto-generates the `schema_version` `UpdateData`. The data-aware backfill is the only thing that must be hand-added.

---

## Task 1: Schema columns + migration + data-aware backfill

Adds all three columns (on `habit_config`, `habit_config_history`, and `habit_node`), the CHECK constraint, the seed bump, the generated migration, the hand-added backfill, and every migration/schema test — including fixing the one existing test that EF-seeds `habit_nodes` at an earlier migration (it breaks the moment a `habit_node` column is added).

**Files:**
- Modify: `src/Recordum.Core/Models/HabitConfig.cs`
- Modify: `src/Recordum.Core/Models/HabitConfigHistory.cs`
- Modify: `src/Recordum.Core/Models/HabitNode.cs`
- Modify: `src/Recordum.Data/Configurations/HabitConfigConfiguration.cs`
- Modify: `src/Recordum.Data/Configurations/HabitNodeConfiguration.cs`
- Modify: `src/Recordum.Data/SeedData/AppSettingSeed.cs`
- Generate (via `dotnet ef`, do not hand-write): `src/Recordum.Data/Migrations/<ts>_PerTypePrefsV214.cs` (+ `.Designer.cs` + snapshot)
- Hand-edit (migration body only): `src/Recordum.Data/Migrations/<ts>_PerTypePrefsV214.cs`
- Modify: `tests/Recordum.Data.Tests/Fixtures/TestDbHelper.cs` (add raw-insert helpers)
- Modify: `tests/Recordum.Data.Tests/Migrations/CounterReclassV210Tests.cs` (raw-insert its habit_nodes + bump its version assert)
- Test (create): `tests/Recordum.Data.Tests/Migrations/PerTypePrefsV214MigrationTests.cs`
- Modify (version assertions `2.13` → `2.14`): `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`, `tests/Recordum.Data.Tests/Migrations/ActiveTimerV25MigrationTests.cs`, `tests/Recordum.Data.Tests/Migrations/NoteCategoryV211MigrationTests.cs`, `tests/Recordum.Data.Tests/Schema/StreakBrokenAtV26Tests.cs`, plus any Backup tests that assert the current version (find with grep in Step 9).

**Interfaces:**
- Produces: `HabitConfig.Repeatable` (`bool`), `HabitConfig.SeriesLabelsJson` (`string?`), `HabitConfigHistory.Repeatable` (`bool`), `HabitConfigHistory.SeriesLabelsJson` (`string?`), `HabitNode.UiPrefsJson` (`string?`). DB columns `habit_configs.repeatable` (INTEGER NOT NULL DEFAULT 0, CHECK `repeatable IN (0,1)`), `habit_configs.series_labels_json` (TEXT NULL), `habit_config_histories.repeatable`/`series_labels_json`, `habit_nodes.ui_prefs_json` (TEXT NULL). `schema_version` = `2.14`.
- Produces (test helpers): `TestDbHelper.RawInsertHabitNodeAsync(connection, id, name, habitType)`, `TestDbHelper.RawInsertHabitConfigAsync(connection, id, habitNodeId)`.

- [ ] **Step 1: Add the model properties**

In `src/Recordum.Core/Models/HabitConfig.cs`, after the `StreakBrokenAt` property (line ~33), add:

```csharp
    // Onda 2 (U8): registration semantics. Versioned in HabitConfigHistory.
    public bool Repeatable { get; set; }
    public string? SeriesLabelsJson { get; set; }
```

In `src/Recordum.Core/Models/HabitConfigHistory.cs`, after `SessionRepeatCount` (line ~29), add the mirror:

```csharp
    public bool Repeatable { get; set; }
    public string? SeriesLabelsJson { get; set; }
```

In `src/Recordum.Core/Models/HabitNode.cs`, after `CreatedByPluginId` (line ~22), add:

```csharp
    // Onda 2: per-node presentation preferences (JSON). NULL = type defaults. Not versioned.
    public string? UiPrefsJson { get; set; }
```

- [ ] **Step 2: Configure the columns in Fluent API**

In `src/Recordum.Data/Configurations/HabitConfigConfiguration.cs`, add inside `Configure`, after the `builder.Property(c => c.NoteMarkerColor)...` line:

```csharp
        builder.Property(c => c.Repeatable).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.SeriesLabelsJson).HasMaxLength(1000);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_habit_config_repeatable", "repeatable IN (0,1)"));
```

In `src/Recordum.Data/Configurations/HabitNodeConfiguration.cs`, add after `builder.Property(n => n.CreatedByPluginId).HasMaxLength(200);`:

```csharp
        builder.Property(n => n.UiPrefsJson); // TEXT NULL, no length cap (JSON blob)
```

(`HabitConfigHistory` needs no explicit config — `HabitConfigHistoryConfiguration` auto-maps the two new scalar properties; the history table is a raw mirror with no CHECK.)

- [ ] **Step 3: Bump the seed schema version**

In `src/Recordum.Data/SeedData/AppSettingSeed.cs`, add a seed date constant after `SeedDateV213`:

```csharp
    private static readonly DateTime SeedDateV214 = new(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc);
```

and change the `schema_version` row to:

```csharp
        new AppSetting { Key = "schema_version",              Value = "2.14",     UpdatedAt = SeedDateV214 },
```

- [ ] **Step 4: Build to confirm the model compiles, then generate the migration**

Run: `dotnet build src/Recordum.Data -p:UseAppHost=false --nologo`
Expected: build succeeds.

Then generate the migration:

Run: `dotnet ef migrations add PerTypePrefsV214 --project src/Recordum.Data --startup-project src/Recordum.WPF`
Expected: creates `src/Recordum.Data/Migrations/<timestamp>_PerTypePrefsV214.cs` (+ `.Designer.cs` + updates the snapshot).

- [ ] **Step 5: Verify the generated migration, then hand-add the backfill**

Open `src/Recordum.Data/Migrations/<ts>_PerTypePrefsV214.cs`. The generated `Up` must:
- `AddColumn<bool>("repeatable", "habit_configs", nullable: false, defaultValue: false)` (may render as a table rebuild in SQLite due to the CHECK constraint — that is expected and correct),
- `AddColumn<string>("series_labels_json", "habit_configs", nullable: true)`,
- `AddColumn<bool>("repeatable", "habit_config_histories", nullable: false, defaultValue: false)`,
- `AddColumn<string>("series_labels_json", "habit_config_histories", nullable: true)`,
- `AddColumn<string>("ui_prefs_json", "habit_nodes", nullable: true)`,
- a `CheckConstraint` `ck_habit_config_repeatable`,
- `UpdateData` bumping `schema_version` to `2.14` (value + `updated_at`).

If anything is missing, the model/config edits are wrong — fix them and regenerate (delete the generated files first, or `dotnet ef migrations remove`). Do **not** edit the `.Designer.cs`/snapshot by hand.

At the **end** of the `Up` method body (after all generated operations), hand-add the data-aware backfill:

```csharp
            // Onda 2 (spec §6): habits that already recorded multi-series completions are
            // repeatable by definition — flip them on so existing data keeps its behavior.
            // Runs after the column + its CHECK exist (table rebuild already done above).
            migrationBuilder.Sql(@"
UPDATE habit_configs SET repeatable = 1 WHERE habit_node_id IN (
    SELECT DISTINCT c.habit_node_id
    FROM completions c
    JOIN completion_values cv ON cv.completion_id = c.id
    WHERE cv.series_index > 0
);");
```

- [ ] **Step 6: Add raw-insert helpers to TestDbHelper**

The next steps stage rows against a schema that predates this migration, where `repeatable`/`ui_prefs_json` do not exist — so EF (full model) cannot INSERT those tables. Add raw-SQL helpers that write only the columns stable since v2.4. In `tests/Recordum.Data.Tests/Fixtures/TestDbHelper.cs`, add:

```csharp
    /// <summary>
    /// Raw-inserts a habit_nodes row using only columns stable since PinScopeV24 (no ui_prefs_json).
    /// For staging rows at a migration older than a later habit_node column addition, where EF's
    /// full-model INSERT would reference a not-yet-existing column.
    /// </summary>
    public static async Task RawInsertHabitNodeAsync(
        SqliteConnection connection, Guid id, string name, string habitType)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO habit_nodes (id, name, color, node_type, habit_type, ai_data_sharing,
                                     pin_scope, sort_order, is_archived, priority, is_locked,
                                     created_at, updated_at, updated_at_ms)
            VALUES ($id, $name, '#808080', 'Habit', $type, 'None',
                    'None', 0, 0, 0, 0,
                    '2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', 1748736000000);
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString("D").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$type", habitType);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Raw-inserts a habit_configs row with the NOT NULL columns stable since v2.4 (no repeatable /
    /// series_labels_json). Defaults: Daily schedule, Standard streak, NoRecovery.
    /// </summary>
    public static async Task RawInsertHabitConfigAsync(
        SqliteConnection connection, Guid id, Guid habitNodeId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO habit_configs (id, habit_node_id, schedule_type, streak_mode, streak_recovery,
                                       has_integrated_timer, timer_auto_prompt, session_base_timer,
                                       quick_complete, ai_data_sharing,
                                       created_at, updated_at, updated_at_ms)
            VALUES ($id, $h, 'Daily', 'Standard', 'None',
                    0, 0, 0,
                    0, 'None',
                    '2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', 1748736000000);
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString("D").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$h", habitNodeId.ToString("D").ToUpperInvariant());
        await cmd.ExecuteNonQueryAsync();
    }
```

> **Verify the enum literals** `'Daily'`, `'Standard'`, `'None'` against the current CHECK constraints/enum names in the model snapshot if the insert fails; adjust to the actual stored strings. (These are the `ScheduleType`/`StreakMode`/`StreakRecovery` enum names converted `HasConversion<string>()`.)

- [ ] **Step 7: Write the migration test (fails until the migration is correct)**

Create `tests/Recordum.Data.Tests/Migrations/PerTypePrefsV214MigrationTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies PerTypePrefsV214: schema_version bumps to 2.14; the three new columns exist with the
/// right nullability/default; the repeatable CHECK rejects out-of-range values; and the data-aware
/// backfill flips repeatable=1 exactly for habits that already have a multi-series completion.
/// </summary>
public sealed class PerTypePrefsV214MigrationTests
{
    [Fact]
    public async Task Schema_version_is_2_14_after_full_migration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var setting = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.14", setting.Value);
    }

    [Fact]
    public async Task New_columns_round_trip_and_repeatable_defaults_false()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        db.HabitNodes.Add(new HabitNode
        {
            Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c",
            UiPrefsJson = "{\"strip\":\"Days14\"}",
        });
        db.HabitConfigs.Add(new HabitConfig
        {
            Id = Guid.NewGuid(), HabitNodeId = habitId,
            SeriesLabelsJson = "[\"Notte\",\"Riposino\"]",
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var node = await db.HabitNodes.SingleAsync(n => n.Id == habitId);
        var cfg = await db.HabitConfigs.SingleAsync(c => c.HabitNodeId == habitId);
        Assert.Equal("{\"strip\":\"Days14\"}", node.UiPrefsJson);
        Assert.Equal("[\"Notte\",\"Riposino\"]", cfg.SeriesLabelsJson);
        Assert.False(cfg.Repeatable); // NOT NULL DEFAULT 0
    }

    [Fact]
    public async Task Repeatable_check_constraint_rejects_out_of_range()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        await TestDbHelper.RawInsertHabitNodeAsync(connection, habitId, "c", "Counter");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO habit_configs (id, habit_node_id, schedule_type, streak_mode, streak_recovery, " +
            "has_integrated_timer, timer_auto_prompt, session_base_timer, quick_complete, ai_data_sharing, " +
            "created_at, updated_at, updated_at_ms, repeatable) " +
            "VALUES ($id, $h, 'Daily', 'Standard', 'None', 0, 0, 0, 0, 'None', " +
            "'2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', 1748736000000, 2);";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$h", habitId.ToString("D").ToUpperInvariant());
        await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Backfill_sets_repeatable_only_for_habits_with_multi_series_completions()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var multi = Guid.NewGuid();   // has a completion_value with series_index > 0  → repeatable
        var single = Guid.NewGuid();  // only series_index 0                            → stays 0

        // Stage rows against the schema just BEFORE this migration (columns not present yet).
        await using (var seed = TestDbHelper.CreateAtMigration(connection, "AttachmentV213"))
        {
            await TestDbHelper.RawInsertHabitNodeAsync(connection, multi, "Multi", "Counter");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, single, "Single", "Counter");
            await TestDbHelper.RawInsertHabitConfigAsync(connection, Guid.NewGuid(), multi);
            await TestDbHelper.RawInsertHabitConfigAsync(connection, Guid.NewGuid(), single);

            // completions + completion_values are unchanged by this migration → EF-seed them.
            var metricMulti = Guid.NewGuid();
            var metricSingle = Guid.NewGuid();
            seed.MetricDefinitions.Add(new MetricDefinition { Id = metricMulti, HabitNodeId = multi, Name = "reps", Type = MetricType.Integer, SortOrder = 0 });
            seed.MetricDefinitions.Add(new MetricDefinition { Id = metricSingle, HabitNodeId = single, Name = "reps", Type = MetricType.Integer, SortOrder = 0 });

            var compMulti = Guid.NewGuid();
            var compSingle = Guid.NewGuid();
            seed.Completions.Add(new Completion { Id = compMulti, HabitNodeId = multi, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
            seed.Completions.Add(new Completion { Id = compSingle, HabitNodeId = single, CompletedAt = DateTime.UtcNow, CompletedAtMs = 2 });
            seed.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = compMulti, MetricDefinitionId = metricMulti, SeriesIndex = 1, ValueInteger = 10 });
            seed.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = compSingle, MetricDefinitionId = metricSingle, SeriesIndex = 0, ValueInteger = 5 });
            await seed.SaveChangesAsync();
        }

        // Apply the remaining migration (PerTypePrefsV214 + its backfill).
        await using (var migrate = TestDbHelper.CreateFull(connection))
            await migrate.Database.MigrateAsync();

        await using var db = TestDbHelper.CreateFull(connection);
        var configs = await db.HabitConfigs.ToDictionaryAsync(c => c.HabitNodeId, c => c.Repeatable);
        Assert.True(configs[multi]);
        Assert.False(configs[single]);
    }
}
```

- [ ] **Step 8: Run the new migration tests**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~PerTypePrefsV214"`
Expected: 4 tests PASS. If `RawInsertHabitConfigAsync` fails on a CHECK, fix the enum literals per Step 6's note and rerun.

- [ ] **Step 9: Fix the pre-existing tests broken by the new columns / version bump**

**(a)** `CounterReclassV210Tests` EF-seeds `habit_nodes` at `NoteHistoryV29` — now broken because the full-model INSERT references `ui_prefs_json`, which does not exist at that migration. Replace its seed block so the nodes are raw-inserted (metric_definitions stay EF-seeded, referencing the raw node IDs). In `tests/Recordum.Data.Tests/Migrations/CounterReclassV210Tests.cs`, change the staging block to:

```csharp
        await using (var seed = TestDbHelper.CreateAtMigration(connection, "NoteHistoryV29"))
        {
            flessioni = Guid.NewGuid(); nientefumo = Guid.NewGuid(); voglie = Guid.NewGuid();
            await TestDbHelper.RawInsertHabitNodeAsync(connection, flessioni, "Flessioni", "Resistance");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, nientefumo, "Niente fumo", "Resistance");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, voglie, "Voglie", "Resistance");

            seed.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(), HabitNodeId = flessioni, Name = "Ripetizioni",
                Type = MetricType.Integer, SortOrder = 0,
            });
            seed.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(), HabitNodeId = voglie, Name = "Intensità",
                Type = MetricType.Integer, SortOrder = 0,
                LabelsJson = "[\"Bassa\",\"Media\",\"Alta\"]",
            });
            await seed.SaveChangesAsync();
        }
```

Delete the now-unused private `Resistance(string name)` helper and the `HabitNode`/`HabitType` usings if they become unused. Also bump this test's final assertion from `"2.13"` to `"2.14"`.

**(b)** Update every remaining test that asserts the *current* schema version is `2.13`. Find them:

Run: `git grep -n '"2.13"' tests/`
For each assertion that means "current schema version" (in `SeedDataTests.AppSetting_schema_version_is_2_13`, `ActiveTimerV25MigrationTests`, `NoteCategoryV211MigrationTests`, `StreakBrokenAtV26Tests`, and any Backup test asserting the live DB version), change the expected value to `"2.14"` (and rename the test method / comment if it embeds `2_13`). Leave untouched any `"2.13"` that refers to a *historical* value (e.g. a `Down`-direction or older-migration assertion).

- [ ] **Step 10: Run the full Data suite**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo`
Expected: all green (394 + the 4 new = 398, minus none removed). If a Backup test still fails on a version string, it asserts the current version — bump it to `2.14`.

- [ ] **Step 11: Commit**

```bash
git add src/Recordum.Core/Models src/Recordum.Data/Configurations src/Recordum.Data/SeedData \
        src/Recordum.Data/Migrations tests/Recordum.Data.Tests
git commit -m "feat(data): schema v2.14 — per-type prefs columns + repeatable backfill

Adds habit_config.repeatable (+CHECK) & series_labels_json, habit_node.ui_prefs_json,
mirrors repeatable/series_labels_json into habit_config_history, and backfills
repeatable=1 for habits with existing multi-series completions. Bumps schema_version
to 2.14 (2.13 was consumed by Onda 1b). Raw-insert test helpers added; CounterReclass
staging test switched to raw inserts (habit_node gained a column).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Version `repeatable` + `series_labels_json` in HabitConfigHistory

Task 1 added the mirror *columns*; this task makes the interceptor actually snapshot them, and proves round-trip read/write of the two config fields through the existing `HabitConfigService` (satisfying the spec's "servizi prefs … repeatable, series_labels_json" with no new API).

**Files:**
- Modify: `src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs`
- Test (create): `tests/Recordum.Data.Tests/Integration/HabitConfigRepeatableVersioningTests.cs`

**Interfaces:**
- Consumes: `HabitConfig.Repeatable`, `HabitConfig.SeriesLabelsJson`, `HabitConfigHistory.Repeatable`, `HabitConfigHistory.SeriesLabelsJson` (Task 1); `IHabitConfigService.UpsertAsync`/`GetAsync`/`ListHistoryAsync` (existing).
- Produces: `"Repeatable"` and `"SeriesLabelsJson"` present in `HabitConfigHistoryColumns.Versioned` (and absent from `Excluded`).

- [ ] **Step 1: Write the failing versioning test**

Create `tests/Recordum.Data.Tests/Integration/HabitConfigRepeatableVersioningTests.cs`. Model it on the existing `HabitConfigServiceTests` for wiring (same fixture/DI helper it uses — open that file and copy its context/service construction). The behavior:

```csharp
    [Fact]
    public async Task Changing_repeatable_writes_a_history_snapshot_of_the_previous_value()
    {
        // Arrange: a habit + config with repeatable=false.
        // (construct service + node exactly as HabitConfigServiceTests does)
        await service.UpsertAsync(habitId, c => { c.Repeatable = false; c.SeriesLabelsJson = "[\"A\"]"; });

        // Act: flip repeatable on.
        await service.UpsertAsync(habitId, c => c.Repeatable = true);

        // Assert: a history row captured the previous (false) value.
        var history = await service.ListHistoryAsync(habitId);
        Assert.Contains(history, h => h.Repeatable == false);
        var current = await service.GetAsync(habitId);
        Assert.True(current!.Repeatable);
        Assert.Equal("[\"A\"]", current.SeriesLabelsJson);
    }
```

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitConfigRepeatableVersioning"`
Expected: FAIL — no history row is written, because the interceptor's `Versioned` list does not yet include `Repeatable`, so flipping it is not seen as a versioned change.

- [ ] **Step 2: Add the two columns to the Versioned list**

In `src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs`, append to the `Versioned` array:

```csharp
        "HasIntegratedTimer","TimerMode","TimerTargetSeconds","TimerAutoPrompt",
        "RestTimerSeconds","SessionBaseTimer","SessionCountdownSeconds","SessionRepeatCount",
        "Repeatable","SeriesLabelsJson"
```

(They must NOT be in `Excluded` — leave `Excluded` unchanged.)

- [ ] **Step 3: Run the versioning test**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitConfigRepeatableVersioning"`
Expected: PASS.

- [ ] **Step 4: Run the full Data suite (guards the snapshot reflection path)**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo`
Expected: all green. (The interceptor's `BuildSnapshot` reflects over `Versioned` onto `HabitConfigHistory` — both properties now exist there, so no reflection error.)

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs tests/Recordum.Data.Tests
git commit -m "feat(data): version repeatable + series_labels_json in HabitConfigHistory

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: `HabitUiPrefs` typed model + `IHabitUiPrefsService`

The read/write plumbing for `ui_prefs_json`, as a typed record so later sub-plans consume fields, not raw JSON. Governance (spec §6): every JSON key is documented in `docs/ARCHITECTURE.md`.

**Files:**
- Create: `src/Recordum.Core/Enums/HabitUiPrefsEnums.cs`
- Create: `src/Recordum.Core/Models/HabitUiPrefs.cs`
- Create: `src/Recordum.Core/Abstractions/Services/IHabitUiPrefsService.cs`
- Create: `src/Recordum.Data/Services/HabitUiPrefsService.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Modify: `docs/ARCHITECTURE.md`
- Test (create): `tests/Recordum.Data.Tests/Services/HabitUiPrefsServiceTests.cs`

**Interfaces:**
- Consumes: `HabitNode.UiPrefsJson` (Task 1); `IDbContextFactory<RecordumDbContext>`.
- Produces: `HabitUiPrefs` (record: `KeyFigure` `string?`, `Strip` `HomeStrip?`, `OpenColumns` `IReadOnlyList<string>?`, `JournalGesture` `JournalPrimaryGesture?`); enums `HomeStrip { None, Days7, Days14 }`, `JournalPrimaryGesture { Diary, QuickNote }`; `IHabitUiPrefsService.GetAsync(Guid, CancellationToken) → ValueTask<HabitUiPrefs>` (empty record when column NULL), `SetAsync(Guid, HabitUiPrefs, CancellationToken) → ValueTask` (throws `InvalidOperationException` if node missing).

- [ ] **Step 1: Write the failing service test**

Create `tests/Recordum.Data.Tests/Services/HabitUiPrefsServiceTests.cs`. Use the same in-memory `IDbContextFactory` setup other Data service tests use (open an existing file under `tests/Recordum.Data.Tests/Integration/` or `Services/` and copy the factory/fixture construction — e.g. how `AppSettingService` is tested, or the shared test DB fixture). Tests:

```csharp
    [Fact]
    public async Task Get_returns_empty_prefs_when_column_is_null()
    {
        var (service, factory) = CreateService();
        var habitId = await SeedHabitAsync(factory); // node with UiPrefsJson == null
        var prefs = await service.GetAsync(habitId);
        Assert.Null(prefs.KeyFigure);
        Assert.Null(prefs.Strip);
        Assert.Null(prefs.OpenColumns);
        Assert.Null(prefs.JournalGesture);
    }

    [Fact]
    public async Task Set_then_Get_round_trips_all_fields()
    {
        var (service, factory) = CreateService();
        var habitId = await SeedHabitAsync(factory);
        var prefs = new HabitUiPrefs
        {
            KeyFigure = "streak",
            Strip = HomeStrip.Days14,
            OpenColumns = new[] { "Cronologia", "Diario" },
            JournalGesture = JournalPrimaryGesture.QuickNote,
        };
        await service.SetAsync(habitId, prefs);

        var got = await service.GetAsync(habitId);
        Assert.Equal("streak", got.KeyFigure);
        Assert.Equal(HomeStrip.Days14, got.Strip);
        Assert.Equal(new[] { "Cronologia", "Diario" }, got.OpenColumns);
        Assert.Equal(JournalPrimaryGesture.QuickNote, got.JournalGesture);
    }

    [Fact]
    public async Task Set_throws_when_node_missing()
    {
        var (service, _) = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetAsync(Guid.NewGuid(), new HabitUiPrefs()).AsTask());
    }
```

Provide `CreateService()` and `SeedHabitAsync(factory)` helpers in the test class following the existing Data-test fixture pattern (in-memory SQLite connection kept open, `IDbContextFactory` returning contexts over it, `MigrateAsync` once). `SeedHabitAsync` inserts one `HabitNode` (`NodeType.Habit`, `HabitType.Counter`, name, color) and returns its Id.

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitUiPrefsService"`
Expected: FAIL — types/service do not exist (compile error).

- [ ] **Step 2: Create the enums and record**

`src/Recordum.Core/Enums/HabitUiPrefsEnums.cs`:

```csharp
namespace Recordum.Core.Enums;

/// <summary>Home-row strip length shown for a habit. Null pref = per-type default.</summary>
public enum HomeStrip { None, Days7, Days14 }

/// <summary>Primary journal gesture on the "Today" strip / landing. Null pref = template/type default.</summary>
public enum JournalPrimaryGesture { Diary, QuickNote }
```

`src/Recordum.Core/Models/HabitUiPrefs.cs`:

```csharp
using Recordum.Core.Enums;

namespace Recordum.Core.Models;

/// <summary>
/// Per-node presentation preferences, serialized to <see cref="HabitNode.UiPrefsJson"/>.
/// Every field nullable: null = fall back to the habit type's default. Consumed by Onda 2.1–2.5.
/// Governance: each field is documented in docs/ARCHITECTURE.md — keep them in sync.
/// </summary>
public sealed record HabitUiPrefs
{
    /// <summary>Home-row key-figure id (per-type set defined by the consuming surface). Null = type default.</summary>
    public string? KeyFigure { get; init; }

    /// <summary>Home-row strip length. Null = type default.</summary>
    public HomeStrip? Strip { get; init; }

    /// <summary>Per-habit open workspace columns (column identifiers). Null = type default.</summary>
    public IReadOnlyList<string>? OpenColumns { get; init; }

    /// <summary>Journal primary gesture. Null = template/type default.</summary>
    public JournalPrimaryGesture? JournalGesture { get; init; }
}
```

- [ ] **Step 3: Create the service interface**

`src/Recordum.Core/Abstractions/Services/IHabitUiPrefsService.cs`:

```csharp
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitUiPrefsService
{
    /// <summary>Reads a node's presentation prefs. Returns an empty record when the column is NULL.</summary>
    ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Serializes and stores a node's prefs. Throws if the node does not exist.</summary>
    ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement the service**

`src/Recordum.Data/Services/HabitUiPrefsService.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class HabitUiPrefsService(IDbContextFactory<RecordumDbContext> dbFactory)
    : IHabitUiPrefsService
{
    // String enums + omit nulls keep the stored JSON small and human-inspectable (governance).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var json = await db.HabitNodes.AsNoTracking()
            .Where(n => n.Id == habitNodeId)
            .Select(n => n.UiPrefsJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return new HabitUiPrefs();
        return JsonSerializer.Deserialize<HabitUiPrefs>(json, JsonOptions) ?? new HabitUiPrefs();
    }

    public async ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new InvalidOperationException($"HabitNode {habitNodeId} not found.");
        node.UiPrefsJson = JsonSerializer.Serialize(prefs, JsonOptions);
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Run the service tests**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitUiPrefsService"`
Expected: 3 tests PASS.

- [ ] **Step 6: Register in DI**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, next to `services.AddScoped<IHabitConfigService, HabitConfigService>();`, add:

```csharp
        services.AddScoped<IHabitUiPrefsService, HabitUiPrefsService>();
```

Ensure the `using` for the service namespace resolves (it lives in `Recordum.Data.Services`, same namespace as the other `AddScoped` service types there).

- [ ] **Step 7: Document the JSON keys (governance)**

In `docs/ARCHITECTURE.md`, add a subsection documenting the `ui_prefs_json` schema. Place it near the schema/persistence discussion (search the file for "schema" or "AppSetting" to find the right home):

```markdown
### `habit_node.ui_prefs_json` keys (per-node presentation prefs)

Serialized from `HabitUiPrefs` (`Recordum.Core.Models`) via `IHabitUiPrefsService`. NULL column
or absent key = fall back to the habit type's default. Governance: every key lives here.

| Key | Type | Meaning | Introduced |
|---|---|---|---|
| `keyFigure` | string | Home-row key figure id (per-type set) | Onda 2 |
| `strip` | enum `None`/`Days7`/`Days14` | Home-row strip length | Onda 2 |
| `openColumns` | string[] | Per-habit open workspace columns | Onda 2 |
| `journalGesture` | enum `Diary`/`QuickNote` | Journal primary gesture | Onda 2 |
```

- [ ] **Step 8: Build the whole solution + run the full Data suite**

Run: `dotnet build -p:UseAppHost=false --nologo`
Expected: succeeds (WPF Bootstrap compiles with the new DI line).

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo`
Expected: all green.

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.Core src/Recordum.Data/Services/HabitUiPrefsService.cs \
        src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs docs/ARCHITECTURE.md tests/Recordum.Data.Tests
git commit -m "feat(data): HabitUiPrefs typed model + IHabitUiPrefsService for ui_prefs_json

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Final verification (after all tasks)

- [ ] Run the full WPF suite too (the DI change touches WPF Bootstrap):
  `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo` — expected all green.
- [ ] Confirm no `*.Designer.cs`/snapshot was hand-edited: `git diff --name-only` should show the migration `.Designer.cs` + snapshot as *generated* (by `dotnet ef`), not manually touched beyond generation.

## Out of scope (this sub-plan)

Template seed changes (Sleep → `repeatable = on`) belong to **2.3** (registration UX). Any Home-row / Today-strip / builder rendering that *reads* these columns belongs to **2.1/2.2/2.5**. No WPF ViewModels, no UI, no new strings here.
