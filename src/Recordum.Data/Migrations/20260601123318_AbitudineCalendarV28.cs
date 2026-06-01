using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AbitudineCalendarV28 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_seconds",
                table: "segment_definitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "target_date",
                table: "notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note_marker_color",
                table: "habit_configs",
                type: "TEXT",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "session_repeat_count",
                table: "habit_configs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "session_repeat_count",
                table: "habit_config_histories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2.8" });

            migrationBuilder.CreateIndex(
                name: "ix_notes_habit_node_id_target_date",
                table: "notes",
                columns: new[] { "habit_node_id", "target_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notes_habit_node_id_target_date",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "segment_definitions");

            migrationBuilder.DropColumn(
                name: "target_date",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "note_marker_color",
                table: "habit_configs");

            migrationBuilder.DropColumn(
                name: "session_repeat_count",
                table: "habit_configs");

            migrationBuilder.DropColumn(
                name: "session_repeat_count",
                table: "habit_config_histories");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "2.7" });
        }
    }
}
