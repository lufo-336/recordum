using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerTypePrefsV214 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ui_prefs_json",
                table: "habit_nodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "repeatable",
                table: "habit_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "series_labels_json",
                table: "habit_configs",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "repeatable",
                table: "habit_config_histories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "series_labels_json",
                table: "habit_config_histories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 7, 9, 0, 0, 0, 0, DateTimeKind.Utc), "2.14" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_habit_config_repeatable",
                table: "habit_configs",
                sql: "repeatable IN (0,1)");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_habit_config_repeatable",
                table: "habit_configs");

            migrationBuilder.DropColumn(
                name: "ui_prefs_json",
                table: "habit_nodes");

            migrationBuilder.DropColumn(
                name: "repeatable",
                table: "habit_configs");

            migrationBuilder.DropColumn(
                name: "series_labels_json",
                table: "habit_configs");

            migrationBuilder.DropColumn(
                name: "repeatable",
                table: "habit_config_histories");

            migrationBuilder.DropColumn(
                name: "series_labels_json",
                table: "habit_config_histories");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 7, 8, 0, 0, 0, 0, DateTimeKind.Utc), "2.13" });
        }
    }
}
