# Recordum Sync Exchange File Format (`recordum-sync` 1.0)

This document defines the public contract for Recordum device synchronization files (`.rcsync`). The format is designed for long-term stability, independence from internal SQLite migrations, inspectability by users, and interoperability with external tools.

---

## 1. Container & Encrypted Envelope (`RCSY`)

Each participating device writes its complete merged state into its own `.rcsync` file (recommended naming: `recordum-<device-name>-<device_id_8chars>.rcsync`).

### 1.1 Outer Binary Envelope (Byte-by-Byte Layout)

Outside, a `.rcsync` file is encrypted and authenticated using `Argon2id` + chunked `AES-256-GCM`, with a distinct 4-byte magic (`RCSY`) so sync files and backup files can never be confused.

Total header length: **41 bytes**, followed by a sequence of authenticated AES-256-GCM chunks (`ciphertext_i ‖ tag_i`, 16-byte tag per chunk). For envelope version `0x02`, the plaintext chunk size is fixed at **1 MiB** (`1,048,576` bytes; not stored in the header).

| Byte Offset | Length | Type / Endianness | Value / Meaning |
|---|---|---|---|
| `0..3` | 4 B | ASCII | Magic bytes `"RCSY"` (`0x52 0x43 0x53 0x59`). |
| `4` | 1 B | `uint8` | Envelope version. MUST be `0x02`. Version `0x01` is rejected as an unsupported envelope; any version `> 0x02` is rejected as a newer format requiring a newer version of Recordum. |
| `5..7` | 3 B | `byte[3]` | Reserved (writers emit `0x00 0x00 0x00`; readers do not check them). |
| `8` | 1 B | `uint8` | KDF identifier. MUST be `0x01` (Argon2id, Argon2 version `0x13`, no secret, no associated data); any other value is rejected. |
| `9..10` | 2 B | `uint16` big-endian | Argon2id memory cost in MiB (writer default: `64`; valid reader range: `8..1024`). The memory parameter given to Argon2 is this value in KiB, i.e. header value × 1024 (`64` → `65536` KiB). |
| `11` | 1 B | `uint8` | Argon2id iteration count (writer default: `4`; valid reader range: `1..24`). |
| `12` | 1 B | `uint8` | Argon2id degree of parallelism (writer default: `2`; valid reader range: `1..8`). |
| `13..28` | 16 B | `byte[16]` | Random 128-bit Argon2id salt. |
| `29..40` | 12 B | `byte[12]` | Random 96-bit base AES-GCM nonce (regenerated on every write). |
| `41..` | variable | `chunk[]` | Sequence of chunks `ciphertext_i ‖ tag_i` (`i = 0, 1, ...` up to `2^32 − 1`), encrypted with the 32-byte Argon2id key derived from the UTF-8 passphrase and this header's salt and parameters. No ciphertext byte cap applies; memory use is constant. |

Chunk framing, per-chunk nonce, and authentication:

- **Chunk size & final short chunk**: Every chunk except the last carries exactly `1 MiB` (`1,048,576` B) of plaintext (`1 MiB + 16 B` on disk). The final chunk carries **0 to `1 MiB − 1`** bytes of plaintext (`16 B` to `1 MiB + 15 B` on disk). When the plaintext length is an exact multiple of `1 MiB` (including 0 bytes), a final empty chunk (`0 B` ciphertext + `16 B` tag) is written so a full chunk is never the last chunk.
- **Per-chunk nonce**: For chunk index `i` (`uint32`, 0-based), the 96-bit AES-GCM nonce is `base_nonce XOR (7 zero bytes ‖ BE32(i) ‖ final_byte)`, where `final_byte` is `0x01` on the final chunk and `0x00` on all preceding chunks.
- **Per-chunk AAD**: Every chunk passes the exact **41-byte header** (`bytes 0..40`) as AES-256-GCM associated data (AAD), authenticating the envelope version, KDF parameters, salt, and base nonce.
- **Tag**: Each chunk ends with a **16-byte** (128-bit) AES-256-GCM authentication tag.

