using System.Collections.ObjectModel;

namespace Recordum.Data.Sync;

/// <summary>
/// Contratto congelato di ciò che viaggia nel formato "recordum-sync" e delle tabelle/chiavi locali.
/// Al giorno 1 ogni <see cref="SyncField.PublicName"/> coincide con la colonna snake_case;
/// se una colonna interna cambia nome, si aggiorna <see cref="SyncField.Column"/>, mai <see cref="SyncField.PublicName"/>.
/// </summary>
public static class SyncCatalog
{
    private static readonly string[] IdKey = ["id"];
    private static readonly string[] AppSettingKey = ["key"];
    private static readonly string[] HabitNodeTagKey = ["habit_node_id", "tag_id"];
    private static readonly string[] NoteTagKey = ["note_id", "tag_id", "source"];

    private static readonly string[] NoNaturalKey = [];
    private static readonly string[] TagNaturalKey = ["name"];
    private static readonly string[] CodeNaturalKey = ["code"];
    private static readonly string[] ScaleAnchorNaturalKey = ["scale_template_id", "value"];

    public static IReadOnlyList<SyncEntity> Entities { get; } = Array.AsReadOnly(BuildEntities());

    private static readonly Dictionary<string, SyncEntity> ByPublicName =
        Entities.ToDictionary(e => e.PublicName, StringComparer.Ordinal);

    public static IReadOnlySet<string> LocalTables { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "active_timer",
        "backup_log",
        "export_jobs",
        "ai_insights",
        "sync_meta",
        // Aggiunte dal Task 2:
        "sync_tombstone",
        "sync_base",
        "sync_field_ms",
    };

    public static IReadOnlyDictionary<string, string> ExcludedColumns { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Colonne già presenti nel contratto ma aggiunte fisicamente al DB dal Task 2;
    /// il Task 2 svuota questo elenco appena migra lo schema.
    /// </summary>
    public static IReadOnlySet<string> PendingColumns { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Indici unici non-PK che non possono generare duplicati tra dispositivi diversi.</summary>
    public static IReadOnlyDictionary<string, string> UniqueIndexExceptions { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["habit_configs.habit_node_id"] = "1:1 con habit_node: una sola configurazione per abitudine",
        });

    public static IReadOnlySet<string> TravellingSettingKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "daily_reset_time",
        "monthly_summary_day",
        "monthly_summary_time",
    };

