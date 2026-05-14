using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Prefer static readonly fields
#pragma warning disable CA1825 // Avoid zero-length array allocations

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "charts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    chart_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    config_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_by_plugin_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "export_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    layout_json = table.Column<string>(type: "TEXT", nullable: false),
                    is_official = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_by_plugin_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_export_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "habit_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    parent_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    node_type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    habit_type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_pinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    is_locked = table.Column<bool>(type: "INTEGER", nullable: false),
                    ai_data_sharing = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    reset_time = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    created_by_plugin_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_nodes", x => x.id);
                    table.CheckConstraint("ck_habit_node_ai_data_sharing", "ai_data_sharing IN ('None','LocalOnly','ApiAllowed')");
                    table.CheckConstraint("ck_habit_node_group_has_no_habit_type", "(node_type = 'Habit' AND habit_type IS NOT NULL) OR (node_type = 'Group' AND habit_type IS NULL)");
                    table.CheckConstraint("ck_habit_node_habit_type", "habit_type IS NULL OR habit_type IN ('Action','Counter','Resistance','Journal','State')");
                    table.CheckConstraint("ck_habit_node_node_type", "node_type IN ('Habit','Group')");
                    table.ForeignKey(
                        name: "fk_habit_nodes_habit_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "habit_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    is_official = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_by_plugin_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scale_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    min_value = table.Column<int>(type: "INTEGER", nullable: false),
                    max_value = table.Column<int>(type: "INTEGER", nullable: false),
                    labels_json = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    is_official = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scale_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_meta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    device_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    last_sync_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_meta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    symbol = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    is_official = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units_of_measure", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "export_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    export_template_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    format = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    filter_json = table.Column<string>(type: "TEXT", nullable: true),
                    generated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_export_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_export_jobs_export_templates_export_template_id",
                        column: x => x.export_template_id,
                        principalTable: "export_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_insights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    insight_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    body_md = table.Column<string>(type: "TEXT", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    generated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    valid_until = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_rating = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_insights", x => x.id);
                    table.CheckConstraint("ck_ai_insight_user_rating", "user_rating IS NULL OR (user_rating BETWEEN 1 AND 5)");
                    table.ForeignKey(
                        name: "fk_ai_insights_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    started_at_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    ended_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ended_at_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    duration_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    is_retroactive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_completions_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "habit_config_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    valid_from = table.Column<DateTime>(type: "TEXT", nullable: false),
                    valid_until = table.Column<DateTime>(type: "TEXT", nullable: true),
                    schedule_type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    schedule_days_json = table.Column<string>(type: "TEXT", nullable: true),
                    schedule_weekly_target = table.Column<int>(type: "INTEGER", nullable: true),
                    schedule_every_x_days = table.Column<int>(type: "INTEGER", nullable: true),
                    schedule_monthly_days_json = table.Column<string>(type: "TEXT", nullable: true),
                    streak_mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    streak_recovery = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    streak_tolerance_minutes = table.Column<int>(type: "INTEGER", nullable: true),
                    has_integrated_timer = table.Column<bool>(type: "INTEGER", nullable: false),
                    timer_mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    timer_target_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    timer_auto_prompt = table.Column<bool>(type: "INTEGER", nullable: false),
                    rest_timer_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    session_base_timer = table.Column<bool>(type: "INTEGER", nullable: false),
                    session_countdown_seconds = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_config_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_config_histories_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "habit_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    schedule_type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    schedule_days_json = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    schedule_weekly_target = table.Column<int>(type: "INTEGER", nullable: true),
                    schedule_every_x_days = table.Column<int>(type: "INTEGER", nullable: true),
                    schedule_monthly_days_json = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    streak_mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    streak_recovery = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    streak_tolerance_minutes = table.Column<int>(type: "INTEGER", nullable: true),
                    has_integrated_timer = table.Column<bool>(type: "INTEGER", nullable: false),
                    timer_mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    timer_target_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    timer_auto_prompt = table.Column<bool>(type: "INTEGER", nullable: false),
                    rest_timer_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    session_base_timer = table.Column<bool>(type: "INTEGER", nullable: false),
                    session_countdown_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    quick_complete = table.Column<bool>(type: "INTEGER", nullable: false),
                    ai_data_sharing = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_configs_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    body_md = table.Column<string>(type: "TEXT", nullable: false),
                    is_sensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_notes_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notify_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    notify_time = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "segment_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_rest = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_segment_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_segment_definitions_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "streak_pauses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pause_from = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    pause_to = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_streak_pauses", x => x.id);
                    table.CheckConstraint("ck_streak_pause_to_gte_from", "pause_to >= pause_from");
                    table.ForeignKey(
                        name: "fk_streak_pauses_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "habit_node_tags",
                columns: table => new
                {
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tag_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_node_tags", x => new { x.habit_node_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_habit_node_tags_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_habit_node_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metric_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    unit_of_measure_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    scale_template_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    labels_json = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_metric_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_metric_definitions_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_metric_definitions_scale_templates_scale_template_id",
                        column: x => x.scale_template_id,
                        principalTable: "scale_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_metric_definitions_units_of_measure_unit_of_measure_id",
                        column: x => x.unit_of_measure_id,
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completion_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    note_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    caption = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachment_exactly_one_parent", "(completion_id IS NOT NULL OR note_id IS NOT NULL) AND NOT (completion_id IS NOT NULL AND note_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_attachments_completions_completion_id",
                        column: x => x.completion_id,
                        principalTable: "completions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attachments_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "completion_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completion_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    segment_definition_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    segment_index = table.Column<int>(type: "INTEGER", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    started_at_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    ended_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ended_at_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    duration_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    is_manual_edit = table.Column<bool>(type: "INTEGER", nullable: false),
                    note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_completion_segments", x => x.id);
                    table.ForeignKey(
                        name: "fk_completion_segments_completions_completion_id",
                        column: x => x.completion_id,
                        principalTable: "completions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_completion_segments_segment_definitions_segment_definition_id",
                        column: x => x.segment_definition_id,
                        principalTable: "segment_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chart_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    chart_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    metric_definition_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chart_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_chart_series_charts_chart_id",
                        column: x => x.chart_id,
                        principalTable: "charts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chart_series_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_chart_series_metric_definitions_metric_definition_id",
                        column: x => x.metric_definition_id,
                        principalTable: "metric_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    metric_definition_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    target_value = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    period_type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    period_start = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    period_end = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    rolling_days = table.Column<int>(type: "INTEGER", nullable: true),
                    is_closed = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    closed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goals", x => x.id);
                    table.CheckConstraint("ck_goal_period_consistency", "(period_type = 'Fixed' AND period_start IS NOT NULL AND period_end IS NOT NULL) OR (period_type = 'Rolling' AND rolling_days IS NOT NULL) OR (period_type = 'Open')");
                    table.CheckConstraint("ck_goal_period_type", "period_type IN ('Fixed','Rolling','Open')");
                    table.ForeignKey(
                        name: "fk_goals_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goals_metric_definitions_metric_definition_id",
                        column: x => x.metric_definition_id,
                        principalTable: "metric_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "completion_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completion_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    metric_definition_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completion_segment_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    series_index = table.Column<int>(type: "INTEGER", nullable: false),
                    value_integer = table.Column<long>(type: "INTEGER", nullable: true),
                    value_decimal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    value_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    value_text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_completion_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_completion_values_completion_segments_completion_segment_id",
                        column: x => x.completion_segment_id,
                        principalTable: "completion_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_completion_values_completions_completion_id",
                        column: x => x.completion_id,
                        principalTable: "completions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_completion_values_metric_definitions_metric_definition_id",
                        column: x => x.metric_definition_id,
                        principalTable: "metric_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "key", "updated_at", "value" },
                values: new object[,]
                {
                    { "ai_default_sharing", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "none" },
                    { "ai_mode", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "disabled" },
                    { "app_lock_enabled", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "false" },
                    { "app_lock_type", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "pin" },
                    { "daily_reset_time", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "00:00" },
                    { "inactivity_timeout_minutes", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "5" },
                    { "language", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "en" },
                    { "monthly_summary_day", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "1" },
                    { "monthly_summary_time", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "09:00" },
                    { "notifications_enabled", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "true" },
                    { "onboarding_complete", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "false" },
                    { "schema_version", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "2.3" },
                    { "theme", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "system" }
                });

            migrationBuilder.InsertData(
                table: "export_templates",
                columns: new[] { "id", "created_at", "created_by_plugin_id", "description", "is_official", "layout_json", "name", "updated_at", "updated_at_ms" },
                values: new object[,]
                {
                    { new Guid("00000004-0000-0000-0000-000000000001"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Full journal export covering all journal-type habits across a chosen date range.", true, "{\"blocks\":[{\"type\":\"title\",\"text\":\"Global Journal\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"journal\"},{\"type\":\"completion_timeline\",\"include_notes\":true},{\"type\":\"attachment_gallery\"}]}", "Global Journal", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000004-0000-0000-0000-000000000002"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Performance report for Fitness category habits with metric charts and streak summaries.", true, "{\"blocks\":[{\"type\":\"title\",\"text\":\"Fitness Report\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"category:Fitness\"},{\"type\":\"metric_chart\",\"chart_type\":\"line\"},{\"type\":\"streak_summary\"},{\"type\":\"goal_progress\"}]}", "Fitness Report", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000004-0000-0000-0000-000000000003"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Clinical-style report for Health category habits, suitable for sharing with a practitioner.", true, "{\"blocks\":[{\"type\":\"title\",\"text\":\"Health Report\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"category:Health\"},{\"type\":\"completion_heatmap\"},{\"type\":\"metric_chart\",\"chart_type\":\"bar\"},{\"type\":\"note_list\"}]}", "Health Report", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000004-0000-0000-0000-000000000004"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Compact overview of all habits with streak indicators for a chosen date range.", true, "{\"blocks\":[{\"type\":\"title\",\"text\":\"Summary\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"all\",\"show_streaks\":true},{\"type\":\"completion_timeline\"}]}", "Minimal Summary", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L }
                });

            migrationBuilder.InsertData(
                table: "habit_templates",
                columns: new[] { "id", "category", "created_at", "created_by_plugin_id", "description", "is_official", "name", "payload_json", "updated_at", "updated_at_ms" },
                values: new object[,]
                {
                    { new Guid("00000003-0000-0000-0000-000000000001"), "Daily routines", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "A daily action habit to anchor your morning routine.", true, "Morning routine", "{\"node\":{\"name\":\"Morning routine\",\"node_type\":\"Habit\",\"habit_type\":\"Action\",\"color\":\"#f5a623\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[],\"segments\":[]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000002"), "Fitness", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Session-mode habit for logging gym workouts with Work/Rest segments.", true, "Workout log", "{\"node\":{\"name\":\"Workout log\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"},\"config\":{\"schedule_type\":\"SpecificDays\",\"streak_mode\":\"Weekly\",\"streak_recovery\":\"None\",\"has_integrated_timer\":true,\"timer_mode\":\"Session\"},\"metrics\":[{\"name\":\"Duration\",\"type\":\"Seconds\"},{\"name\":\"Reps\",\"type\":\"Integer\"},{\"name\":\"Weight\",\"type\":\"Decimal\"}],\"segments\":[{\"name\":\"Work\",\"is_rest\":false,\"sort_order\":0},{\"name\":\"Rest\",\"is_rest\":true,\"sort_order\":1}]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000003"), "Daily routines", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "A free-form Markdown journal entry, one per day.", true, "Daily journal", "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[],\"segments\":[]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000004"), "Health", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Track daily water consumption in litres with a 2L goal.", true, "Water intake", "{\"node\":{\"name\":\"Water intake\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#5bc8f5\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Amount\",\"type\":\"Decimal\",\"unit\":\"l\"}],\"segments\":[],\"goal\":{\"target\":2,\"unit\":\"l\",\"period_type\":\"Open\"}}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000005"), "Health", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Log hours slept and subjective sleep quality each morning.", true, "Sleep tracker", "{\"node\":{\"name\":\"Sleep tracker\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Hours slept\",\"type\":\"Decimal\",\"unit\":\"hours\"},{\"name\":\"Sleep quality\",\"type\":\"Integer\",\"scale_code\":\"sleep-quality\"}],\"segments\":[]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000006"), "Health", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Resistance habit that tracks abstinence time since last cigarette.", true, "Stop smoking", "{\"node\":{\"name\":\"Stop smoking\",\"node_type\":\"Habit\",\"habit_type\":\"Resistance\",\"color\":\"#27ae60\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"None\"},\"metrics\":[],\"segments\":[]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000007"), "Wellbeing", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Daily mood state on a 1–5 labelled scale.", true, "Mood tracker", "{\"node\":{\"name\":\"Mood tracker\",\"node_type\":\"Habit\",\"habit_type\":\"State\",\"color\":\"#f39c12\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Mood\",\"type\":\"Integer\",\"scale_code\":\"mood-5\"}],\"segments\":[]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L },
                    { new Guid("00000003-0000-0000-0000-000000000008"), "Health", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, "Log meals with multi-series entries (food name, grams, calories).", true, "Food diary", "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Food name\",\"type\":\"Text\"},{\"name\":\"Grams\",\"type\":\"Decimal\",\"unit\":\"g\"},{\"name\":\"Calories\",\"type\":\"Decimal\",\"unit\":\"kcal\"}],\"segments\":[]}", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1778716800000L }
                });

            migrationBuilder.InsertData(
                table: "scale_templates",
                columns: new[] { "id", "code", "created_at", "is_official", "labels_json", "max_value", "min_value", "name" },
                values: new object[,]
                {
                    { new Guid("00000002-0000-0000-0000-000000000001"), "bristol", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"Hard pellets\",\"Lumpy sausage\",\"Cracked sausage\",\"Smooth sausage\",\"Soft blobs\",\"Mushy\",\"Liquid\"]", 7, 1, "Bristol Stool Scale" },
                    { new Guid("00000002-0000-0000-0000-000000000002"), "mayo-rectal-bleeding", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"No bleeding\",\"Streaks on tissue\",\"Visible blood with stool\",\"Bleeding without stool\"]", 3, 0, "Mayo Rectal Bleeding Score" },
                    { new Guid("00000002-0000-0000-0000-000000000003"), "nrs-pain", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"No pain\",\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\",\"9\",\"Worst pain\"]", 10, 0, "NRS Pain Scale" },
                    { new Guid("00000002-0000-0000-0000-000000000004"), "likert-5", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"Strongly disagree\",\"Disagree\",\"Neutral\",\"Agree\",\"Strongly agree\"]", 5, 1, "Likert Scale (1–5)" },
                    { new Guid("00000002-0000-0000-0000-000000000005"), "sleep-quality", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"Very poor\",\"Poor\",\"Fair\",\"Good\",\"Excellent\"]", 5, 1, "Sleep Quality (1–5)" },
                    { new Guid("00000002-0000-0000-0000-000000000006"), "stress-10", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"Calm\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\",\"9\",\"Overwhelmed\"]", 10, 1, "Stress Level (1–10)" },
                    { new Guid("00000002-0000-0000-0000-000000000007"), "mood-5", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "[\"Very low\",\"Low\",\"Neutral\",\"Good\",\"Very good\"]", 5, 1, "Mood Scale (1–5)" }
                });

            migrationBuilder.InsertData(
                table: "units_of_measure",
                columns: new[] { "id", "code", "created_at", "is_official", "name", "symbol" },
                values: new object[,]
                {
                    { new Guid("00000001-0000-0000-0000-000000000001"), "km", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Kilometre", "km" },
                    { new Guid("00000001-0000-0000-0000-000000000002"), "m", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Metre", "m" },
                    { new Guid("00000001-0000-0000-0000-000000000003"), "kg", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Kilogram", "kg" },
                    { new Guid("00000001-0000-0000-0000-000000000004"), "g", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Gram", "g" },
                    { new Guid("00000001-0000-0000-0000-000000000005"), "ml", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Millilitre", "ml" },
                    { new Guid("00000001-0000-0000-0000-000000000006"), "l", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Litre", "l" },
                    { new Guid("00000001-0000-0000-0000-000000000007"), "kcal", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Kilocalorie", "kcal" },
                    { new Guid("00000001-0000-0000-0000-000000000008"), "steps", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Steps", null },
                    { new Guid("00000001-0000-0000-0000-000000000009"), "hours", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Hours", "h" },
                    { new Guid("00000001-0000-0000-0000-000000000010"), "minutes", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Minutes", "min" },
                    { new Guid("00000001-0000-0000-0000-000000000011"), "seconds", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Seconds", "s" },
                    { new Guid("00000001-0000-0000-0000-000000000012"), "pages", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Pages", null },
                    { new Guid("00000001-0000-0000-0000-000000000013"), "reps", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Repetitions", null },
                    { new Guid("00000001-0000-0000-0000-000000000014"), "sets", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), true, "Sets", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_insights_habit_node_id_valid_until",
                table: "ai_insights",
                columns: new[] { "habit_node_id", "valid_until" });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_completion_id",
                table: "attachments",
                column: "completion_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_note_id",
                table: "attachments",
                column: "note_id");

            migrationBuilder.CreateIndex(
                name: "ix_chart_series_chart_id_sort_order",
                table: "chart_series",
                columns: new[] { "chart_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_chart_series_habit_node_id",
                table: "chart_series",
                column: "habit_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_chart_series_metric_definition_id",
                table: "chart_series",
                column: "metric_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_charts_created_by_plugin_id",
                table: "charts",
                column: "created_by_plugin_id");

            migrationBuilder.CreateIndex(
                name: "ix_completion_segments_completion_id_segment_index",
                table: "completion_segments",
                columns: new[] { "completion_id", "segment_index" });

            migrationBuilder.CreateIndex(
                name: "ix_completion_segments_segment_definition_id",
                table: "completion_segments",
                column: "segment_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_completion_values_completion_id",
                table: "completion_values",
                column: "completion_id");

            migrationBuilder.CreateIndex(
                name: "ix_completion_values_completion_segment_id",
                table: "completion_values",
                column: "completion_segment_id");

            migrationBuilder.CreateIndex(
                name: "ix_completion_values_metric_definition_id",
                table: "completion_values",
                column: "metric_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_completions_habit_node_id_completed_at",
                table: "completions",
                columns: new[] { "habit_node_id", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_export_jobs_created_at",
                table: "export_jobs",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_export_jobs_export_template_id",
                table: "export_jobs",
                column: "export_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_export_templates_created_by_plugin_id",
                table: "export_templates",
                column: "created_by_plugin_id");

            migrationBuilder.CreateIndex(
                name: "ix_goals_habit_node_id",
                table: "goals",
                column: "habit_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_goals_metric_definition_id",
                table: "goals",
                column: "metric_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_config_histories_habit_node_id_valid_from",
                table: "habit_config_histories",
                columns: new[] { "habit_node_id", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_habit_configs_habit_node_id",
                table: "habit_configs",
                column: "habit_node_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_habit_node_tags_tag_id",
                table: "habit_node_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_nodes_created_by_plugin_id",
                table: "habit_nodes",
                column: "created_by_plugin_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_nodes_deleted_at",
                table: "habit_nodes",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_habit_nodes_node_type",
                table: "habit_nodes",
                column: "node_type");

            migrationBuilder.CreateIndex(
                name: "ix_habit_nodes_parent_id",
                table: "habit_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_templates_created_by_plugin_id",
                table: "habit_templates",
                column: "created_by_plugin_id");

            migrationBuilder.CreateIndex(
                name: "ix_metric_definitions_habit_node_id",
                table: "metric_definitions",
                column: "habit_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_metric_definitions_scale_template_id",
                table: "metric_definitions",
                column: "scale_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_metric_definitions_unit_of_measure_id",
                table: "metric_definitions",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_habit_node_id_created_at",
                table: "notes",
                columns: new[] { "habit_node_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_habit_node_id",
                table: "notifications",
                column: "habit_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_scale_templates_code",
                table: "scale_templates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_segment_definitions_habit_node_id",
                table: "segment_definitions",
                column: "habit_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_streak_pause_habit_node_id_pause_from",
                table: "streak_pauses",
                columns: new[] { "habit_node_id", "pause_from" });

            migrationBuilder.CreateIndex(
                name: "ix_sync_meta_device_id",
                table: "sync_meta",
                column: "device_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tag_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_units_of_measure_code",
                table: "units_of_measure",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_insights");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "chart_series");

            migrationBuilder.DropTable(
                name: "completion_values");

            migrationBuilder.DropTable(
                name: "export_jobs");

            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "habit_config_histories");

            migrationBuilder.DropTable(
                name: "habit_configs");

            migrationBuilder.DropTable(
                name: "habit_node_tags");

            migrationBuilder.DropTable(
                name: "habit_templates");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "streak_pauses");

            migrationBuilder.DropTable(
                name: "sync_meta");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "charts");

            migrationBuilder.DropTable(
                name: "completion_segments");

            migrationBuilder.DropTable(
                name: "export_templates");

            migrationBuilder.DropTable(
                name: "metric_definitions");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "completions");

            migrationBuilder.DropTable(
                name: "segment_definitions");

            migrationBuilder.DropTable(
                name: "scale_templates");

            migrationBuilder.DropTable(
                name: "units_of_measure");

            migrationBuilder.DropTable(
                name: "habit_nodes");
        }
    }
}