A truncated file (including truncation at an exact chunk boundary), reordered chunks, trailing bytes after the final chunk, a modified header byte, or a file encrypted with a different passphrase fails the AES-GCM tag check. Readers stream plaintext chunk by chunk and callers discard the output if decryption fails.

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
  },
  "merged_from": {
    "7f1e0c2a-9b3d-4f6e-8a1c-5d2b9e4f7a10": 1767225000000
  },
  "data_signature": "15:1767225600000:1767225600000:0:0:0:0:0:0:0:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855:1024/0/0:True"
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
| `merged_from` | `object` (`string` → `integer`) | For each peer `device_id`, the `written_at_ms` of the last archive from that peer merged by the writer before writing this one. It tells a peer which of its own states this device has seen (§6 rule 4). Absent or `null` = empty. |
| `data_signature` | `string?` | Opaque signature of the data carried by this archive. A peer compares it only against previous archives from the **same** device and against its own written signature to emit at most one echo (§6 rule 4). Absent or `null` = none. |

### 2.2 JSON Lines (`data/<entity>.jsonl` and `tombstones.jsonl`)

- **Encoding**: UTF-8 without BOM (invalid UTF-8 rejects the file), `\n` (`0x0A`) line terminator, compact JSON (one object per line). Writers do not escape non-ASCII characters or HTML-sensitive characters (`<`, `>`, `&`, `'`, `+`) as `\uXXXX`, so files diff readably; the file is never embedded in HTML. Readers accept any valid JSON escaping.
- **Row Identifier (`"id"`)**: Every line in `data/<entity>.jsonl` has `"id"` as its first property, equal to the entity's primary key column values joined with `"|"` as stored in SQLite, followed by all public non-photo fields of the entity. Readers reject a row whose `"id"` differs from its key fields joined with `"|"`, a duplicate `"id"` within one entity file, and a duplicate ZIP entry name.
- **Per-Field Timestamps (`"_field_ms"`)**: Optional JSON object written as the **last** property of a row only when at least one timed field (any catalog field of the entity except `updated_at_ms` and key columns, including `Photo` fields) has a timestamp strictly less than the row's `updated_at_ms`. Keys are public field names in ordinal order (`StringComparer.Ordinal`), values are non-negative 64-bit integers. When `"_field_ms"` is omitted or does not list a field, that field's timestamp is the row's `updated_at_ms`. `"_field_ms"` is metadata, not a row field: it is not included in `SyncRow.Fields`. Readers reject a `"_field_ms"` whose value is not a JSON object, any entry whose value is not an integer `>= 0`, any entry when the row lacks an integer `updated_at_ms`, or any entry greater than the row's `updated_at_ms`; an entry equal to `updated_at_ms` is accepted and ignored (equivalent to no timestamp for that field), and unknown field names (from a newer minor version), `"updated_at_ms"`, and key columns inside `"_field_ms"` are ignored.
- **Timestamp Range**: every `updated_at_ms`, `_field_ms` value and `deleted_at_ms` is an integer from `0` to `253402300799999` (the last millisecond of year 9999, .NET `DateTimeOffset.MaxValue`). A value outside this range rejects the archive (`SyncFormatException`): merging adds `+ 1` to timestamps, which must not overflow.
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

Local device tables (`active_timer`, `backup_log`, `export_jobs`, `ai_insights`, `sync_meta`, `sync_tombstone`, `sync_base`, `sync_field_ms`) never appear under `data/`.

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

## 5. Canonical Form of a Value

Used to break field ties deterministically when two versions of the same field have the same field timestamp. The canonical text of a JSON value is:

