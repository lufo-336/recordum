using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFoundationV221 : Migration
    {
        private static (string Table, string Source)[] BackfillSources =>
        [
            ("app_settings", Ms("updated_at")),
            ("units_of_measure", Ms("created_at")),
            ("tags", Ms("created_at")),
            ("scale_templates", Ms("created_at")),
            ("habit_config_histories", Ms("COALESCE(valid_until, valid_from)")),
            ("goals", Ms("COALESCE(closed_at, created_at)")),
            ("notifications", Ms("created_at")),
            ("streak_pauses", Ms("created_at")),
            ("habit_node_tags", Ms("created_at")),
            ("note_histories", Ms("created_at")),
            ("completions", "COALESCE(completed_at_ms, 0)"),
            ("completion_segments", "COALESCE(started_at_ms, 0)"),
        ];

        private static string Ms(string column) =>
            $"COALESCE(CAST(ROUND(unixepoch({column}, 'subsec') * 1000) AS INTEGER), 0)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "units_of_measure",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "streak_pauses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "segment_definitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "scale_templates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "notifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "last_edited_device_name",
                table: "notes",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "note_tag",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "note_histories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "metric_definitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "habit_node_tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "habit_config_histories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "goals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "last_edited_device_name",
                table: "completions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "completions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "completion_values",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "completion_segments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "chart_series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "updated_at_ms",
                table: "app_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "sync_base",
                columns: table => new
                {
                    peer_device_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    entity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false),
                    row_json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_base", x => new { x.peer_device_id, x.entity, x.entity_id });
                });

            migrationBuilder.CreateTable(
                name: "sync_tombstone",
                columns: table => new
                {
                    entity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    alias_of = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_tombstone", x => new { x.entity, x.entity_id });
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "ai_default_sharing",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "ai_mode",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "app_lock_enabled",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "app_lock_type",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.enabled",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.frequency_days",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.last_run_utc",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.path",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.retention_count",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "daily_reset_time",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "inactivity_timeout_minutes",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "language",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "monthly_summary_day",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "monthly_summary_time",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "notifications_enabled",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "onboarding_complete",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "updated_at_ms", "value" },
                values: new object[] { new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Utc), 0L, "2.21" });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "theme",
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000001"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000002"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000003"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000004"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000005"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000006"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "scale_templates",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000007"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000001"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000002"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000003"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000004"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000005"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000006"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000007"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000008"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000009"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000010"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000011"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000012"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000013"),
                column: "updated_at_ms",
                value: 0L);

            migrationBuilder.UpdateData(
                table: "units_of_measure",
                keyColumn: "id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000014"),
                column: "updated_at_ms",
                value: 0L);

            // Backfill di updated_at_ms (dopo i seed, che lo portano a 0): per ogni tabella l'istante che ha, nell'ordine
            // updated_at, created_at, altrimenti 0. EF scrive i DateTime come 'yyyy-MM-dd HH:mm:ss[.fffffff]' (misurato,
            // senza T né Z); unixepoch(..., 'subsec') legge anche la frazione e il ROUND evita l'errore del double.
            // Un testo illeggibile dà NULL e quindi 0. completions ha già completed_at_ms; le tabelle figlie senza
            // colonna di tempo restano a 0 (default).
            foreach (var (table, source) in BackfillSources)
            {
                migrationBuilder.Sql(
                    $"UPDATE {table} SET updated_at_ms = {source};");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var entity in Sync.SyncCatalog.Entities)
            {
                var table = entity.Table.Replace("\"", "\"\"", StringComparison.Ordinal);
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS "trg_sync_del_{table}";
                    DROP TRIGGER IF EXISTS "trg_sync_upd_{table}";
                    DROP TRIGGER IF EXISTS "trg_sync_ins_{table}";
                    DROP TRIGGER IF EXISTS "trg_sync_fms_{table}";
                    """);
            }

            migrationBuilder.DropTable(
                name: "sync_base");

            migrationBuilder.DropTable(
                name: "sync_tombstone");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "units_of_measure");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "streak_pauses");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "segment_definitions");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "scale_templates");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "last_edited_device_name",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "note_tag");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "note_histories");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "metric_definitions");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "habit_node_tags");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "habit_config_histories");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "last_edited_device_name",
                table: "completions");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "completions");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "completion_values");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "completion_segments");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "chart_series");

            migrationBuilder.DropColumn(
                name: "updated_at_ms",
                table: "app_settings");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Utc), "2.20" });
        }
    }
}
