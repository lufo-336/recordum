using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class FoodDiaryQuickNoteV217 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.17");

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000003"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"Diary\"},\"metrics\":[],\"segments\":[]}");

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000008"),
                columns: new[] { "description", "payload_json" },
                values: new object[] { "Log each meal in seconds: a short note and a photo, as many times a day as you eat.", "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"QuickNote\"},\"metrics\":[],\"segments\":[]}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.16");

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000003"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[],\"segments\":[]}");

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000008"),
                columns: new[] { "description", "payload_json" },
                values: new object[] { "Log meals with multi-series entries (food name, grams, calories).", "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Food name\",\"type\":\"Text\"},{\"name\":\"Grams\",\"type\":\"Decimal\",\"unit\":\"g\"},{\"name\":\"Calories\",\"type\":\"Decimal\",\"unit\":\"kcal\"}],\"segments\":[]}" });
        }
    }
}