- JSON string: the string's text (unescaped).
- JSON `true` / `false` / `null`: `true`, `false`, `null`.
- JSON number that is an integer fitting a signed 64-bit integer (written without fraction or exponent): plain decimal digits with an optional leading `-` (e.g. `-12`, `0`).
- Any other JSON number (a fraction, an exponent, or an integer beyond signed 64 bits): the number is read as an IEEE-754 double and written exactly as .NET `double.ToString("R", CultureInfo.InvariantCulture)` does: the shortest digit string that parses back to the same double, `.` as decimal separator, a leading `-` for negatives, and
  - **fixed notation** when the decimal exponent `x` of the value (value = `d.ddd × 10^x`) satisfies `-4 <= x <= 16`, with no trailing zeros and no trailing `.` (e.g. `0.0001`, `1000000000000000` for `1E+15`, `10000000000000000` for `1E+16`, `99999999999999980`);
  - **scientific notation** otherwise (`x <= -5` or `x >= 17`): the shortest digits as `d` or `d.ddd`, then `E`, an explicit sign (`+` or `-`) and at least two exponent digits (e.g. `1E-05` for `0.00001`, `1.5E-05`, `1E+17`, `1.2345678901234567E+19` for `12345678901234567000`, `1.8446744073709552E+19` for the integer `18446744073709551616`, `1E+300`).
  Thresholds measured, not derived: `0.0001` fixed, `0.00001` scientific; `99999999999999980` fixed, `1E+17` scientific. This is *not* JavaScript's rule (JavaScript switches at `1e21` and `1e-7`); implement it as stated.
- JSON object or array: the raw JSON text of the value as it appears in the file. No column uses object or array values today.

Two values of the same field are compared by the **UTF-8 bytes** of their canonical value text, ordinally (bytewise); the smaller wins a tie.

## 6. Merge Rules Summary (§8.3)

Merging is **idempotent** and **convergent**:

- Applying the same peer archive twice changes nothing the second time.
- Devices that keep exchanging archives reach the same state.
- `Plain` and `Photo` field values and their per-field timestamps form a join-semilattice and do not depend on the order archives are read in, with or without a base (`sync_base`); `Text` fields and deletions converge across exchanges without losing text.

1. **New Rows**: A row whose `"id"` does not exist locally and has no tombstone with `deleted_at_ms >= row.updated_at_ms` is inserted (entities are processed in foreign-key dependency order, parents before children), together with its `_field_ms` entries (`< row.updated_at_ms`).
2. **Concurrent Updates to the Same Row (`Plain` and `Photo` fields, Ruling 20)**:
   - A local edit always beats the version it edits: if an `UPDATE` writes an `updated_at_ms` not strictly greater than the previous row's timestamp (`<= OLD.updated_at_ms`, e.g. same millisecond or a local clock behind a peer's timestamp), the `trg_sync_upd_*` trigger bumps `updated_at_ms` to `MAX(recordum_now_ms(), OLD.updated_at_ms + 1)` while `trg_sync_fms_*` records `OLD.updated_at_ms` in `sync_field_ms` for unchanged fields so only the edited fields advance to the new timestamp.
   - Each field carries its own timestamp: locally from `sync_field_ms` (or the local row's `updated_at_ms` when absent); remotely from `_field_ms[field]` (or the remote row's `updated_at_ms` when absent).
   - For each non-key field present in the incoming row (`updated_at_ms` excluded):
     - If the local and remote values are equal, the value stays and its timestamp becomes `max(local_field_ms, remote_field_ms)`.
     - If a `Text` field differs between local and remote, Rule 3 applies.
     - Otherwise, the value with the higher field timestamp wins (`remote_field_ms > local_field_ms` → remote; `remote_field_ms < local_field_ms` → local); ties break deterministically by the **smaller canonical value text** (§5), compared ordinally as UTF-8 bytes.
   - Fields absent from the incoming row (older minor version) keep their local value and local field timestamp.
   - `updated_at_ms` is not merged as a field: the merged row's `updated_at_ms` is the maximum of the local row's `updated_at_ms`, the remote row's `updated_at_ms`, and all merged field timestamps (a row can be newer than all of its fields, for example after an `UPDATE` that changed none of them). Locally, `sync_field_ms` stores only fields whose timestamp is strictly less than the row's `updated_at_ms`.
   - `sync_base` for `(peer_device_id, entity, entity_id)` stores the remote row as last merged (in public form, with photo fields as lowercase hex SHA-256) so Rule 3 knows which text pieces the peer already had and Rule 4 knows whether a deleted text was already seen. A remote row whose `updated_at_ms` is lower than the base's is stale (an older file from the same peer) and is ignored entirely.
   - Photos (`Photo` fields) follow the same per-field timestamp rule, compared by SHA-256 hash (ties break by the smaller lowercase hex hash). When a local photo is present, the remote photo is read and hashed only if it can win: it is not older than the local one (`remote_field_ms >= local_field_ms`), and not at the same field timestamp (`remote_field_ms == local_field_ms`) with the remote row's `updated_at_ms` unchanged since `sync_base` (where the hash tie-break was already decided). A photo missing from the archive (or a full-size `content` when the receiver does not keep originals) is not a change. A local photo that is missing is filled from the archive with the remote field timestamp.
