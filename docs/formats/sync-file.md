# Recordum Sync Exchange File Format (`recordum-sync` 1.0)

This document defines the public contract for Recordum device synchronization files (`.rcsync`). The format is designed for long-term stability, independence from internal SQLite migrations, inspectability by users, and interoperability with external tools.

---

## 1. Container & Encrypted Envelope (`RCSY`)

Each participating device writes its complete merged state into its own `.rcsync` file (recommended naming: `recordum-<device-name>-<device_id_8chars>.rcsync`).

### 1.1 Outer Binary Envelope (Byte-by-Byte Layout)

Outside, a `.rcsync` file uses the same authenticated encryption structure as `.recbak` backups (`Argon2id` + `AES-256-GCM`), with a distinct 4-byte magic (`RCSY`) so sync files and backup files can never be confused.

Total header length: **41 bytes**, followed by the AES-256-GCM ciphertext and a **16-byte** authentication tag.

| Byte Offset | Length | Type / Endianness | Value / Meaning |
|---|---|---|---|
| `0..3` | 4 B | ASCII | Magic bytes `"RCSY"` (`0x52 0x43 0x53 0x59`). |
| `4` | 1 B | `uint8` | Envelope version. MUST be `0x01`; any other value is rejected. |
| `5..7` | 3 B | `byte[3]` | Reserved (writers emit `0x00 0x00 0x00`; readers do not check them). |
| `8` | 1 B | `uint8` | KDF identifier. MUST be `0x01` (Argon2id, Argon2 version `0x13`, no secret, no associated data); any other value is rejected. |
| `9..10` | 2 B | `uint16` big-endian | Argon2id memory cost in MiB (writer default: `64`; valid reader range: `8..1024`). The memory parameter given to Argon2 is this value in KiB, i.e. header value × 1024 (`64` → `65536` KiB). |
| `11` | 1 B | `uint8` | Argon2id iteration count (writer default: `4`; valid reader range: `1..24`). |
| `12` | 1 B | `uint8` | Argon2id degree of parallelism (writer default: `2`; valid reader range: `1..8`). |
| `13..28` | 16 B | `byte[16]` | Random 128-bit Argon2id salt. |
| `29..40` | 12 B | `byte[12]` | Random 96-bit AES-GCM nonce (regenerated on every write). |
| `41..(N-17)` | `N - 57` B | `byte[]` | AES-256-GCM ciphertext of the inner ZIP archive (32-byte key = Argon2id of the UTF-8 passphrase with this header's salt and parameters; empty AAD). The hard ceiling of `2 GiB` covers ciphertext plus tag together (everything after byte 40). |
| `(N-16)..(N-1)` | 16 B | `byte[16]` | 128-bit AES-256-GCM authentication tag. |

A truncated file, a partially uploaded cloud file, or a file encrypted with a different passphrase fails the AES-GCM tag check before any plaintext is parsed.

---

## 2. Inner ZIP Archive Layout

Decrypting the `RCSY` envelope yields a standard ZIP archive with the following layout:

```text
manifest.json
data/app_setting.jsonl
data/unit_of_measure.jsonl
data/tag.jsonl
data/scale_template.jsonl
data/scale_anchor.jsonl
data/habit_node.jsonl
data/habit_config.jsonl
data/habit_config_history.jsonl
data/metric_definition.jsonl
data/segment_definition.jsonl
data/goal.jsonl
data/notification.jsonl
data/streak_pause.jsonl
data/abstinence_period.jsonl
data/habit_node_tag.jsonl
data/completion.jsonl
data/completion_segment.jsonl
data/completion_value.jsonl
data/note.jsonl
data/note_history.jsonl
data/note_tag.jsonl
data/attachment.jsonl
data/habit_template.jsonl
data/export_template.jsonl
data/chart.jsonl
data/chart_series.jsonl
tombstones.jsonl
photos/<attachment_id>.jpg         (or .png when mime_type is image/png)
photos/<attachment_id>.thumb.jpg
```

### 2.1 `manifest.json`

UTF-8 JSON object without BOM at the root of the ZIP:

```json
{
  "format": "recordum-sync",
  "format_version": "1.0",
  "device_id": "2c8b06d9-4a8e-4c4e-9d8a-3e6b9f1c2a3b",
  "device_name": "Fedora Studio",
  "app_version": "1.0.0",
  "written_at_ms": 1767225600000,
  "includes_photos": true,
  "entities": {
    "app_setting": 3,
    "habit_node": 12
  }
}
```

| Field | JSON Type | Meaning |
|---|---|---|
| `format` | `string` | Always `"recordum-sync"`. |
| `format_version` | `string` | `"<major>.<minor>"` (currently `"1.0"`). |
| `device_id` | `string` | Unique identifier of the writing device (from `sync_meta`). |
| `device_name` | `string` | Human-readable name of the writing device. |
| `app_version` | `string` | Recordum version that wrote the archive. |
| `written_at_ms` | `integer` | UTC timestamp when the archive was written, in Unix epoch milliseconds (`IClock.UtcNowMs`). |
| `includes_photos` | `boolean` | `true` when full-size photos (`photos/<id>.<ext>`) are included; `false` when only thumbnails (`photos/<id>.thumb.jpg`) are written. |
| `entities` | `object` (`string` → `integer`) | Map of public entity name to number of rows written in `data/<entity>.jsonl`. |

### 2.2 JSON Lines (`data/<entity>.jsonl` and `tombstones.jsonl`)

- **Encoding**: UTF-8 without BOM (invalid UTF-8 rejects the file), `\n` (`0x0A`) line terminator, compact JSON (one object per line). Writers do not escape non-ASCII characters or HTML-sensitive characters (`<`, `>`, `&`, `'`, `+`) as `\uXXXX`, so files diff readably; the file is never embedded in HTML. Readers accept any valid JSON escaping.
- **Row Identifier (`"id"`)**: Every line in `data/<entity>.jsonl` has `"id"` as its first property, equal to the entity's primary key column values joined with `"|"` as stored in SQLite, followed by all public non-photo fields of the entity. Readers reject a row whose `"id"` differs from its key fields joined with `"|"`, a duplicate `"id"` within one entity file, and a duplicate ZIP entry name.
- **Deterministic Ordering**: Every `data/<entity>.jsonl` file is sorted ordinally (`StringComparer.Ordinal`) by `"id"`. `tombstones.jsonl` is sorted ordinally by `("entity", "entity_id")`.
- **Value Types**:
  - Writers MUST emit exactly the forms below. They are the forms EF Core stores in SQLite (measured), and readers validate them: a value that is well-formed JSON but not in the exact form for its column is rejected (`SyncFormatException`), because the app could not load it afterwards.
  - Booleans (SQLite `INTEGER` `0`/`1`) → JSON `true` / `false`.
  - Integers (`int`, `long`, `short`, `byte`) → JSON integer number, within the range of the column type (`int` 32-bit, `short` 16-bit, `byte` 8-bit, `long` 64-bit).
  - Floating-point (`double`, `float`) → JSON number.
  - Decimals → JSON **string**, invariant culture, `.` as separator, no exponent, no thousands separator, with the scale as stored (e.g. `"125.75"`, `"0.1000000000000000055511151231"`); parsing it as a decimal and writing it back must give the same string.
  - GUIDs → JSON string, format `D` in **UPPERCASE** hex (e.g. `"ABCDEF01-0000-0000-0000-000000000002"`). Lowercase is rejected.
  - `DateTime` (UTC) → `yyyy-MM-dd HH:mm:ss[.FFFFFFF]`: space separator, no zone suffix, fractional digits only when non-zero and without trailing zeros (e.g. `"2026-02-10 14:30:00"`, `"2026-02-10 14:30:00.1234567"`).
  - `DateOnly` → `yyyy-MM-dd` (e.g. `"2026-02-03"`).
  - `TimeOnly` → `HH:mm:ss[.FFFFFFF]` (e.g. `"04:30:00"`).
  - Enums → JSON string with the member name, exactly as defined (e.g. `"Habit"`); unknown names are rejected.
  - JSON-in-TEXT columns → JSON string containing the raw JSON payload.
  - Photos (`attachment.content` and `attachment.thumbnail`, binary) → omitted from JSON rows; externalized as binary entries under `photos/`.

### 2.3 `tombstones.jsonl`

Each line records a deleted row or a natural-key alias from `sync_tombstone`:

| Field | JSON Type | Meaning |
|---|---|---|
| `entity` | `string` | Public entity name (e.g. `"note"`, `"tag"`). |
| `entity_id` | `string` | Row `"id"` (primary key values joined with `"|"`). |
| `deleted_at_ms` | `integer` | UTC deletion timestamp in Unix epoch milliseconds. |
| `device_id` | `string?` | Device ID that performed the deletion or merge. |
| `alias_of` | `string?` | Surviving target ID when this row was merged by natural key into another GUID; `null` for normal deletions. |

---

## 3. Travelling Entities and Public Fields

Local device tables (`active_timer`, `backup_log`, `export_jobs`, `ai_insights`, `sync_meta`, `sync_tombstone`, `sync_base`) never appear under `data/`.

### app_setting

Table: `app_settings` · Key: `key` · Only `daily_reset_time`, `monthly_summary_day`, and `monthly_summary_time` travel. Local settings (`language`, `theme`, `home.rows`, `onboarding_complete`, `ai_*`, `notifications_enabled`, `app_lock_*`, `inactivity_timeout_minutes`, `backup.auto.*`, `schema_version`, `sync.*`) are never written.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `key` | `string` | Plain | Setting key name (primary key). |
| `value` | `string` | Plain | Serialized setting value. |
| `updated_at` | `string` | Plain | UTC timestamp of last update. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update (LWW clock). |

### unit_of_measure

Table: `units_of_measure` · Key: `id` · Natural key: `code`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `code` | `string` | Plain | Unique machine-readable unit code (natural key). |
| `name` | `string` | Plain | Display name of the unit. |
| `symbol` | `string?` | Plain | Short unit symbol (e.g. `"km"`, `"kg"`). |
| `is_official` | `boolean` | Plain | `true` for built-in units, `false` for user-created units. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### tag

Table: `tags` · Key: `id` · Natural key: `name` (case-insensitive `NOCASE`).

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `name` | `string` | Plain | Case-insensitive unique tag name (natural key). |
| `color` | `string?` | Plain | Optional hex color (`#RRGGBB`). |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### scale_template

Table: `scale_templates` · Key: `id` · Natural key: `code`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `code` | `string` | Plain | Unique scale code (natural key). |
| `name` | `string` | Plain | Display name of the scale. |
| `min_value` | `integer` | Plain | Minimum numeric bound of the scale. |
| `max_value` | `integer` | Plain | Maximum numeric bound of the scale. |
| `precision` | `string` | Plain | Scale precision enum (`"Integer"` or `"Decimal"`). |
| `is_official` | `boolean` | Plain | `true` for built-in scales, `false` for user-created scales. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### scale_anchor

Table: `scale_anchors` · Key: `id` · Natural key: `scale_template_id|value`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `scale_template_id` | `string` | Plain | Foreign key to `scale_template.id`. |
| `value` | `integer` | Plain | Integer rung value on the scale. |
| `label` | `string?` | Plain | Human-readable label for this rung. |
| `tag_id` | `string?` | Plain | Optional derived tag foreign key (`tag.id`). |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### habit_node

Table: `habit_nodes` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `parent_id` | `string?` | Plain | Parent group `habit_node.id`, or `null` at root level. |
| `node_type` | `string` | Plain | Node kind enum (`"Habit"` or `"Group"`). |
| `habit_type` | `string?` | Plain | Habit type enum (`"Action"`, `"Counter"`, `"Resistance"`, `"Journal"`, `"State"`), or `null` for groups. |
| `name` | `string` | Plain | Habit or group title. |
| `description` | `string?` | Plain | Optional description. |
| `color` | `string` | Plain | Identifying hex color (`#RRGGBB`). |
| `icon` | `string?` | Plain | Optional Lucide icon key. |
| `sort_order` | `integer` | Plain | Sibling sort order. |
| `priority` | `integer` | Plain | Priority level. |
| `is_archived` | `boolean` | Plain | Whether the node is archived. |
| `is_locked` | `boolean` | Plain | Whether the node requires unlock. |
| `pin_scope` | `string` | Plain | Pin scope enum (`"None"`, `"Local"`, `"Global"`). |
| `reset_time` | `string?` | Plain | Optional custom day-boundary reset time (`HH:mm:ss`, see section 2.2). |
| `ai_data_sharing` | `string` | Plain | AI sharing policy enum (`"None"`, `"LocalOnly"`, `"ApiAllowed"`). |
| `ui_prefs_json` | `string?` | Plain | Per-node presentation preferences JSON. |
| `created_by_plugin_id` | `string?` | Plain | Provenance plugin ID if created by a plugin. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |
| `deleted_at` | `string?` | Plain | Soft-delete (trash) UTC timestamp, or `null` when active. |

### habit_config

Table: `habit_configs` · Key: `id` (1:1 with `habit_node` via `habit_node_id`).

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id` (unique). |
| `schedule_type` | `string` | Plain | Schedule enum (`"Daily"`, `"SpecificDays"`, `"WeeklyX"`, `"EveryXDays"`, `"Monthly"`, `"Custom"`). |
| `schedule_days_json` | `string?` | Plain | JSON array of scheduled weekdays. |
| `schedule_monthly_days_json` | `string?` | Plain | JSON array of scheduled days of the month. |
| `schedule_every_x_days` | `integer?` | Plain | Interval in days for `EveryXDays` schedules. |
| `schedule_weekly_target` | `integer?` | Plain | Target completions per week for `WeeklyX` schedules. |
| `streak_mode` | `string` | Plain | Streak calculation mode enum (`"Daily"`, `"Weekly"`, `"Occurrences"`, `"Both"`). |
| `streak_recovery` | `string` | Plain | Streak recovery mode enum (`"None"`, `"GraceDay"`, `"Manual"`, `"Retroactive"`). |
| `streak_tolerance_minutes` | `integer?` | Plain | Optional grace tolerance in minutes. |
| `streak_broken_at` | `string?` | Plain | UTC timestamp when the streak was last broken. |
| `has_integrated_timer` | `boolean` | Plain | Whether an integrated timer is enabled. |
| `timer_mode` | `string?` | Plain | Timer mode enum (`"Stopwatch"`, `"Countdown"`, `"Primary"`, `"Session"`). |
| `timer_target_seconds` | `integer?` | Plain | Target countdown duration in seconds. |
| `timer_auto_prompt` | `boolean` | Plain | Whether to prompt automatically when the timer finishes. |
| `rest_timer_seconds` | `integer?` | Plain | Rest duration in seconds between sets. |
| `session_base_timer` | `boolean` | Plain | Whether session mode uses a base timer. |
| `session_countdown_seconds` | `integer?` | Plain | Session countdown duration in seconds. |
| `session_repeat_count` | `integer?` | Plain | Number of session cycle repetitions. |
| `repeatable` | `boolean` | Plain | Whether multiple series/entries per completion are enabled. |
| `series_labels_json` | `string?` | Plain | JSON array of labels for repeatable series. |
| `quick_complete` | `boolean` | Plain | Whether single-tap quick completion is enabled. |
| `note_marker_color` | `string?` | Plain | Optional hex color for note markers. |
| `ai_data_sharing` | `string` | Plain | AI sharing policy enum (`"None"`, `"LocalOnly"`, `"ApiAllowed"`). |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### habit_config_history

Table: `habit_config_histories` · Key: `id` · History entity (append-only set).

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `schedule_type` | `string` | Plain | Snapshot of `schedule_type`. |
| `schedule_days_json` | `string?` | Plain | Snapshot of `schedule_days_json`. |
| `schedule_monthly_days_json` | `string?` | Plain | Snapshot of `schedule_monthly_days_json`. |
| `schedule_every_x_days` | `integer?` | Plain | Snapshot of `schedule_every_x_days`. |
| `schedule_weekly_target` | `integer?` | Plain | Snapshot of `schedule_weekly_target`. |
| `streak_mode` | `string` | Plain | Snapshot of `streak_mode`. |
| `streak_recovery` | `string` | Plain | Snapshot of `streak_recovery`. |
| `streak_tolerance_minutes` | `integer?` | Plain | Snapshot of `streak_tolerance_minutes`. |
| `has_integrated_timer` | `boolean` | Plain | Snapshot of `has_integrated_timer`. |
| `timer_mode` | `string?` | Plain | Snapshot of `timer_mode`. |
| `timer_target_seconds` | `integer?` | Plain | Snapshot of `timer_target_seconds`. |
| `timer_auto_prompt` | `boolean` | Plain | Snapshot of `timer_auto_prompt`. |
| `rest_timer_seconds` | `integer?` | Plain | Snapshot of `rest_timer_seconds`. |
| `session_base_timer` | `boolean` | Plain | Snapshot of `session_base_timer`. |
| `session_countdown_seconds` | `integer?` | Plain | Snapshot of `session_countdown_seconds`. |
| `session_repeat_count` | `integer?` | Plain | Snapshot of `session_repeat_count`. |
| `repeatable` | `boolean` | Plain | Snapshot of `repeatable`. |
| `series_labels_json` | `string?` | Plain | Snapshot of `series_labels_json`. |
| `valid_from` | `string` | Plain | UTC start of validity interval. |
| `valid_until` | `string?` | Plain | UTC end of validity interval, or `null` if current. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of snapshot record. |

### metric_definition

Table: `metric_definitions` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `name` | `string` | Plain | Metric display name. |
| `type` | `string` | Plain | Metric value type enum (`"Integer"`, `"Decimal"`, `"Seconds"`, `"Text"`). |
| `unit_of_measure_id` | `string?` | Plain | Optional foreign key to `unit_of_measure.id`. |
| `scale_template_id` | `string?` | Plain | Optional foreign key to `scale_template.id`. |
| `sort_order` | `integer` | Plain | Display order among the habit's metrics. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### segment_definition

Table: `segment_definitions` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `name` | `string` | Plain | Segment name (e.g. `"Work"`, `"Rest"`). |
| `duration_seconds` | `integer?` | Plain | Optional target duration in seconds (`null` for manual lap). |
| `is_rest` | `boolean` | Plain | Whether this segment is a rest interval. |
| `color` | `string?` | Plain | Optional hex color (`#RRGGBB`). |
| `sort_order` | `integer` | Plain | Sequence order within the session. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### goal

Table: `goals` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `metric_definition_id` | `string?` | Plain | Optional foreign key to `metric_definition.id`. |
| `name` | `string` | Plain | Goal title. |
| `target_value` | `string` | Plain | Exact decimal target value serialized as string. |
| `period_type` | `string` | Plain | Period enum (`"Fixed"`, `"Rolling"`, `"Open"`). |
| `period_start` | `string?` | Plain | Start date (`yyyy-MM-dd`) for `Fixed` goals. |
| `period_end` | `string?` | Plain | End date (`yyyy-MM-dd`) for `Fixed` goals. |
| `rolling_days` | `integer?` | Plain | Window length in days for `Rolling` goals. |
| `is_closed` | `boolean` | Plain | Whether the goal has been closed. |
| `closed_at` | `string?` | Plain | UTC timestamp when closed. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### notification

Table: `notifications` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `notify_type` | `string` | Plain | Notification type enum (`"Reminder"`, `"StreakMilestone"`, `"Summary"`). |
| `notify_time` | `string?` | Plain | Time of day (`HH:mm:ss`, see section 2.2) for scheduled reminders. |
| `is_enabled` | `boolean` | Plain | Whether the notification is active. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### streak_pause

Table: `streak_pauses` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `pause_from` | `string` | Plain | Pause start logical date (`yyyy-MM-dd`). |
| `pause_to` | `string` | Plain | Pause end logical date (`yyyy-MM-dd`, inclusive). |
| `reason` | `string?` | Plain | Optional user note explaining the pause. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### abstinence_period

Table: `abstinence_period` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `started_at_ms` | `integer` | Plain | UTC start instant in Unix epoch milliseconds. |
| `ended_at_ms` | `integer?` | Plain | UTC relapse/end instant in Unix epoch milliseconds (`null` while open). |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### habit_node_tag

Table: `habit_node_tags` · Key: `habit_node_id|tag_id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `tag_id` | `string` | Plain | Foreign key to `tag.id`. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### completion

Table: `completions` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `completed_at` | `string` | Plain | UTC completion timestamp. |
| `completed_at_ms` | `integer` | Plain | UTC completion instant in Unix epoch milliseconds. |
| `started_at` | `string?` | Plain | Optional UTC timer start timestamp. |
| `started_at_ms` | `integer?` | Plain | Optional UTC timer start instant in Unix epoch milliseconds. |
| `ended_at` | `string?` | Plain | Optional UTC timer end timestamp. |
| `ended_at_ms` | `integer?` | Plain | Optional UTC timer end instant in Unix epoch milliseconds. |
| `duration_seconds` | `integer?` | Plain | Total elapsed duration in seconds. |
| `is_retroactive` | `boolean` | Plain | `true` when logged retroactively (stamped at noon of the logical day). |
| `notes` | `string?` | Text | Free-form completion note (merged under Rule B on concurrent edits). |
| `last_edited_device_name` | `string?` | Plain | Human-readable name of the device that last edited `notes`. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### completion_segment

Table: `completion_segments` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `completion_id` | `string` | Plain | Foreign key to `completion.id`. |
| `segment_definition_id` | `string` | Plain | Foreign key to `segment_definition.id`. |
| `segment_index` | `integer` | Plain | Zero-based occurrence index within the completion. |
| `started_at` | `string` | Plain | UTC segment start timestamp. |
| `started_at_ms` | `integer` | Plain | UTC segment start instant in Unix epoch milliseconds. |
| `ended_at` | `string?` | Plain | Optional UTC segment end timestamp. |
| `ended_at_ms` | `integer?` | Plain | Optional UTC segment end instant in Unix epoch milliseconds. |
| `duration_seconds` | `integer?` | Plain | Segment duration in seconds. |
| `is_manual_edit` | `boolean` | Plain | Whether the segment duration was edited manually. |
| `note` | `string?` | Plain | Short segment annotation. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### completion_value

Table: `completion_values` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `completion_id` | `string` | Plain | Foreign key to `completion.id`. |
| `metric_definition_id` | `string` | Plain | Foreign key to `metric_definition.id`. |
| `completion_segment_id` | `string?` | Plain | Optional foreign key to `completion_segment.id`. |
| `series_index` | `integer` | Plain | Zero-based series index for repeatable completions. |
| `value_integer` | `integer?` | Plain | Integer metric value. |
| `value_decimal` | `string?` | Plain | Exact decimal metric value serialized as string (preserves scale values and full decimal precision). |
| `value_seconds` | `integer?` | Plain | Duration metric value in seconds. |
| `value_text` | `string?` | Plain | Text metric value. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### note

Table: `notes` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `completion_id` | `string?` | Plain | Soft foreign key to paired `completion.id` (for `"Diario"` entries). |
| `category` | `string` | Plain | Note category enum (`"Veloce"` or `"Diario"`). |
| `target_date` | `string?` | Plain | Logical calendar day (`yyyy-MM-dd`) this note belongs to. |
| `body_md` | `string` | Text | Markdown body of the diary/note entry (merged under Rule B on concurrent edits). |
| `is_sensitive` | `boolean` | Plain | Whether the entry is marked sensitive. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `last_edited_device_name` | `string?` | Plain | Human-readable name of the device that last edited `body_md`. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### note_history

Table: `note_histories` · Key: `id` · History entity (append-only set).

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `note_id` | `string` | Plain | Foreign key to `note.id`. |
| `body_md` | `string` | Plain | Immutable snapshot of the note's previous Markdown body. |
| `created_at` | `string` | Plain | UTC snapshot timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of snapshot record. |

### note_tag

Table: `note_tag` · Key: `note_id|tag_id|source`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `note_id` | `string` | Plain | Foreign key to `note.id`. |
| `tag_id` | `string` | Plain | Foreign key to `tag.id`. |
| `source` | `string` | Plain | Tag provenance enum (`"Inline"` or `"Property"`). |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### attachment

Table: `attachment` · Key: `id`. Binary photo blobs are externalized under `photos/` and omitted from `data/attachment.jsonl`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key (`<attachment_id>`). |
| `note_id` | `string` | Plain | Foreign key to `note.id`. |
| `filename` | `string` | Plain | Original or normalized file name. |
| `mime_type` | `string` | Plain | MIME type (`"image/png"` → `.png`, otherwise `.jpg`). |
| `file_size_bytes` | `integer` | Plain | Byte length of the full-resolution photo. |
| `content` | external file (`photos/<id>.<ext>`) | Photo | Full-resolution photo bytes (omitted from JSON row and omitted from ZIP when `includes_photos == false`). |
| `thumbnail` | external file (`photos/<id>.thumb.jpg`) | Photo | JPEG thumbnail bytes (omitted from JSON row; always written to `photos/<id>.thumb.jpg` when present). |
| `is_missing` | `boolean` | Plain | `true` when the original file is missing and no content is available. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### habit_template

Table: `habit_templates` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `name` | `string` | Plain | Template display name. |
| `category` | `string?` | Plain | Optional category label. |
| `description` | `string?` | Plain | Optional description. |
| `payload_json` | `string` | Plain | Serialized template composition JSON. |
| `is_official` | `boolean` | Plain | `true` for built-in templates, `false` for user-saved templates. |
| `created_by_plugin_id` | `string?` | Plain | Provenance plugin ID if created by a plugin. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### export_template

Table: `export_templates` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `name` | `string` | Plain | Export template title. |
| `description` | `string?` | Plain | Optional description. |
| `layout_json` | `string` | Plain | Serialized layout definition JSON. |
| `is_official` | `boolean` | Plain | `true` for built-in templates, `false` for user-created templates. |
| `created_by_plugin_id` | `string?` | Plain | Provenance plugin ID if created by a plugin. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### chart

Table: `charts` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `name` | `string` | Plain | Chart title. |
| `description` | `string?` | Plain | Optional chart description. |
| `chart_type` | `string` | Plain | Chart renderer type (e.g. `"line"`, `"bar"`). |
| `config_json` | `string` | Plain | Serialized chart configuration JSON. |
| `created_by_plugin_id` | `string?` | Plain | Provenance plugin ID if created by a plugin. |
| `created_at` | `string` | Plain | UTC creation timestamp. |
| `updated_at` | `string` | Plain | UTC update timestamp. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

### chart_series

Table: `chart_series` · Key: `id`.

| Public Field | JSON Type | Kind | Meaning |
|---|---|---|---|
| `id` | `string` | Plain | GUID primary key. |
| `chart_id` | `string` | Plain | Foreign key to `chart.id`. |
| `habit_node_id` | `string` | Plain | Foreign key to `habit_node.id`. |
| `metric_definition_id` | `string?` | Plain | Optional foreign key to `metric_definition.id`. |
| `label` | `string?` | Plain | Optional series legend label. |
| `color` | `string?` | Plain | Optional series hex color (`#RRGGBB`). |
| `sort_order` | `integer` | Plain | Display order within the chart. |
| `updated_at_ms` | `integer` | Plain | Unix epoch milliseconds of last update. |

---

## 4. Compatibility Rules (§6.4)

1. **Major Version Check**: Readers MUST check the `<major>` component of `manifest.format_version`. If `<major> != 1`, the reader MUST reject the archive (`SyncFormatException`) and report that Recordum needs to be updated on the peer device.
2. **Minor Version Forward Compatibility**: A newer `<minor>` version (e.g. `"1.9"`) indicates additive changes (new optional fields or new entity files). Readers MUST accept any minor version within major `1` and silently ignore unknown `data/<unknown>.jsonl` files and unknown JSON properties inside known entities.
3. **Absent Fields Are Not Null**: If a field defined in the catalog is absent from a JSON object (for example, written by an older minor version), it MUST NOT be treated as `null` or cleared. Field-level merge only compares and updates fields present in the incoming row.
4. **Atomic Validation**: All JSON Lines in `data/*.jsonl` and `tombstones.jsonl` are validated when opening the archive. A single truncated or malformed JSON line rejects the entire archive (`SyncFormatException`) so no partial state is ever applied.

---

## 5. Canonical Form of a Row

Used only to break ties deterministically. The canonical form of a row is a byte string built as follows:

1. Take the row's fields (all the fields present in the row as read, `id` excluded) and sort them by public name, comparing the **UTF-8 bytes** of the names ordinally.
2. For each field in that order emit, as UTF-8: the field name, one `0x00` byte, the value text, one `0x00` byte.
3. The value text is: for a JSON string, the string's text (unescaped); for a JSON number, its shortest round-trip invariant form (integers as plain digits without exponent or fraction; other numbers as the shortest string that parses back to the same double, like JavaScript `Number.prototype.toString`); `true` or `false`; `null` for JSON null.
4. Two rows are compared by their canonical byte strings, ordinally (bytewise); the smaller wins a tie.

Example: the fields `{"id":"X","b":2,"a":"x","c":true,"d":null}` give the bytes `a 00 x 00 b 00 2 00 c 00 true 00 d 00 null 00` (spaces added for reading).

## 6. Merge Rules Summary (§8.3)

Merging is **idempotent** and **commutative**: applying the same peer archive twice or merging peer archives in any order converges to the exact same database state.

1. **New Rows**: A row whose `"id"` does not exist locally and has no tombstone with `deleted_at_ms >= row.updated_at_ms` is inserted (entities are processed in foreign-key dependency order, parents before children).
2. **Concurrent Updates to the Same Row (`Plain` fields)**:
   - Compared field-by-field against `sync_base` (the last merged snapshot for that `(peer_device_id, entity, entity_id)`).
   - If a field changed on only one side relative to the base, that change wins even if the other side has a newer `updated_at_ms` from editing a different field.
   - If the same field changed on both sides (or no base exists yet), the row with the higher `updated_at_ms` wins; ties break deterministically by the **smaller canonical form** of the row (see "Canonical form of a row" below), compared as ordinal byte strings; if the two canonical forms are equal the rows are identical and either wins.
3. **Rule B — Free-Form Text (`Text` fields: `note.body_md`, `completion.notes`)**:
   - Diary and note text is never silently discarded.
   - If a `Text` field changed on both sides relative to `sync_base` (or differs on first merge without a base), both versions are preserved in the same record: the winning version (higher `updated_at_ms`, tie-broken by the smaller canonical form of the row) remains at the top, and the losing version is appended at the bottom using the fixed conflict block format:
     ```text
     "\n\n— <device> · yyyy-MM-dd HH:mm —\n<text>"
     ```
     where `<device>` is the losing version's `last_edited_device_name`, or the literal `?` when it is null or empty; `yyyy-MM-dd HH:mm` is the losing version's `updated_at_ms` formatted in the **merging device's local time zone**; and `<text>` is the losing text. Because the time zone is the merger's, two devices can write slightly different blocks; the next round converges through the tie rule (the smaller canonical form wins and the other block is recognised, not duplicated).
   - A text "already contains the block" when some line starts (anchored at line start) with the pattern `— <x> · \d{4}-\d{2}-\d{2} \d{2}:\d{2} —` (em dashes U+2014, middle dot U+00B7, `<x>` any text without a line break) and the part after that line equals the losing text.
   - If the winning text already contains that block, it is not appended again. The merged row takes `updated_at_ms = max(local, remote)`.
4. **Delete vs. Edit**:
   - `tombstone.deleted_at_ms` is compared against `row.updated_at_ms`. The newer action wins.
   - If deletion wins against a row whose `Text` field contains text that was not present before deletion, the losing text becomes a **new quick note** (`note`) on the same habit and the same day, carrying the rule-B block (so the text is never lost).
   - **Parent deleted, children follow**: if a parent row stays deleted, its child rows (those that reference it) are deleted with it, as the local cascade would, even if they were edited elsewhere afterwards. A `habit_node` goes to the trash (soft delete) instead, and restoring it is an edit that wins if newer.
   - Soft-deleted `habit_node` rows travel normally via `deleted_at`; restoring from trash updates `updated_at_ms` and wins as a normal edit.
5. **Natural Keys & Deduplication**:
   - `tag` (`name`, case-insensitive), `unit_of_measure` (`code`), and `scale_template` (`code`): when two devices create rows with different GUIDs and the same natural key, the lexicographically smaller GUID wins, all foreign-key references are rewritten to the winning GUID, and the discarded GUID is recorded in `tombstones.jsonl` with `alias_of` pointing to the winning GUID.
   - `scale_anchor`: matched by `(scale_template_id, value)`.
6. **History Entities (`habit_config_history`, `note_history`)**:
   - Immutable snapshots merged as append-only sets by `"id"`.
7. **Domain Invariants**:
   - Two open `abstinence_period` rows (`ended_at_ms == null`) on the same habit: the period with the earlier `started_at_ms` remains open; the later one is closed at its own `started_at_ms` (`ended_at_ms = started_at_ms`, duration zero) and counted among items to review.
   - Cycle in `habit_node.parent_id`: the more recent reparenting loses and reverts to its previous parent (or root `null` if the parent no longer exists).
