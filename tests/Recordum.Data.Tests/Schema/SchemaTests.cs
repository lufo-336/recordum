using Microsoft.Data.Sqlite;
using Xunit;

namespace Recordum.Data.Tests.Schema;

/// <summary>
/// Verifies that all migrations produce all 28 expected tables and §6.4 indexes.
/// </summary>
public sealed class SchemaTests : IClassFixture<SchemaFixture>
{
    private readonly SchemaFixture _fixture;

    public SchemaTests(SchemaFixture fixture)
    {
        _fixture = fixture;
    }

    // 30 tables: 25 from InitialSchema + active_timer added in ActiveTimerV25 + backup_log added in BackupLogV27 + note_histories added in NoteHistoryV29 + abstinence_period added in AbstinencePeriodV212 + note_tag added in NoteTagAttachmentBlobV219 (snake_case, EF naming)
    private static readonly string[] ExpectedTables =
    [
        "abstinence_period",
        "backup_log",
        "habit_nodes",
        "habit_configs",
        "habit_config_histories",
        "units_of_measure",
        "scale_templates",
        "metric_definitions",
        "segment_definitions",
        "completions",
        "completion_values",
        "completion_segments",
        "streak_pauses",
        "note_tag",
        "notes",
        "note_histories",
        "attachment",
        "goals",
        "notifications",
        "tags",
        "habit_node_tags",
        "habit_templates",
        "export_templates",
        "export_jobs",
        "charts",
        "chart_series",
        "ai_insights",
        "app_settings",
        "sync_meta",
        "active_timer",
    ];

    // Indexes from CLAUDE.md §6.4 (EF Core naming: ix_{table}_{columns})
    private static readonly string[] ExpectedIndexes =
    [
        "ix_habit_nodes_parent_id",
        "ix_habit_nodes_node_type",
        "ix_habit_nodes_deleted_at",
        "ix_habit_configs_habit_node_id",
        "ix_habit_config_histories_habit_node_id_valid_from",
        "ix_completions_habit_node_id_completed_at",
        "ix_completion_values_completion_id",
        "ix_completion_values_metric_definition_id",
        "ix_completion_segments_completion_id_segment_index",
        "ix_streak_pause_habit_node_id_pause_from",
        "ix_notes_habit_node_id_created_at",
        "ix_note_histories_note_id_created_at",
        "ix_attachment_note_id",
        "ix_goals_habit_node_id",
        "ix_notifications_habit_node_id",
        "ix_tag_name",
        "ix_chart_series_chart_id_sort_order",
        "ix_ai_insights_habit_node_id_valid_until",
        "ix_backup_log_created_at",
        "ix_export_jobs_created_at",
        "ix_sync_meta_device_id",
        "ix_abstinence_period_habit_started",
    ];

    [Fact]
    public void All_30_tables_exist_after_migration()
    {
        using var conn = _fixture.OpenRawConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('__EFMigrationsHistory', '__EFMigrationsLock', 'sqlite_sequence')";

        var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            actualTables.Add(reader.GetString(0));

        Assert.Equal(30, actualTables.Count);

        foreach (var expected in ExpectedTables)
            Assert.Contains(expected, actualTables);
    }

    [Fact]
    public void All_spec_6_4_indexes_exist_after_migration()
    {
        using var conn = _fixture.OpenRawConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'";

        var actualIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            actualIndexes.Add(reader.GetString(0));

        foreach (var expected in ExpectedIndexes)
            Assert.Contains(expected, actualIndexes);
    }
}