3. **Rule B — Free-Form Text (`Text` fields: `note.body_md`, `completion.notes`)**:
   - Diary and note text is never silently lost.
   - When the local and remote `Text` values differ, the version with the higher field timestamp wins (`remote_field_ms > local_field_ms` → remote; `<` → local); ties break by the **smaller canonical value text** (§5).
   - The losing version is discarded (keeping the winning text as-is with `max(local_field_ms, remote_field_ms)`) **only if** the writer of the winning version already had it:
     - (a) every piece of the losing text is in the winning text or in this peer's `sync_base` entry for the row (when present); **or**
     - (b) the losing text is this device's local text and the peer's `manifest.merged_from` for this device is at or after the row's *last change* (defined in Rule 4); **or**
     - (c) the losing text is the peer's remote text and every piece of it is in the winning text or in any `sync_base` entry this device holds for the row (from any peer).
   - Otherwise, both versions are preserved in the same record: the winning version remains at the top, and the losing version is appended at the bottom using the fixed conflict block format:
     ```text
     "\n\n— <device> · yyyy-MM-dd HH:mm —\n<text>"
     ```
     where `<device>` is the losing version's `last_edited_device_name`, or the literal `?` when it is null or empty; `yyyy-MM-dd HH:mm` is the losing field's timestamp formatted in the **merging device's local time zone**; and `<text>` is the losing text. Because the time zone is the merger's, two devices can write slightly different blocks; the next round converges through the tie rule (the smaller canonical value text wins and the other block is recognised, not duplicated).
   - A block header is a line that starts (anchored at line start) with the pattern `— <x> · \d{4}-\d{2}-\d{2} \d{2}:\d{2} —` (em dashes U+2014, middle dot U+00B7, `<x>` any text without a line break). A text is read as **pieces**: the *head* (everything before the first header, without the `\n\n` that precedes it) and the *body* of each block (from the line after its header to the `\n\n` before the next header, or to the end).
   - Merging adds only the losing pieces the winning text does not already have (compared exactly): the losing head becomes a new block with the losing version's device and time; the losing blocks are copied with their own headers. The blocks are then written in order of their header time text, then device, then body (ordinal), so the result does not depend on the order in which files arrive. If nothing is new, the winning text is kept as it is; when the losing text already contains every piece of the winning one (for example the winning version is the head of a text merged elsewhere), the losing text is kept. So a version is never present twice, and a merged text that comes back is not merged again.
   - If the merged result equals one of the two versions, it takes `max(local_field_ms, remote_field_ms)`; if it produces a new combined text differing from both, its field timestamp is `max(local_field_ms, remote_field_ms) + 1`, so it beats both versions on every device. When the winning text is empty, the result starts directly with `— ` without the two leading newlines. `last_edited_device_name` follows the text: it takes the value from the version at the top and the text's field timestamp.
   - A cleanup (removing conflict blocks) done before having merged the peer's combined text can make the removed block return until the peer's `merged_from` confirms it has seen the cleanup: text is never lost.