    /// <summary>Chiavi di AppSettingSeed che restano locali al dispositivo.</summary>
    public static IReadOnlySet<string> LocalSettingKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "language",
        "theme",
        "onboarding_complete",
        "ai_mode",
        "ai_default_sharing",
        "notifications_enabled",
        "app_lock_enabled",
        "app_lock_type",
        "inactivity_timeout_minutes",
        "backup.auto.enabled",
        "backup.auto.frequency_days",
        "backup.auto.retention_count",
        "backup.auto.last_run_utc",
        "backup.auto.path",
        "schema_version",
    };

    /// <summary>Ruling 20: i campi che hanno un'ora propria (<c>sync_field_ms</c>, <c>_field_ms</c> nel formato): tutti
    /// tranne <c>updated_at_ms</c> e le colonne chiave, foto comprese.</summary>
    public static IReadOnlyList<SyncField> TimedFields(SyncEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.Fields
            .Where(f => f.PublicName != "updated_at_ms" && !entity.KeyColumns.Contains(f.Column))
            .ToList();
    }

    public static SyncEntity Get(string publicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicName);
        return ByPublicName.TryGetValue(publicName, out var entity)
            ? entity
            : throw new KeyNotFoundException($"Unknown sync entity '{publicName}'.");
    }

    private static SyncField Plain(string column) => new(column, column, SyncFieldKind.Plain);
    private static SyncField Text(string column) => new(column, column, SyncFieldKind.Text);
    private static SyncField Photo(string column) => new(column, column, SyncFieldKind.Photo);

    private static SyncEntity[] BuildEntities() =>
    [
        new(
            Table: "app_settings",
            PublicName: "app_setting",
            KeyColumns: AppSettingKey,
            Fields:
            [
                Plain("key"),
                Plain("value"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "units_of_measure",
            PublicName: "unit_of_measure",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("code"),
                Plain("name"),
                Plain("symbol"),
                Plain("is_official"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: CodeNaturalKey,
            IsHistory: false),
        new(
            Table: "tags",
            PublicName: "tag",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("name"),
                Plain("color"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: TagNaturalKey,
            IsHistory: false),
        new(
            Table: "scale_templates",
            PublicName: "scale_template",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("code"),
                Plain("name"),
                Plain("min_value"),
                Plain("max_value"),
                Plain("precision"),
                Plain("is_official"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: CodeNaturalKey,
            IsHistory: false),
        new(
            Table: "scale_anchors",
            PublicName: "scale_anchor",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("scale_template_id"),
                Plain("value"),
                Plain("label"),
                Plain("tag_id"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: ScaleAnchorNaturalKey,
            IsHistory: false),
        new(
            Table: "habit_nodes",
            PublicName: "habit_node",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("parent_id"),
                Plain("node_type"),
                Plain("habit_type"),
                Plain("name"),
                Plain("description"),
                Plain("color"),
                Plain("icon"),
                Plain("sort_order"),
                Plain("priority"),
                Plain("is_archived"),
                Plain("is_locked"),
                Plain("pin_scope"),
                Plain("reset_time"),
                Plain("ai_data_sharing"),
                Plain("ui_prefs_json"),
                Plain("created_by_plugin_id"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
                Plain("deleted_at"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "habit_configs",
            PublicName: "habit_config",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("schedule_type"),
                Plain("schedule_days_json"),
                Plain("schedule_monthly_days_json"),
                Plain("schedule_every_x_days"),
                Plain("schedule_weekly_target"),
                Plain("streak_mode"),
                Plain("streak_recovery"),
                Plain("streak_tolerance_minutes"),
                Plain("streak_broken_at"),
                Plain("has_integrated_timer"),
                Plain("timer_mode"),
                Plain("timer_target_seconds"),
                Plain("timer_auto_prompt"),
                Plain("rest_timer_seconds"),
                Plain("session_base_timer"),
                Plain("session_countdown_seconds"),
                Plain("session_repeat_count"),
                Plain("repeatable"),
                Plain("series_labels_json"),
                Plain("quick_complete"),
                Plain("note_marker_color"),
                Plain("ai_data_sharing"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "habit_config_histories",
            PublicName: "habit_config_history",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("schedule_type"),
                Plain("schedule_days_json"),
                Plain("schedule_monthly_days_json"),
                Plain("schedule_every_x_days"),
                Plain("schedule_weekly_target"),
                Plain("streak_mode"),
                Plain("streak_recovery"),
                Plain("streak_tolerance_minutes"),
                Plain("has_integrated_timer"),
                Plain("timer_mode"),
                Plain("timer_target_seconds"),
                Plain("timer_auto_prompt"),
                Plain("rest_timer_seconds"),
                Plain("session_base_timer"),
                Plain("session_countdown_seconds"),
                Plain("session_repeat_count"),
                Plain("repeatable"),
                Plain("series_labels_json"),
                Plain("valid_from"),
                Plain("valid_until"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: true),
        new(
            Table: "metric_definitions",
            PublicName: "metric_definition",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("name"),
                Plain("type"),
                Plain("unit_of_measure_id"),
                Plain("scale_template_id"),
                Plain("sort_order"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "segment_definitions",
            PublicName: "segment_definition",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("name"),
                Plain("duration_seconds"),
                Plain("is_rest"),
                Plain("color"),
                Plain("sort_order"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "goals",
            PublicName: "goal",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("metric_definition_id"),
                Plain("name"),
                Plain("target_value"),
                Plain("period_type"),
                Plain("period_start"),
                Plain("period_end"),
                Plain("rolling_days"),
                Plain("is_closed"),
                Plain("closed_at"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "notifications",
            PublicName: "notification",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("notify_type"),
                Plain("notify_time"),
                Plain("is_enabled"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "streak_pauses",
            PublicName: "streak_pause",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("pause_from"),
                Plain("pause_to"),
                Plain("reason"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "abstinence_period",
            PublicName: "abstinence_period",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("started_at_ms"),
                Plain("ended_at_ms"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "habit_node_tags",
            PublicName: "habit_node_tag",
            KeyColumns: HabitNodeTagKey,
            Fields:
            [
                Plain("habit_node_id"),
                Plain("tag_id"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "completions",
            PublicName: "completion",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("completed_at"),
                Plain("completed_at_ms"),
                Plain("started_at"),
                Plain("started_at_ms"),
                Plain("ended_at"),
                Plain("ended_at_ms"),
                Plain("duration_seconds"),
                Plain("is_retroactive"),
                Text("notes"),
                Plain("last_edited_device_name"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "completion_segments",
            PublicName: "completion_segment",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("completion_id"),
                Plain("segment_definition_id"),
                Plain("segment_index"),
                Plain("started_at"),
                Plain("started_at_ms"),
                Plain("ended_at"),
                Plain("ended_at_ms"),
                Plain("duration_seconds"),
                Plain("is_manual_edit"),
                Plain("note"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "completion_values",
            PublicName: "completion_value",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("completion_id"),
                Plain("metric_definition_id"),
                Plain("completion_segment_id"),
                Plain("series_index"),
                Plain("value_integer"),
                Plain("value_decimal"),
                Plain("value_seconds"),
                Plain("value_text"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "notes",
            PublicName: "note",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("habit_node_id"),
                Plain("completion_id"),
                Plain("category"),
                Plain("target_date"),
                Text("body_md"),
                Plain("is_sensitive"),
                Plain("created_at"),
                Plain("last_edited_device_name"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "note_histories",
            PublicName: "note_history",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("note_id"),
                Plain("body_md"),
                Plain("created_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: true),
        new(
            Table: "note_tag",
            PublicName: "note_tag",
            KeyColumns: NoteTagKey,
            Fields:
            [
                Plain("note_id"),
                Plain("tag_id"),
                Plain("source"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "attachment",
            PublicName: "attachment",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("note_id"),
                Plain("filename"),
                Plain("mime_type"),
                Plain("file_size_bytes"),
                Photo("content"),
                Photo("thumbnail"),
                Plain("is_missing"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "habit_templates",
            PublicName: "habit_template",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("name"),
                Plain("category"),
                Plain("description"),
                Plain("payload_json"),
                Plain("is_official"),
                Plain("created_by_plugin_id"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "export_templates",
            PublicName: "export_template",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("name"),
                Plain("description"),
                Plain("layout_json"),
                Plain("is_official"),
                Plain("created_by_plugin_id"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "charts",
            PublicName: "chart",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("name"),
                Plain("description"),
                Plain("chart_type"),
                Plain("config_json"),
                Plain("created_by_plugin_id"),
                Plain("created_at"),
                Plain("updated_at"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
        new(
            Table: "chart_series",
            PublicName: "chart_series",
            KeyColumns: IdKey,
            Fields:
            [
                Plain("id"),
                Plain("chart_id"),
                Plain("habit_node_id"),
                Plain("metric_definition_id"),
                Plain("label"),
                Plain("color"),
                Plain("sort_order"),
                Plain("updated_at_ms"),
            ],
            NaturalKey: NoNaturalKey,
            IsHistory: false),
    ];
}