4. **Delete vs. Edit**:
   - `tombstone.deleted_at_ms` is compared against `row.updated_at_ms`. The newer action wins; on a tie the deletion wins.
   - Remote tombstones are always copied (two tombstones for the same row keep the later `deleted_at_ms`). After the merge, a tombstone whose row exists locally with a newer `updated_at_ms` is dropped, so every device ends up with the same set.
   - **Echo once (`data_signature`)**: when merging a peer's archive changes nothing locally (for example, a cleanup or deletion already won here), the receiver still writes its own archive once so its updated `merged_from` reaches the peer, provided the peer's `data_signature` is present, differs from the last `data_signature` previously acknowledged for that peer, and differs from this device's own current `data_signature`. The peer's `data_signature` is saved locally only after the own archive is written; because the echo carries the writer's unchanged `data_signature`, it triggers no further echoes.
   - Besides the remote row, `sync_base` keeps local-only facts about when this device's row last changed, used by Rule 3 and Rule 4 to tell whether a peer had already seen a local row before overriding its text or deleting it: its `updated_at_ms` (`_local_ms`) and this device's clock at the merge that gave the row that version (`_merged_at`). The merge time is stamped only by a merge that changed the row (an insert, a field, a photo); a later merge that leaves the row as it is carries the earlier stamp forward, or stamps nothing. A row's *last change* is the earliest `_merged_at` held by a base that records its current `updated_at_ms`. If no base holds one, the version is a local edit and its last change is its `updated_at_ms` plus a margin of `5000` ms: the edit's time is taken before it is saved, so a file written just after that time may not contain it.
   - If deletion wins against a row whose `Text` field contains text that was not present before deletion, the losing text becomes a **new quick note** (`note`) on the same habit and the same day, carrying the rule-B block starting directly with `— ` without the two leading newlines (so the text is never lost).
     - A local row deleted by the peer's tombstone is rescued unless all its pieces are in the base for that row. Without a base, it is rescued unless the peer's `merged_from` for this device is at or after the row's *last change*: then the peer had seen this version when it wrote the tombstone. If `merged_from` does not name this device, the row is rescued (design §8.4: without a base a text difference is a conflict).
     - A peer's row that arrives after a local deletion is rescued unless its text is all in some base this device holds for that row, from any peer (bases survive a local deletion). With no base at all, the row was created here and is coming back, so it is not rescued.
     - The note: `id` = the first 16 bytes of SHA-256 over the UTF-8 of `recordum-sync-salvage`, entity name, row id, the row's `updated_at_ms` (decimal digits) and the text, separated by `0x00`, read as a .NET `Guid` and written uppercase `D` (two devices rescuing the same text create the same note); `category` `Veloce`; `completion_id` null; `habit_node_id` of the row; for a `note`, its `target_date`, `is_sensitive` and `created_at`; for a `completion`, `target_date` null, `is_sensitive` false and `created_at` = `completed_at`; `last_edited_device_name` of the row; `updated_at_ms` = the tombstone's `deleted_at_ms`. It is inserted with `ON CONFLICT DO NOTHING`, together with one `note_history` row: `id` from the same hash over `recordum-sync-salvage`, `note_history`, the note id, the tombstone's `deleted_at_ms` and an empty text; the note's `body_md` and `created_at`; the same `updated_at_ms`.
     - No note is created when one with that id exists or has a tombstone (here or in the file being merged): a rescued note that the user deleted stays deleted.
   - **Parent deleted, children follow**: if a parent row stays deleted, its child rows (those that reference it) are deleted with it, as the local cascade would, even if they were edited elsewhere afterwards. This also covers a child that arrives from a peer after its parent was deleted here. A reference whose local cascade is `SET NULL` is set to null instead. A `habit_node` goes to the trash (soft delete) instead, and restoring it is an edit that wins if newer.
   - Soft-deleted `habit_node` rows travel normally via `deleted_at`; restoring from trash updates `updated_at_ms` and wins as a normal edit.
5. **Natural Keys & Deduplication**:
   - `tag` (`name`, case-insensitive), `unit_of_measure` (`code`), and `scale_template` (`code`): when two devices create rows with different GUIDs and the same natural key, the lexicographically smaller GUID wins, all foreign-key references are rewritten to the winning GUID, and the discarded GUID is recorded in `tombstones.jsonl` with `alias_of` pointing to the winning GUID.
   - `scale_anchor`: matched by `(scale_template_id, value)`.
6. **History Entities (`habit_config_history`, `note_history`)**:
   - Immutable snapshots merged as append-only sets by `"id"`.
7. **Domain Invariants**:
   - Two open `abstinence_period` rows (`ended_at_ms == null`) on the same habit: the period with the earlier `started_at_ms` remains open; the later one is closed at its own `started_at_ms` (`ended_at_ms = started_at_ms`, duration zero) and counted among items to review.
   - Cycle in `habit_node.parent_id`: the more recent reparenting loses and reverts to its previous parent (or root `null` if the parent no longer exists).
